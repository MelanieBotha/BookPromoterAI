namespace BookPromoterAI;

static class DashboardRoutes
{
    public static void Map(WebApplication app, PostGenerator generator)
    {
        app.MapGet("/dashboard", (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var notice = request.Query.ContainsKey("subscribed")
                ? """<div class="notice success">Payment confirmed. Your subscription is active and your card will be billed monthly for the plan you selected.</div>"""
                : "";
            return Results.Content(H.RenderPage(http, "Dashboard", DashboardPage.Render(store, generator, request, settings, notice), store), "text/html");
        });
    }
}
