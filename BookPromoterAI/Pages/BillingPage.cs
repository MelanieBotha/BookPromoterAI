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

        var pm = store.CurrentPaymentMethod;
        var paymentSummary = pm is null
            ? """<p class="muted">No payment method on file yet.</p>"""
            : pm.IsBank
                ? $"""<p>Bank account on file: <strong>{H.Encode(pm.Summary)}</strong></p>"""
                : $"""<p>Card on file: <strong>{H.Encode(pm.CardholderName)}</strong> ending in <strong>{H.Encode(pm.Last4)}</strong>, expires {H.Encode(pm.Expiry)} &mdash; {H.Encode(pm.Country)}</p>""";

        var showPaymentForm = store.AccessType != "Lifetime Free (Publisher)" && store.AccessType != "Free Trial";
        paymentValues ??= pm is null
            ? null
            : new PaymentMethodInput(
                pm.PaymentType, "", pm.Region, pm.Country,
                pm.CardholderName, "", pm.Expiry,
                pm.BankName, pm.RoutingOrSortCode, pm.Iban, "");

        var paymentFormSection = showPaymentForm ? $"""
            <section class="panel">
                <h2>Payment Method</h2>
                {paymentSummary}
                <p class="muted">Accepts international cards and bank accounts from any country. No real charge is processed in this prototype.</p>
                <form method="post" action="/billing/payment-method" class="form">
                    {PaymentOptions.PaymentFieldsHtml(paymentValues, "billing-")}
                    <button class="button" type="submit">Save Payment Method</button>
                </form>
            </section>
            """ : """
            <section class="panel">
                <h2>Payment Method</h2>
                <p class="muted">No payment method needed for this plan.</p>
            </section>
            """;

        var plansSection = PlansSection(store, "/subscription/change", includePayment: false);

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Subscription &amp; Billing</p>
                    <h1>Manage your plan and payment details.</h1>
                    <p class="muted">Available worldwide — pay by card or bank account from any country.</p>
                </div>
            </section>

            {notice}

            <section class="panel">
                <h2>Current Plan: {H.Encode(planName)}</h2>
                {planDetails}
            </section>

            {paymentFormSection}

            <section class="panel">
                <h2>Change Plan</h2>
                <p class="muted">Pick a different plan below to switch at any time.</p>
            </section>
            {plansSection}

            <section class="panel">
                <h2>What You Get</h2>
                <p>Add books, create social media posts, set posting schedules, and track link clicks. Higher tiers unlock more books, more connected accounts, and more AI posts per month.</p>
            </section>
            """;
    }

    public static string SubscribePage(AppStoreDb store, string notice, PaymentMethodInput? paymentValues = null)
    {
        var plansSection = PlansSection(store, "/subscription", includePayment: true, paymentValues: paymentValues);
        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Plans &amp; Pricing</p>
                    <h1>Choose the plan that matches how many books you're promoting.</h1>
                    <p class="muted">Subscribe from anywhere. Pay by international card or bank transfer — all countries and regions welcome.</p>
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

    public static string PlansSection(AppStoreDb store, string formAction, bool includePayment = false, PaymentMethodInput? paymentValues = null)
    {
        var cards = new StringBuilder();
        foreach (var plan in store.Plans)
        {
            var features = new StringBuilder();
            foreach (var feature in plan.Features)
                features.Append($"<li>{H.Encode(feature)}</li>");

            var paymentFields = includePayment
                ? $"""
                    <div class="subscribe-payment">
                        <h3>Payment details</h3>
                        {PaymentOptions.PaymentFieldsHtml(paymentValues, $"plan-{plan.Id}-")}
                    </div>
                    """
                : "";

            cards.Append($"""
                <article class="panel plan-card">
                    <h2>{H.Encode(plan.Name)}</h2>
                    <p class="price">${plan.MonthlyFee:0.00}<span> USD/month</span></p>
                    <ul class="plan-features">{features}</ul>
                    <form method="post" action="{formAction}" class="plan-form form">
                        <input type="hidden" name="plan" value="{H.Encode(plan.Id)}">
                        <label>Account email
                            <input type="email" name="email" placeholder="you@example.com" required>
                        </label>
                        {paymentFields}
                        <button class="button" type="submit">Choose {H.Encode(plan.Name)}</button>
                    </form>
                </article>
                """);
        }

        return $"""
            <section class="panel">
                <h2>Subscription Plans</h2>
                <p class="muted">All prices in USD. Payment accepted from any country via card or bank account.</p>
            </section>
            <section class="choice-grid plans-grid">
                {cards}
            </section>
            """;
    }
}
