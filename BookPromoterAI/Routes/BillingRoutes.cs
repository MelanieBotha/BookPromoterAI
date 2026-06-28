namespace BookPromoterAI;

static class BillingRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/subscription", (HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            if (store.HasCustomerAccess) return Results.Redirect("/billing");
            var cancelled = http.Request.Query.ContainsKey("cancelled")
                ? """<div class="notice">Checkout cancelled. Choose a plan when you're ready.</div>"""
                : "";
            return Results.Content(H.RenderPage(http, "Subscribe", BillingPage.SubscribePage(store, cancelled), store), "text/html");
        });

        app.MapGet("/subscription/checkout", (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            if (store.HasCustomerAccess) return Results.Redirect("/billing");
            var planId = request.Query["plan"].ToString();
            if (string.IsNullOrWhiteSpace(planId))
                return Results.Redirect("/subscription");
            return Results.Content(H.RenderPage(http, "Checkout", BillingPage.CheckoutPage(store, planId, ""), store), "text/html");
        });

        app.MapPost("/subscription", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            if (store.IsBillingConfigured)
                return Results.Redirect("/subscription");

            var form = await request.ReadFormAsync();
            var planId = form["plan"].ToString();
            var payment = PaymentOptions.Parse(form);
            var result = store.StartPaidSubscription(form["email"].ToString(), planId, payment);
            if (result.Success) return Results.Redirect("/dashboard");
            var notice = $"""<div class="notice error">{H.Encode(result.Message)}</div>""";
            return Results.Content(H.RenderPage(http, "Checkout", BillingPage.CheckoutPage(store, planId, notice, payment), store), "text/html");
        });

        app.MapPost("/subscription/stripe", async (HttpRequest request, HttpContext http, AppStoreDb store, StripeBillingService stripe) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var planId = form["plan"].ToString();
            var user = store.GetCurrentDbUser();
            var plan = store.GetDbPlan(planId);
            if (user is null || plan is null)
                return BillingError(http, store, planId, "Choose a valid plan.");

            var (ok, url, error) = await stripe.CreateCheckoutSessionAsync(request, user, plan, planId);
            if (ok && url is not null) return Results.Redirect(url);
            return BillingError(http, store, planId, error ?? "Could not start Stripe checkout.");
        });

        app.MapGet("/subscription/success", async (HttpRequest request, HttpContext http, AppStoreDb store, StripeBillingService stripe) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            var sessionId = request.Query["session_id"].ToString();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                var notice = """<div class="notice error">Missing payment confirmation from Stripe. If you were charged, check Subscription &amp; Billing or contact support.</div>""";
                return Results.Content(H.RenderPage(http, "Subscribe", BillingPage.SubscribePage(store, notice), store), "text/html");
            }

            var user = store.GetCurrentDbUser();
            var (ok, planId, error) = await stripe.TryFulfillCheckoutSessionAsync(sessionId, store, user?.Id);
            if (ok) return Results.Redirect("/dashboard?subscribed=1");

            var errNotice = $"""<div class="notice error">{H.Encode(error ?? "Payment could not be confirmed.")}</div>""";
            if (!string.IsNullOrWhiteSpace(planId))
                return Results.Content(H.RenderPage(http, "Checkout", BillingPage.CheckoutPage(store, planId, errNotice), store), "text/html");
            return Results.Content(H.RenderPage(http, "Subscribe", BillingPage.SubscribePage(store, errNotice), store), "text/html");
        });

        app.MapPost("/subscription/change", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            if (store.IsBillingConfigured)
            {
                var notice = store.HasProviderSubscription
                    ? """<div class="notice">To change plans, use Manage Billing (Stripe) or cancel and resubscribe.</div>"""
                    : """<div class="notice">Choose a plan and complete Stripe checkout to subscribe. Plan changes without payment are not allowed.</div>""";
                return Results.Content(H.RenderPage(http, "Subscription &amp; Billing", BillingPage.Render(store, notice), store), "text/html");
            }
            var form = await request.ReadFormAsync();
            var result = store.ChangePlan(form["plan"].ToString());
            var cls = result.Success ? "success" : "error";
            var changeNotice = $"""<div class="notice {cls}">{H.Encode(result.Message)}</div>""";
            return Results.Content(H.RenderPage(http, "Subscription &amp; Billing", BillingPage.Render(store, changeNotice), store), "text/html");
        });

        app.MapPost("/subscription/promo-code", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
            await RedeemPromoCodeAsync(request, http, store, subscribePage: true));

        app.MapGet("/billing", (HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            return Results.Content(H.RenderPage(http, "Subscription &amp; Billing", BillingPage.Render(store, ""), store), "text/html");
        });

        app.MapPost("/billing/stripe-portal", async (HttpRequest request, HttpContext http, AppStoreDb store, StripeBillingService stripe) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var user = store.GetCurrentDbUser();
            if (user is null) return Results.Redirect("/billing");
            var (ok, url, error) = await stripe.CreatePortalSessionAsync(request, user);
            if (ok && url is not null) return Results.Redirect(url);
            var notice = $"""<div class="notice error">{H.Encode(error ?? "Could not open billing portal.")}</div>""";
            return Results.Content(H.RenderPage(http, "Subscription &amp; Billing", BillingPage.Render(store, notice), store), "text/html");
        });

        app.MapPost("/billing/payment-method", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            if (store.IsBillingConfigured)
                return Results.Redirect("/billing");

            var form = await request.ReadFormAsync();
            var payment = PaymentOptions.Parse(form);
            var message = store.SavePaymentMethod(payment);
            var cls = message == "Payment method saved." ? "success" : "error";
            return Results.Content(H.RenderPage(http, "Subscription &amp; Billing", BillingPage.Render(store, $"""<div class="notice {cls}">{H.Encode(message)}</div>""", payment), store), "text/html");
        });

        app.MapPost("/billing/promo-code", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
            await RedeemPromoCodeAsync(request, http, store, subscribePage: false));

        app.MapPost("/billing/cancel", async (HttpContext http, AppStoreDb store, StripeBillingService stripe) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var user = store.GetCurrentDbUser();
            if (user is null) return Results.Redirect("/billing");

            if (user.PaymentProvider == "stripe" && !string.IsNullOrWhiteSpace(user.StripeSubscriptionId))
            {
                var (ok, error) = await stripe.CancelSubscriptionAsync(user);
                if (!ok)
                {
                    var errNotice = $"""<div class="notice error">{H.Encode(error ?? "Stripe cancellation failed.")}</div>""";
                    return Results.Content(H.RenderPage(http, "Subscription &amp; Billing", BillingPage.Render(store, errNotice), store), "text/html");
                }
            }
            else if (user.PaymentProvider == "paypal")
            {
                var errNotice = """<div class="notice error">PayPal billing is no longer supported here. Cancel your PayPal subscription from your PayPal account, then subscribe again with Stripe if needed.</div>""";
                return Results.Content(H.RenderPage(http, "Subscription &amp; Billing", BillingPage.Render(store, errNotice), store), "text/html");
            }

            var result = store.CancelSubscription();
            var cls = result.Success ? "success" : "error";
            var notice = $"""<div class="notice {cls}">{H.Encode(result.Message)}</div>""";
            return Results.Content(H.RenderPage(http, "Subscription &amp; Billing", BillingPage.Render(store, notice), store), "text/html");
        });
    }

    static IResult BillingError(HttpContext http, AppStoreDb store, string planId, string message) =>
        Results.Content(
            H.RenderPage(http, "Checkout", BillingPage.CheckoutPage(store, planId, $"""<div class="notice error">{H.Encode(message)}</div>"""), store),
            "text/html");

    static async Task<IResult> RedeemPromoCodeAsync(HttpRequest request, HttpContext http, AppStoreDb store, bool subscribePage)
    {
        if (!store.IsLoggedIn) return Results.Redirect("/start");

        var form = await request.ReadFormAsync();
        var result = store.RedeemPromoCode(null, form["promoCode"].ToString());
        if (result.Success) return Results.Redirect("/dashboard");

        var cls = "error";
        var notice = $"""<div class="notice {cls}">{H.Encode(result.Message)}</div>""";
        if (subscribePage)
            return Results.Content(H.RenderPage(http, "Subscribe", BillingPage.SubscribePage(store, notice), store), "text/html");

        if (!store.HasCustomerAccess) return Results.Redirect("/subscription");
        return Results.Content(H.RenderPage(http, "Subscription &amp; Billing", BillingPage.Render(store, notice), store), "text/html");
    }
}
