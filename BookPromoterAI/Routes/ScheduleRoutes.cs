namespace BookPromoterAI;

static class ScheduleRoutes
{
    public static void Map(WebApplication app, PostGenerator generator)
    {
        app.MapGet("/schedule", () => Results.Redirect("/my-account"));

        app.MapPost("/schedule", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings, SocialPostingService postingService) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");

            var form = await request.ReadFormAsync();
            var platforms = form["platform"].ToList();
            var postsPerWeek = form["postsPerWeek"].ToList();
            var approvals = form["requiresApproval"].ToHashSet(StringComparer.OrdinalIgnoreCase);
            var autoPostPlatforms = form["autoPostEnabled"].ToHashSet(StringComparer.OrdinalIgnoreCase);

            var updatedSchedules = new List<SocialSchedule>();
            for (var i = 0; i < platforms.Count; i++)
            {
                var platform = platforms[i] ?? "";
                if (string.IsNullOrWhiteSpace(platform)) continue;
                var parsed = int.TryParse(postsPerWeek.ElementAtOrDefault(i), out var count) ? count : 0;
                updatedSchedules.Add(new SocialSchedule
                {
                    Platform = platform,
                    PostsPerWeek = Math.Clamp(parsed, 0, 14),
                    RequiresApproval = approvals.Contains(platform),
                    AutoPostEnabled = autoPostPlatforms.Contains(platform)
                });
            }

            var scaledNotice = "";
            var maxWeeklyPosts = store.CurrentPlan?.MaxWeeklyPosts;
            if (maxWeeklyPosts is int cap)
            {
                var total = updatedSchedules.Sum(s => s.PostsPerWeek);
                if (total > cap)
                {
                    var scale = (double)cap / total;
                    var remaining = cap;
                    var withPosts = updatedSchedules.Where(s => s.PostsPerWeek > 0).ToList();
                    for (var i = 0; i < withPosts.Count; i++)
                    {
                        var s = withPosts[i];
                        var scaled = i == withPosts.Count - 1 ? remaining : Math.Max(0, (int)Math.Floor(s.PostsPerWeek * scale));
                        s.PostsPerWeek = Math.Min(s.PostsPerWeek, scaled);
                        remaining -= s.PostsPerWeek;
                    }
                    scaledNotice = $"""<div class="notice error">Your {H.Encode(store.CurrentPlan!.Name)} plan allows up to {cap} posts/week. Schedule scaled down.</div>""";
                }
            }

            store.SaveSchedules(updatedSchedules);
            var newAds = store.GenerateWeeklyPosts(generator, PublicUrl.Base(request, settings));
            var userId = store.GetCurrentDbUser()?.Id;
            var posted = userId is int uid
                ? await store.RunDuePostsAsync(postingService, uid)
                : await store.RunDuePostsAsync(postingService);

            var notice = BuildScheduleSavedNotice(store, updatedSchedules, newAds.Count, posted);
            if (!string.IsNullOrEmpty(scaledNotice))
                notice = scaledNotice + notice;
            return Results.Content(H.RenderPage(http, "My Account", MyAccountPage.Render(store, notice), store), "text/html");
        });

        app.MapPost("/schedule/remove-platform/{platform}", (string platform, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            store.RemoveSchedule(Uri.UnescapeDataString(platform));
            return Results.Redirect("/my-account");
        });
    }

    static string BuildScheduleSavedNotice(AppStoreDb store, List<SocialSchedule> schedules, int generatedCount, int postedCount)
    {
        var lines = new List<string> { "Posting schedule saved." };
        if (generatedCount > 0)
            lines.Add($"{generatedCount} post(s) updated for this week — check the Ad Library.");
        if (postedCount > 0)
            lines.Add($"{postedCount} post(s) auto-posted now (simulated until real OAuth is connected — see Posting Activity Log below).");
        else
        {
            foreach (var schedule in schedules.Where(s => s.AutoPostEnabled))
            {
                foreach (var blocker in store.GetAutoPostBlockers(schedule.Platform))
                    lines.Add($"{schedule.Platform}: {blocker}");
            }
        }

        if (schedules.Any(s => s.AutoPostEnabled))
            lines.Add("Real social networks are not posted to yet — connect OAuth when available, or copy posts from the Ad Library.");

        var body = string.Join("<br>", lines.Select(H.Encode));
        return $"""<div class="notice success">{body}</div>""";
    }
}
