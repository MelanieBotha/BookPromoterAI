using System.Text;
namespace BookPromoterAI;

static class OwnerPage
{
    public static string Render(AppStoreDb store, string notice = "")
    {
        var accessRows = new StringBuilder();
        foreach (var code in store.PromoCodes.Where(c => !c.IsLifetimeFree))
        {
            var cls = code.IsRedeemed ? "used" : "available";
            accessRows.Append($"""
                <div class="promo-row">
                    <span>{H.Encode(code.Code)}</span>
                    <span>{H.Encode(code.IntendedRecipientEmail ?? "Any email")} &middot; {code.FreeTrialDays}-day access</span>
                    <span class="status {cls}">{(code.IsRedeemed ? "Used" : "Ready")}</span>
                </div>
                """);
        }
        if (!store.PromoCodes.Any(c => !c.IsLifetimeFree))
            accessRows.Append("""<p class="muted">No access codes yet. Click "Generate New Access Code" below.</p>""");

        var lifetimeRows = new StringBuilder();
        foreach (var code in store.PromoCodes.Where(c => c.IsLifetimeFree))
        {
            var cls = code.IsRedeemed ? "used" : "available";
            lifetimeRows.Append($"""
                <div class="promo-row">
                    <span>{H.Encode(code.Code)}</span>
                    <span>{H.Encode(code.IntendedRecipientEmail ?? "Any email")} &middot; Lifetime Free (Publisher)</span>
                    <span class="status {cls}">{(code.IsRedeemed ? "Used" : "Ready")}</span>
                </div>
                """);
        }
        if (!store.PromoCodes.Any(c => c.IsLifetimeFree))
            lifetimeRows.Append("""<p class="muted">No lifetime free codes yet. Click "Generate New Lifetime Code" below.</p>""");

        var planRows = new StringBuilder();
        foreach (var plan in store.Plans)
        {
            planRows.Append($"""
                <div class="promo-row plan-row">
                    <span>{H.Encode(plan.Name)}</span>
                    <span>
                        <form method="post" action="/owner/plan-price" class="inline-form tight">
                            <input type="hidden" name="planId" value="{H.Encode(plan.Id)}">
                            <label>Monthly Fee
                                <input name="monthlyFee" type="number" min="0" step="0.01" value="{plan.MonthlyFee:0.00}">
                            </label>
                            <button class="button small" type="submit">Save</button>
                        </form>
                    </span>
                    <span class="status available">{plan.BookLimitText} books / {plan.SocialAccountLimitText} accounts</span>
                </div>
                <div class="promo-row plan-row">
                    <span class="muted">{H.Encode(plan.Name)} payment IDs</span>
                    <span>
                        <form method="post" action="/owner/plan-payment-ids" class="inline-form tight">
                            <input type="hidden" name="planId" value="{H.Encode(plan.Id)}">
                            <label>Stripe Price ID
                                <input name="stripePriceId" value="{H.Encode(plan.StripePriceId ?? "")}" placeholder="price_... (optional)">
                            </label>
                            <button class="button small" type="submit">Save IDs</button>
                        </form>
                    </span>
                    <span></span>
                </div>
                """);
        }

        var stripeStatus = store.IsStripeConfigured ? "Connected" : "Not configured";
        var billingStatus = store.IsBillingConfigured
            ? $"""<p class="notice success">Stripe: {stripeStatus}. Customers can subscribe with card checkout.</p>"""
            : """<p class="notice error">Billing is not live yet. Add Stripe API keys in Railway (see setup steps below).</p>""";

        var payout = store.GetOwnerPayoutSettings();
        var payoutSummary = payout.IsConfigured
            ? $"""
                <p><strong>Account holder:</strong> {H.Encode(payout.AccountHolderName)}</p>
                <p><strong>Bank:</strong> {H.Encode(payout.BankName)} ({H.Encode(payout.AccountType)})</p>
                <p><strong>Account:</strong> {H.Encode(payout.AccountNumberMasked)}</p>
                {(string.IsNullOrWhiteSpace(payout.RoutingOrSortCode) ? "" : $"""<p><strong>Routing / sort code:</strong> {H.Encode(payout.RoutingOrSortCode)}</p>""")}
                {(string.IsNullOrWhiteSpace(payout.Iban) ? "" : $"""<p><strong>IBAN:</strong> {H.Encode(payout.Iban)}</p>""")}
                {(string.IsNullOrWhiteSpace(payout.Notes) ? "" : $"""<p><strong>Notes:</strong> {H.Encode(payout.Notes)}</p>""")}
                """
            : """<p class="muted">No payout bank account saved yet. Add your details below so you know where subscription revenue should go once real billing is connected.</p>""";

        var checkingSelected = payout.AccountType.Equals("Checking", StringComparison.OrdinalIgnoreCase) ? "selected" : "";
        var savingsSelected = payout.AccountType.Equals("Savings", StringComparison.OrdinalIgnoreCase) ? "selected" : "";
        var businessSelected = payout.AccountType.Equals("Business", StringComparison.OrdinalIgnoreCase) ? "selected" : "";

        return $"""
            <section class="panel">
                <h1>Owner</h1>
                <p class="muted">Owner settings for promo codes, plan prices, and Stripe billing. Only visible when you log in with the owner account.</p>
                <p class="muted small-text">App version <strong>v{AppVersion.Display}</strong></p>
            </section>

            {notice}

            <details class="owner-collapsible" open>
                <summary class="owner-collapsible-heading">Stripe Billing</summary>
                <div class="panel owner-settings">
                    {billingStatus}
                    <p class="muted">Add these <strong>Railway variables</strong> (Settings &rarr; Variables) to go live:</p>
                    <ul class="plan-features">
                        <li><code>Stripe__SecretKey</code>, <code>Stripe__PublishableKey</code>, <code>Stripe__WebhookSecret</code></li>
                    </ul>
                    <p class="muted"><strong>Stripe webhook URL:</strong> <code>https://bookpromoterai.us/webhooks/stripe</code> (events: checkout.session.completed, customer.subscription.updated, customer.subscription.deleted, invoice.payment_failed)</p>
                    <p class="muted">Stripe uses your plan price automatically if no Stripe Price ID is set. Payouts go to your Stripe balance, then your bank account linked in the Stripe dashboard.</p>
                </div>
            </details>

            <details class="owner-collapsible">
                <summary class="owner-collapsible-heading">Payout Bank Account</summary>
                <div class="panel owner-settings">
                    <p class="muted">Optional reference for where you want subscription revenue deposited. Stripe pays out to the bank account linked in your Stripe dashboard.</p>
                    {payoutSummary}
                    <form method="post" action="/owner/payout-settings" class="form">
                        <label>Account holder name
                            <input name="accountHolderName" value="{H.Encode(payout.AccountHolderName)}" required>
                        </label>
                        <label>Bank name
                            <input name="bankName" value="{H.Encode(payout.BankName)}" required>
                        </label>
                        <label>Account type
                            <select name="accountType">
                                <option value="Checking" {checkingSelected}>Checking</option>
                                <option value="Savings" {savingsSelected}>Savings</option>
                                <option value="Business" {businessSelected}>Business</option>
                            </select>
                        </label>
                        <label>Routing / sort code
                            <input name="routingOrSortCode" value="{H.Encode(payout.RoutingOrSortCode)}" placeholder="e.g. 021000021 or 20-00-00">
                        </label>
                        <label>Account number
                            <input name="accountNumber" value="{H.Encode(payout.AccountNumber)}" placeholder="Enter full account number" required>
                        </label>
                        <label>IBAN (optional, for international transfers)
                            <input name="iban" value="{H.Encode(payout.Iban)}" placeholder="e.g. GB29NWBK60161331926819">
                        </label>
                        <label>Notes (optional)
                            <textarea name="notes" placeholder="Any extra payout instructions">{H.Encode(payout.Notes)}</textarea>
                        </label>
                        <button class="button" type="submit">Save Payout Bank Account</button>
                    </form>
                </div>
            </details>

            <details class="owner-collapsible">
                <summary class="owner-collapsible-heading">Access Codes (30-Day Access)</summary>
                <div class="panel owner-settings">
                    <p class="muted">Codes are generated automatically. Each redeemed code is automatically replaced with a fresh one.</p>
                    <div class="promo-table">
                        <div class="promo-header">
                            <strong>Code</strong>
                            <strong>Assigned Email / Type</strong>
                            <strong>Status</strong>
                        </div>
                        {accessRows}
                    </div>
                    <form method="post" action="/owner/generate-access-code" class="inline-form">
                        <button class="button" type="submit">Generate New Access Code</button>
                    </form>
                </div>
            </details>

            <details class="owner-collapsible">
                <summary class="owner-collapsible-heading">Lifetime Free Codes (Publisher Tier)</summary>
                <div class="panel owner-settings">
                    <p class="muted">Grants permanent Publisher-tier access with no billing.</p>
                    <div class="promo-table">
                        <div class="promo-header">
                            <strong>Code</strong>
                            <strong>Assigned Email / Type</strong>
                            <strong>Status</strong>
                        </div>
                        {lifetimeRows}
                    </div>
                    <form method="post" action="/owner/generate-lifetime-code" class="inline-form">
                        <button class="button" type="submit">Generate New Lifetime Code</button>
                    </form>
                </div>
            </details>

            <details class="owner-collapsible">
                <summary class="owner-collapsible-heading">Subscription Plan Prices</summary>
                <div class="panel owner-settings">
                    <p class="muted">Change the monthly fee for each plan tier.</p>
                    <div class="promo-table">
                        <div class="promo-header">
                            <strong>Plan</strong>
                            <strong>Monthly Fee</strong>
                            <strong>Limits</strong>
                        </div>
                        {planRows}
                    </div>
                </div>
            </details>

            <details class="owner-collapsible">
                <summary class="owner-collapsible-heading">Feedback &amp; Suggestions Report</summary>
                <div>
                    {FeedbackLogSection(store)}
                </div>
            </details>
            """;
    }

    static string FeedbackLogSection(AppStoreDb store)
    {
        var categories = new[]
        {
            ("Bug Report",        "#c00000", "#ffffff"),
            ("Suggestion",        "#375623", "#ffffff"),
            ("Feature Request",   "#7030a0", "#ffffff"),
            ("General Feedback",  "#c55a11", "#ffffff"),
        };

        var sections = new StringBuilder();
        foreach (var (category, bgColor, textColor) in categories)
        {
            var entries = store.FeedbackEntries
                .Where(f => f.Category == category)
                .OrderByDescending(f => f.SubmittedAt)
                .ToList();

            var rows = new StringBuilder();
            foreach (var entry in entries)
            {
                var investigatedCheck = entry.Investigated
                    ? """<span class="feedback-tick">&#10003;</span>"""
                    : $"""<form method="post" action="/owner/feedback/investigate/{entry.Id}" style="display:inline"><button class="feedback-check-btn" type="submit" title="Mark as investigated">&#9744;</button></form>""";

                // Truncate thank-you email preview to first 120 chars
                var emailPreview = entry.ThankYouEmail.Length > 120
                    ? entry.ThankYouEmail[..120] + "..."
                    : entry.ThankYouEmail;

                rows.Append($"""
                    <tr class="{(entry.Investigated ? "investigated" : "")}">
                        <td>{entry.SubmittedAt:d/M/yyyy}</td>
                        <td>{H.Encode(entry.Category)}</td>
                        <td>{H.Encode(entry.Email)}</td>
                        <td>{H.Encode(entry.Message)}</td>
                        <td class="email-preview">{H.Encode(emailPreview)}</td>
                        <td class="center">{investigatedCheck}</td>
                    </tr>
                    """);
            }

            // Always show at least 3 empty rows like the spreadsheet
            var emptyRows = Math.Max(0, 3 - entries.Count);
            for (var i = 0; i < emptyRows; i++)
                rows.Append("""<tr><td></td><td></td><td></td><td></td><td></td><td class="center"><input type="checkbox" disabled></td></tr>""");

            sections.Append($"""
                <section class="panel owner-settings feedback-category-section">
                    <div class="feedback-category-header" style="background:{bgColor};color:{textColor}">
                        {H.Encode(category)}
                    </div>
                    <div class="feedback-table-wrapper">
                        <table class="feedback-tracker-table">
                            <thead>
                                <tr>
                                    <th>Date</th>
                                    <th>Category</th>
                                    <th>Email address</th>
                                    <th>Message</th>
                                    <th>Emailed Thank you</th>
                                    <th>Investigated</th>
                                </tr>
                            </thead>
                            <tbody>
                                {rows}
                            </tbody>
                        </table>
                    </div>
                </section>
                """);
        }

        return $"""
            {sections}
            """;
    }
}
