namespace BookPromoterAI;

static class MyAccountRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/my-account", (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            if (!store.HasCustomerAccess) return Results.Redirect("/start");
            var notice = request.Query["saved"] == "1"
                ? """<div class="notice success">Posting schedule saved. This week's posts have been generated — check the Ad Library.</div>"""
                : "";
            return Results.Content(H.RenderPage(http, "My Account", MyAccountPage.Render(store, notice), store), "text/html");
        });

        app.MapPost("/my-account/facebook-diagnostics", async (
            HttpRequest request,
            HttpContext http,
            AppStoreDb store,
            FacebookService facebook) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            if (!store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var runProbePost = form.ContainsKey("runProbePost");
            var diagnostics = await store.RunFacebookPostingDiagnosticsAsync(facebook, includeAllAuthorAccounts: false, runProbePost);
            var html = FacebookDiagnosticsHtml.RenderPanel(diagnostics, "/my-account/facebook-diagnostics", "facebook-diagnostics");
            return Results.Content(H.RenderPage(http, "My Account", MyAccountPage.Render(store, "", facebookDiagnosticsHtml: html), store), "text/html");
        });

        app.MapPost("/my-account/delete", (AppStoreDb store) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            store.DeleteAccount();
            return Results.Redirect("/start");
        });
    }
}
