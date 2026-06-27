namespace BookPromoterAI;

static class ScheduleRoutes
{
    public static void Map(WebApplication app, PostGenerator generator)
    {
        app.MapGet("/schedule", () => Results.Redirect("/my-account"));

        app.MapPost("/schedule", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
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
                    store.SaveSchedules(updatedSchedules);
                    var notice = $"""<div class="notice error">Your {H.Encode(store.CurrentPlan!.Name)} plan allows up to {cap} posts/week. Schedule scaled down.</div>""";
                    return Results.Content(H.RenderPage(http, "My Account", MyAccountPage.Render(store, notice), store), "text/html");
                }
            }

            store.SaveSchedules(updatedSchedules);
            store.GenerateWeeklyPosts(generator, PublicUrl.Base(request, settings));
            return Results.Redirect("/my-account?saved=1");
        });

        app.MapPost("/schedule/remove-platform/{platform}", (string platform, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            store.RemoveSchedule(Uri.UnescapeDataString(platform));
            return Results.Redirect("/my-account");
        });
    }
}
