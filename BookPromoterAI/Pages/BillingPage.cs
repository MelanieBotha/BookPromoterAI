using System.Text;
namespace BookPromoterAI;

static class BillingPage
{
    public static string Render(AppStoreDb store, string notice, PaymentMethodInput? paymentValues = null)
    {
        var plan = store.CurrentPlan;
        var planName = plan?.Name ?? store.AccessType;

        var planDetails = store.AccessType == "Lifetime Free (Publisher)"
            ? """<p>Lifetime free access at the Publisher tier. No payment method required.</p>"""
            : store.AccessType == "Free Trial"
                ? $"""<p>You're on an access code (Professional tier features). {H.Encode(store.AccessStatusText)}</p>"""
                : plan is not null
                    ? $"""<p>Current plan: <strong>{H.Encode(plan.Name)}</strong> &mdash; ${plan.MonthlyFee:0.00} USD/month.</p>"""
                    : """<p>No active plan.</p>""";

        if (store.HasProviderSubscription)
        {
            var provider = store.CurrentPaymentProvider ?? "payment provider";
            var status = store.CurrentBillingStatus ?? "active";
            planDetails += $"""<p class="muted">Billed via <strong>{H.Encode(provider)}</strong> &mdash; status: {H.Encode(status)}.</p>""";
            if (store.IsCancelled && store.SubscriptionEndsAt is DateTime ends)
                planDetails += $"""<p class="muted">Cancels on {ends:MMMM d, yyyy}.</p>""";
        }

        var paymentFormSection = PaymentSection(store, paymentValues, isSubscribe: false);

        var cancelSection = store.HasProviderSubscription && store.AccessType != "Lifetime Free (Publisher)" && store.AccessType != "Free Trial"
            ? """
                <section class="panel">
                    <h2>Cancel Subscription</h2>
                    <p class="muted">Your access continues until the end of the current billing period.</p>
                    <form method="post" action="/billing/cancel">
                        <button class="button secondary" type="submit">Cancel at Period End</button>
                    </form>
                </section>
                """
            : "";

        var plansSection = store.HasProviderSubscription
            ? ""
            : PlansSection(store, "/subscription/change", includePayment: false);

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Subscription &amp; Billing</p>
                    <h1>Manage your plan and payment details.</h1>
                    <p class="muted">Pay securely with Stripe (card) or PayPal.</p>
                </div>
            </section>

            {notice}

            <section class="panel">
                <h2>Current Plan: {H.Encode(planName)}</h2>
                {planDetails}
            </section>

            {paymentFormSection}
            {cancelSection}

            {(store.HasProviderSubscription ? "" : """
            <section class="panel">
                <h2>Change Plan</h2>
                <p class="muted">Pick a different plan below to switch at any time.</p>
            </section>
            """)}
            {plansSection}

            <section class="panel">
                <h2>What You Get</h2>
                <p>Add books, create social media posts, set posting schedules, and track link clicks. Higher tiers unlock more books, more connected accounts, and more AI posts per month.</p>
            </section>
            """;
    }

    public static string SubscribePage(AppStoreDb store, string notice, PaymentMethodInput? paymentValues = null)
    {
        var plansSection = PlansSection(store, "/subscription", includePayment: !store.IsBillingConfigured, paymentValues: paymentValues);
        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Plans &amp; Pricing</p>
                    <h1>Choose the plan that matches how many books you're promoting.</h1>
                    <p class="muted">Subscribe with Stripe (card) or PayPal. All prices in USD.</p>
                </div>
            </section>
            {notice}
            {plansSection}
            <section class="panel">
                <h1>What You Get</h1>
                <p>Add books, create social media posts, set posting schedules, and track link clicks.</p>
            </section>
            """;
    }

    static string PaymentSection(AppStoreDb store, PaymentMethodInput? paymentValues, bool isSubscribe)
    {
        if (store.AccessType == "Lifetime Free (Publisher)" || store.AccessType == "Free Trial")
            return """
                <section class="panel">
                    <h2>Payment Method</h2>
                    <p class="muted">No payment method needed for this plan.</p>
                </section>
                """;

        if (store.IsBillingConfigured)
        {
            var manage = store.CurrentPaymentProvider == "stripe" && store.IsStripeConfigured
                ? """<form method="post" action="/billing/stripe-portal"><button class="button" type="submit">Manage Billing (Stripe)</button></form>"""
                : store.CurrentPaymentProvider == "paypal"
                    ? """<p class="muted">Manage your PayPal subscription from your PayPal account.</p>"""
                    : """<p class="muted">Your subscription is managed by your payment provider.</p>""";

            return $"""
                <section class="panel">
                    <h2>Payment Method</h2>
                    <p class="muted">Payments are processed securely. Card details are never stored on this server.</p>
                    {manage}
                </section>
                """;
        }

        var pm = store.CurrentPaymentMethod;
        var paymentSummary = pm is null
            ? """<p class="muted">No payment method on file yet.</p>"""
            : pm.IsBank
                ? $"""<p>Bank account on file: <strong>{H.Encode(pm.Summary)}</strong></p>"""
                : $"""<p>Card on file: <strong>{H.Encode(pm.CardholderName)}</strong> ending in <strong>{H.Encode(pm.Last4)}</strong>, expires {H.Encode(pm.Expiry)} &mdash; {H.Encode(pm.Country)}</p>""";

        paymentValues ??= pm is null
            ? null
            : new PaymentMethodInput(
                pm.PaymentType, "", pm.Region, pm.Country,
                pm.CardholderName, "", pm.Expiry,
                pm.BankName, pm.RoutingOrSortCode, pm.Iban, "");

        var action = isSubscribe ? "/billing/payment-method" : "/billing/payment-method";
        return $"""
            <section class="panel">
                <h2>Payment Method</h2>
                {paymentSummary}
                <p class="muted">Add Stripe and PayPal API keys in Railway to enable live checkout. Until then, payment details are saved locally only.</p>
                <form method="post" action="{action}" class="form">
                    {PaymentOptions.PaymentFieldsHtml(paymentValues, "billing-")}
                    <button class="button" type="submit">Save Payment Method</button>
                </form>
            </section>
            """;
    }

    public static string PlansSection(AppStoreDb store, string formAction, bool includePayment = false, PaymentMethodInput? paymentValues = null)
    {
        var cards = new StringBuilder();
        foreach (var plan in store.Plans)
        {
            var features = new StringBuilder();
            foreach (var feature in plan.Features)
                features.Append($"<li>{H.Encode(feature)}</li>");

            var checkoutButtons = store.IsBillingConfigured
                ? CheckoutButtons(plan.Id, store)
                : "";

            var paymentFields = includePayment && !store.IsBillingConfigured
                ? $"""
                    <div class="subscribe-payment">
                        <h3>Payment details</h3>
                        {PaymentOptions.PaymentFieldsHtml(paymentValues, $"plan-{plan.Id}-")}
                    </div>
                    """
                : "";

            var formInner = store.IsBillingConfigured
                ? checkoutButtons
                : $"""
                    <label>Account email
                        <input type="email" name="email" placeholder="you@example.com" required>
                    </label>
                    {paymentFields}
                    <button class="button" type="submit">Choose {H.Encode(plan.Name)}</button>
                    """;

            var formTag = store.IsBillingConfigured
                ? $"""<div class="plan-form form">{formInner}</div>"""
                : $"""<form method="post" action="{formAction}" class="plan-form form">{formInner}</form>""";

            cards.Append($"""
                <article class="panel plan-card">
                    <h2>{H.Encode(plan.Name)}</h2>
                    <p class="price">${plan.MonthlyFee:0.00}<span> USD/month</span></p>
                    <ul class="plan-features">{features}</ul>
                    {formTag}
                </article>
                """);
        }

        var billingNote = store.IsBillingConfigured
            ? """<p class="muted">Secure checkout via Stripe or PayPal. You will be redirected to complete payment.</p>"""
            : """<p class="muted">All prices in USD. Configure Stripe/PayPal API keys to enable live billing.</p>""";

        return $"""
            <section class="panel">
                <h2>Subscription Plans</h2>
                {billingNote}
            </section>
            <section class="choice-grid plans-grid">
                {cards}
            </section>
            """;
    }

    static string CheckoutButtons(string planId, AppStoreDb store)
    {
        var buttons = new StringBuilder();
        if (store.IsStripeConfigured)
        {
            buttons.Append($"""
                <form method="post" action="/subscription/stripe" class="inline-form">
                    <input type="hidden" name="plan" value="{H.Encode(planId)}">
                    <button class="button" type="submit">Pay with Card (Stripe)</button>
                </form>
                """);
        }
        if (store.IsPayPalConfigured)
        {
            buttons.Append($"""
                <form method="post" action="/subscription/paypal" class="inline-form">
                    <input type="hidden" name="plan" value="{H.Encode(planId)}">
                    <button class="button secondary" type="submit">Pay with PayPal</button>
                </form>
                """);
        }
        return buttons.Length > 0
            ? $"""<div class="checkout-buttons">{buttons}</div>"""
            : """<p class="muted">Billing is not fully configured yet.</p>""";
    }
}
