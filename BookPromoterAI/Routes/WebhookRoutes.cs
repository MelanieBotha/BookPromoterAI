namespace BookPromoterAI;

static class WebhookRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/webhooks/stripe", async (HttpRequest request, StripeBillingService stripe, AppStoreDb store) =>
        {
            var json = await new StreamReader(request.Body).ReadToEndAsync();
            var signature = request.Headers["Stripe-Signature"].ToString();
            if (!await stripe.HandleWebhookAsync(json, signature, store))
                return Results.BadRequest();
            return Results.Ok();
        });
    }
}
