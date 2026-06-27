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

        var paymentFormSection = PaymentSection(store, paymentValues);
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
            : PlanPicker(store, "/subscription/change", changeMode: true);

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

    public static string SubscribePage(AppStoreDb store, string notice) =>
        $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Plans &amp; Pricing</p>
                    <h1>Choose the plan that matches how many books you're promoting.</h1>
                    <p class="muted">Select a plan to continue to secure checkout. All prices in USD.</p>
                </div>
            </section>
            {notice}
            {PlanPicker(store, "/subscription/checkout", changeMode: false)}
            <section class="panel">
                <h2>What You Get</h2>
                <p>Add books, create social media posts, set posting schedules, and track link clicks.</p>
            </section>
            """;

    public static string CheckoutPage(AppStoreDb store, string planId, string notice, PaymentMethodInput? paymentValues = null)
    {
        var plan = store.Plans.FirstOrDefault(p => p.Id == planId);
        if (plan is null)
            return SubscribePage(store, """<div class="notice error">That plan is not available. Please choose again.</div>""");

        var features = new StringBuilder();
        foreach (var feature in plan.Features)
            features.Append($"<li>{H.Encode(feature)}</li>");

        var email = store.LoggedInEmail ?? "";
        var paymentBlock = CheckoutPaymentBlock(store, plan.Id, email, paymentValues);

        return $"""
            <section class="hero checkout-hero">
                <div>
                    <p class="eyebrow">Checkout</p>
                    <h1>Complete your subscription</h1>
                    <p class="muted"><a href="/subscription">&larr; Change plan</a></p>
                </div>
            </section>

            {notice}

            <div class="checkout-layout">
                <aside class="panel checkout-summary">
                    <p class="checkout-eyebrow">Your plan</p>
                    <h2 class="checkout-plan-name">{H.Encode(plan.Name)}</h2>
                    <p class="checkout-price">${plan.MonthlyFee:0.00} <span>USD / month</span></p>
                    <ul class="plan-features checkout-features">{features}</ul>
                    <p class="muted small-text">Billed monthly. Cancel anytime from Subscription &amp; Billing.</p>
                </aside>

                <section class="panel checkout-payment">
                    <h2>Payment</h2>
                    <p class="muted checkout-account">Account: <strong>{H.Encode(email)}</strong></p>
                    {paymentBlock}
                </section>
            </div>
            """;
    }

    static string CheckoutPaymentBlock(AppStoreDb store, string planId, string email, PaymentMethodInput? paymentValues)
    {
        if (store.IsBillingConfigured)
        {
            var buttons = new StringBuilder();
            if (store.IsStripeConfigured)
            {
                buttons.Append($"""
                    <form method="post" action="/subscription/stripe" class="checkout-pay-form">
                        <input type="hidden" name="plan" value="{H.Encode(planId)}">
                        <button class="button checkout-pay-btn" type="submit">Pay with card</button>
                    </form>
                    <p class="muted small-text checkout-secure">Secure checkout powered by Stripe. Card details are entered on Stripe's site.</p>
                    """);
            }
            if (store.IsPayPalConfigured)
            {
                buttons.Append($"""
                    <form method="post" action="/subscription/paypal" class="checkout-pay-form">
                        <input type="hidden" name="plan" value="{H.Encode(planId)}">
                        <button class="button secondary checkout-pay-btn" type="submit">Pay with PayPal</button>
                    </form>
                    """);
            }
            if (buttons.Length == 0)
                return """<p class="notice error">Billing is not fully configured yet.</p>""";
            return buttons.ToString();
        }

        return $"""
            <p class="muted">Prices are in USD. Payment details are saved when you subscribe.</p>
            <form method="post" action="/subscription" class="form checkout-form">
                <input type="hidden" name="plan" value="{H.Encode(planId)}">
                <label>Account email
                    <input type="email" name="email" value="{H.Encode(email)}" required>
                </label>
                {PaymentOptions.PaymentFieldsHtml(paymentValues, "checkout-")}
                <button class="button checkout-pay-btn" type="submit">Subscribe to {H.Encode(store.Plans.First(p => p.Id == planId).Name)}</button>
            </form>
            """;
    }

    public static string PlanPicker(AppStoreDb store, string selectHref, bool changeMode)
    {
        var cards = new StringBuilder();
        foreach (var plan in store.Plans)
        {
            var features = new StringBuilder();
            foreach (var feature in plan.Features)
                features.Append($"<li>{H.Encode(feature)}</li>");

            var action = changeMode
                ? $"""<form method="post" action="/subscription/change" class="plan-picker-action"><input type="hidden" name="plan" value="{H.Encode(plan.Id)}"><button class="button plan-picker-btn" type="submit">Switch to {H.Encode(plan.Name)}</button></form>"""
                : $"""<a class="button plan-picker-btn" href="{selectHref}?plan={Uri.EscapeDataString(plan.Id)}">Choose {H.Encode(plan.Name)}</a>""";

            cards.Append($"""
                <article class="panel plan-card plan-picker-card">
                    <h2>{H.Encode(plan.Name)}</h2>
                    <p class="price">${plan.MonthlyFee:0.00}<span> USD/month</span></p>
                    <ul class="plan-features">{features}</ul>
                    {action}
                </article>
                """);
        }

        return $"""
            <section class="panel">
                <h2>Subscription Plans</h2>
                <p class="muted">{(changeMode ? "Select a new plan." : "Pick one plan — you'll complete payment on the next step.")}</p>
            </section>
            <section class="choice-grid plans-grid plan-picker-grid">
                {cards}
            </section>
            """;
    }

    static string PaymentSection(AppStoreDb store, PaymentMethodInput? paymentValues)
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
        paymentValues ??= pm is null
            ? null
            : new PaymentMethodInput(
                pm.PaymentType, "", pm.Region, pm.Country,
                pm.CardholderName, "", pm.Expiry,
                pm.BankName, pm.RoutingOrSortCode, pm.Iban, "");

        return $"""
            <section class="panel">
                <h2>Payment Method</h2>
                <form method="post" action="/billing/payment-method" class="form">
                    {PaymentOptions.PaymentFieldsHtml(paymentValues, "billing-")}
                    <button class="button" type="submit">Save Payment Method</button>
                </form>
            </section>
            """;
    }
}
