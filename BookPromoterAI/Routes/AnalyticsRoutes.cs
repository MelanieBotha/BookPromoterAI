namespace BookPromoterAI;

static class AnalyticsRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/analytics", (HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            return Results.Content(H.RenderPage(http, "Analytics", AnalyticsPage.Render(store), store), "text/html");
        });
    }
}
