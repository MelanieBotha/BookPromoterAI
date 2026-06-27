namespace BookPromoterAI;

static class FeedbackRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/feedback", (HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            return Results.Content(H.RenderPage(http, "Feedback &amp; Suggestions", FeedbackPage.Render(store, ""), store), "text/html");
        });

        app.MapPost("/feedback", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var email = form["email"].ToString();
            var category = form["category"].ToString();
            var message = form["message"].ToString();
            if (string.IsNullOrWhiteSpace(message))
                return Results.Content(H.RenderPage(http, "Feedback &amp; Suggestions", FeedbackPage.Render(store, """<div class="notice error">Please enter a message.</div>"""), store), "text/html");
            var entry = store.AddFeedback(email, category, message);
            await EmailService.SendThankYouEmail(email, entry.ThankYouEmail, settings.SendGridApiKey, settings.SendGridSenderEmail, settings.SendGridSenderName);
            return Results.Content(H.RenderPage(http, "Feedback &amp; Suggestions", FeedbackPage.Render(store, """<div class="notice success">Thanks! Your feedback has been submitted.</div>"""), store), "text/html");
        });
    }
}
