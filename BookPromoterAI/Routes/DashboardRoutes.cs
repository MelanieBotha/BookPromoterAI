namespace BookPromoterAI;

static class DashboardRoutes
{
    public static void Map(WebApplication app, PostGenerator generator)
    {
        app.MapGet("/dashboard", (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            return Results.Content(H.RenderPage(http, "Dashboard", DashboardPage.Render(store, generator, request, settings), store), "text/html");
        });
    }
}
