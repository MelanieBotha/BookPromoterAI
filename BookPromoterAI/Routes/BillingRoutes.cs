namespace BookPromoterAI;

static class BillingRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/subscription", (HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            if (store.HasCustomerAccess) return Results.Redirect("/billing");
            return Results.Content(H.RenderPage(http, "Subscribe", BillingPage.SubscribePage(store, ""), store), "text/html");
        });

        app.MapPost("/subscription", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var payment = PaymentOptions.Parse(form);
            var result = store.StartPaidSubscription(form["email"].ToString(), form["plan"].ToString(), payment);
            if (result.Success) return Results.Redirect("/dashboard");
            var notice = $"""<div class="notice error">{H.Encode(result.Message)}</div>""";
            return Results.Content(H.RenderPage(http, "Subscribe", BillingPage.SubscribePage(store, notice, payment), store), "text/html");
        });

        app.MapPost("/subscription/change", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var result = store.ChangePlan(form["plan"].ToString());
            var cls = result.Success ? "success" : "error";
            var notice = $"""<div class="notice {cls}">{H.Encode(result.Message)}</div>""";
            return Results.Content(H.RenderPage(http, "Subscription &amp; Billing", BillingPage.Render(store, notice), store), "text/html");
        });

        app.MapGet("/billing", (HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            return Results.Content(H.RenderPage(http, "Subscription &amp; Billing", BillingPage.Render(store, ""), store), "text/html");
        });

        app.MapPost("/billing/payment-method", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var payment = PaymentOptions.Parse(form);
            var message = store.SavePaymentMethod(payment);
            var cls = message == "Payment method saved." ? "success" : "error";
            return Results.Content(H.RenderPage(http, "Subscription &amp; Billing", BillingPage.Render(store, $"""<div class="notice {cls}">{H.Encode(message)}</div>""", payment), store), "text/html");
        });

        app.MapPost("/billing/cancel", (HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var result = store.CancelSubscription();
            var cls = result.Success ? "success" : "error";
            var notice = $"""<div class="notice {cls}">{H.Encode(result.Message)}</div>""";
            return Results.Content(H.RenderPage(http, "Subscription &amp; Billing", BillingPage.Render(store, notice), store), "text/html");
        });
    }
}
