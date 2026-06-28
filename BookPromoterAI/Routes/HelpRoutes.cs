namespace BookPromoterAI;

static class HelpRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/help", (HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            if (!store.HasCustomerAccess) return Results.Redirect("/start");
            return Results.Content(
                H.RenderPage(http, "Help", HelpPage.Render(store, http.Request.Path), store),
                "text/html");
        });
    }
}
