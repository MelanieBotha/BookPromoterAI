namespace BookPromoterAI;

static class AdLibraryRoutes
{
    public static void Map(WebApplication app, PostGenerator generator)
    {
        app.MapGet("/ad-library", (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var search = request.Query["search"].ToString();
            var focus = request.Query["focus"].ToString();
            var notice = request.Query["generated"] == "1"
                ? """<div class="notice success">This week's unapproved posts have been refreshed with the latest captions and book links. Approved posts were left unchanged.</div>"""
                : request.Query["regenerated"] == "1"
                    ? """<div class="notice success">Post regenerated.</div>"""
                    : request.Query["approved"] == "1"
                        ? """<div class="notice success">Post approved for auto-posting.</div>"""
                        : "";
            return Results.Content(H.RenderPage(http, "Ad Library", AdLibraryPage.Render(store, search, notice, focus, request, settings), store), "text/html");
        });

        app.MapPost("/ad-library/generate-week", (HttpRequest request, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            store.GenerateWeeklyPosts(generator, PublicUrl.Base(request, settings));
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
    }

    static string AdLibraryReturnUrl(string search, int focusAdId, bool regenerated = false, bool approved = false)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) parts.Add($"search={Uri.EscapeDataString(search)}");
        parts.Add($"focus=ad-{focusAdId}");
        if (regenerated) parts.Add("regenerated=1");
        if (approved) parts.Add("approved=1");
        return $"/ad-library?{string.Join("&", parts)}#ad-{focusAdId}";
    }
}
