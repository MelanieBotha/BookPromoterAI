namespace BookPromoterAI;

static class LegalRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/terms", (HttpContext http, AppStoreDb store) =>
            Results.Content(
                H.RenderMarketingPage(http, "Terms & Conditions", LegalPage.TermsAndConditions(), store),
                "text/html"));

        app.MapGet("/terms-and-conditions", () => Results.Redirect("/terms"));

        app.MapGet("/accept-terms", (HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            if (store.HasAcceptedTerms) return Results.Redirect(store.HasCustomerAccess ? "/dashboard" : "/start");
            return Results.Content(
                H.RenderMarketingPage(http, "Accept Terms", LegalPage.AcceptTerms(""), store),
                "text/html");
        });

        app.MapPost("/accept-terms", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            if (store.HasAcceptedTerms) return Results.Redirect(store.HasCustomerAccess ? "/dashboard" : "/start");

            var form = await request.ReadFormAsync();
            var accepted = form["acceptTerms"].ToString();
            if (!accepted.Equals("true", StringComparison.OrdinalIgnoreCase) &&
                !accepted.Equals("on", StringComparison.OrdinalIgnoreCase))
            {
                var notice = """<div class="notice error">You must check the box to accept the Terms &amp; Conditions.</div>""";
                return Results.Content(
                    H.RenderMarketingPage(http, "Accept Terms", LegalPage.AcceptTerms(notice), store),
                    "text/html");
            }

            store.AcceptTerms();
            return Results.Redirect(store.HasCustomerAccess ? "/dashboard" : "/start");
        });
    }
}
