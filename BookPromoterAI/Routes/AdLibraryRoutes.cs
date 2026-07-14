namespace BookPromoterAI;

static class AdLibraryRoutes
{
    public static void Map(WebApplication app, PostGenerator generator)
    {
        app.MapGet("/ad-library", (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            store.EnsureCurrentWeekPostSlots();
            var search = request.Query["search"].ToString();
            var focus = request.Query["focus"].ToString();
            var notice = request.Query["generated"] == "1"
                ? """<div class="notice success">Unapproved posts for this week were replaced with new books, platforms, and captions. Approved and already-posted ads were left unchanged.</div>"""
                : request.Query["regenerated"] == "1"
                    ? """<div class="notice success">Post regenerated.</div>"""
                    : request.Query["approved"] == "1"
                        ? """<div class="notice success">Post approved for auto-posting.</div>"""
                        : request.Query["posted"] == "1"
                            ? """<div class="notice success">Post published to your connected social account.</div>"""
                            : request.Query["postError"] == "1"
                                ? $"""<div class="notice error">{H.Encode(request.Query["msg"].ToString())}</div>"""
                                : "";
            return Results.Content(H.RenderPage(http, "Ad Library", AdLibraryPage.Render(store, search, notice, focus, request, settings), store, "page-ad-library"), "text/html");
        });

        app.MapPost("/ad-library/generate-week", (HttpRequest request, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            store.GenerateWeeklyPosts(generator, PublicUrl.Base(request, settings), replaceUnapproved: true);
            return Results.Redirect("/ad-library?generated=1");
        });

        app.MapPost("/ad-library/approve/{id:int}", async (HttpRequest request, int id, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            store.ApproveAd(id);
            return Results.Redirect(AdLibraryReturnUrl(form["search"].ToString(), id, approved: true));
        });

        app.MapPost("/ad-library/regenerate/{id:int}", async (HttpRequest request, int id, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var search = form["search"].ToString();
            store.RegenerateAd(id, generator, PublicUrl.Base(request, settings));
            return Results.Redirect(AdLibraryReturnUrl(search, id, regenerated: true));
        });

        app.MapPost("/ad-library/post-now/{id:int}", async (HttpRequest request, int id, AppStoreDb store, SocialPostingService postingService) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var search = form["search"].ToString();
            var (success, message) = await store.PostAdNowAsync(id, postingService);
            if (success)
                return Results.Redirect(AdLibraryReturnUrl(search, id, posted: true));
            return Results.Redirect($"/ad-library?postError=1&msg={Uri.EscapeDataString(message)}&focus=ad-{id}#ad-{id}");
        });
    }

    static string AdLibraryReturnUrl(string search, int focusAdId, bool regenerated = false, bool approved = false, bool posted = false)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) parts.Add($"search={Uri.EscapeDataString(search)}");
        parts.Add($"focus=ad-{focusAdId}");
        if (regenerated) parts.Add("regenerated=1");
        if (approved) parts.Add("approved=1");
        if (posted) parts.Add("posted=1");
        return $"/ad-library?{string.Join("&", parts)}#ad-{focusAdId}";
    }
}
