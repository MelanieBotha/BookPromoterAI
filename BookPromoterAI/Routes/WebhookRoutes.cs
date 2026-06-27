namespace BookPromoterAI;

static class WebhookRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/webhooks/stripe", async (HttpRequest request, StripeBillingService stripe, AppStoreDb store) =>
        {
            var json = await new StreamReader(request.Body).ReadToEndAsync();
            var signature = request.Headers["Stripe-Signature"].ToString();
            if (!stripe.HandleWebhook(json, signature, store))
                return Results.BadRequest();
            return Results.Ok();
        });

        app.MapPost("/webhooks/paypal", async (HttpRequest request, PayPalBillingService paypal, AppStoreDb store) =>
        {
            var json = await new StreamReader(request.Body).ReadToEndAsync();
            if (!await paypal.HandleWebhookAsync(json, store))
                return Results.BadRequest();
            return Results.Ok();
        });
    }
}
