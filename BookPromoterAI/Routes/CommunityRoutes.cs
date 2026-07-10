namespace BookPromoterAI;

static class CommunityRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/community", (HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            var baseUrl = PublicUrl.Base(http.Request, settings);
            var profile = store.GetBrandCommunityProfile(baseUrl);
            var isAuthor = store.IsLoggedIn && store.HasCustomerAccess;
            return Results.Content(
                H.RenderMarketingPage(http, "Reader Community", CommunityPage.Render(profile, isAuthorContext: isAuthor), store),
                "text/html");
        });
    }
}
