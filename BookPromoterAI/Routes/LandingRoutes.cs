namespace BookPromoterAI;

static class LandingRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/", (HttpContext http, AppStoreDb store) =>
        {
            if (store.IsLoggedIn)
            {
                if (!store.HasAcceptedTerms)
                    return Results.Redirect("/accept-terms");
                if (store.HasCustomerAccess)
                    return Results.Redirect("/dashboard");
            }

            return Results.Content(
                H.RenderMarketingPage(http, "Promote your books smarter", LandingPage.Render(store), store),
                "text/html");
        });
    }
}
