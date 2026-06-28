using System.Text;
namespace BookPromoterAI;

static class OwnerPage
{
    public static string Render(AppStoreDb store, string notice = "", string appBaseUrl = "https://bookpromoterai.us")
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
            : $"""
                <p class="notice error">Billing is not live yet. The app cannot read a valid <code>Stripe__SecretKey</code> from Railway.</p>
                <ul class="plan-features">
                    <li><strong>Secret key:</strong> {H.Encode(store.StripeSecretKeyStatus)}</li>
                    <li><strong>Publishable key:</strong> {H.Encode(store.StripePublishableKeyStatus)}</li>
                    <li><strong>Webhook secret:</strong> {H.Encode(store.StripeWebhookSecretStatus)}</li>
                </ul>
                <p class="muted">After fixing variables, click <strong>Redeploy</strong> in Railway so the app restarts with the new values.</p>
                """;

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

        var storageNotice = store.Database.UsesDataVolume
            ? $"""<p class="notice success">{H.Encode(store.Database.StatusSummary)}</p>"""
            : $"""<p class="notice error">{H.Encode(store.Database.StatusSummary)}</p>""";

        var emailStatus = store.IsSendGridConfigured
            ? """<p class="notice success">SendGrid: Connected. Password resets, access codes, and team invites send real email.</p>"""
            : $"""
                <p class="notice error">Email is not live yet. The app cannot send real emails without SendGrid.</p>
                <ul class="plan-features">
                    <li><strong>API key:</strong> {H.Encode(store.SendGridApiKeyStatus)}</li>
                    <li><strong>Sender email:</strong> {H.Encode(store.SendGridSenderEmailStatus)}</li>
                </ul>
                """;

        var domainStatus = store.UsesCustomDomain
            ? $"""<p class="notice success">{H.Encode(store.PublicBaseUrlStatus)}</p>"""
            : $"""<p class="notice error">{H.Encode(store.PublicBaseUrlStatus)}</p>""";

        var bannerStatus = store.ShowSoftLaunchBanner
            ? """<p class="notice error">Beta banner is still visible on the marketing site. Set <code>Launch__ShowBetaBanner=false</code> in Railway or redeploy v1.4.2+.</p>"""
            : """<p class="notice success">Beta banner is hidden — site shows as fully launched.</p>""";

        var goLiveChecklist = $"""
            <ul class="plan-features">
                {GoLiveItem(store.IsBillingConfigured, "Stripe billing connected")}
                {GoLiveItem(store.Database.UsesDataVolume, "Database on persistent /data volume")}
                {GoLiveItem(store.UsesCustomDomain, "App configured for bookpromoterai.us (DNS must point to Railway)")}
                {GoLiveItem(store.IsSendGridConfigured, "SendGrid email (password resets &amp; access codes)")}
                {GoLiveItem(!store.ShowSoftLaunchBanner, "Soft-launch beta banner hidden")}
                {GoLiveItem(false, "Railway cleanup: delete unused Postgres, Redis, and empty storage services (manual)")}
            </ul>
            """;

        return $"""
            <section class="panel">
                <h1>Owner</h1>
                <p class="muted">Owner settings for promo codes, plan prices, and Stripe billing. Only visible when you log in with the owner account.</p>
                <p class="muted small-text">App version <strong>v{AppVersion.Display}</strong></p>
            </section>

            {notice}

            <details class="owner-collapsible" open>
                <summary class="owner-collapsible-heading">Go Live Checklist</summary>
                <div class="panel owner-settings">
                    {goLiveChecklist}
                    {bannerStatus}
                </div>
            </details>

            <details class="owner-collapsible">
                <summary class="owner-collapsible-heading">Data Storage (Railway Volume)</summary>
                <div class="panel owner-settings">
                    {storageNotice}
                    <p class="muted"><strong>Database path:</strong> <code>{H.Encode(store.Database.Path)}</code></p>
                    <p class="muted">To persist customers, subscriptions, and access codes across redeploys:</p>
                    <ol class="plan-features">
                        <li>Railway &rarr; <strong>BookPromoterAI</strong> service &rarr; right-click &rarr; <strong>Add Volume</strong></li>
                        <li>Mount path: <code>/data</code></li>
                        <li>Variable (already in Dockerfile): <code>DATABASE_PATH=/data/bookpromoter.db</code></li>
                        <li>Redeploy the service</li>
                    </ol>
                    <p class="muted">If you had data before adding the volume, the app copies the old database into <code>/data</code> on first boot.</p>
                </div>
            </details>

            <details class="owner-collapsible">
                <summary class="owner-collapsible-heading">Custom Domain (bookpromoterai.us)</summary>
                <div class="panel owner-settings">
                    {domainStatus}
                    <p class="muted">Connect your domain so customers see <code>bookpromoterai.us</code> instead of the Railway URL.</p>
                    <ol class="plan-features">
                        <li>Railway &rarr; <strong>BookPromoterAI</strong> &rarr; <strong>Settings</strong> &rarr; <strong>Networking</strong> &rarr; <strong>Custom Domain</strong></li>
                        <li>Add <code>bookpromoterai.us</code> and <code>www.bookpromoterai.us</code></li>
                        <li>At your domain registrar, add the CNAME records Railway shows</li>
                        <li>When DNS is live, add Railway variable: <code>App__PublicBaseUrl=https://bookpromoterai.us</code></li>
                        <li>Redeploy. Stripe checkout already returns to the URL you browse; the custom domain is mainly for branding and email links.</li>
                    </ol>
                    <p class="muted">Stripe webhooks can stay on <code>https://bookpromoterai-production.up.railway.app/webhooks/stripe</code> — no change required.</p>
                    <p class="muted"><strong>Your action:</strong> Railway &rarr; BookPromoterAI &rarr; Settings &rarr; Networking &rarr; add custom domains, then add CNAME records at your .us registrar. Test by opening <a href="https://bookpromoterai.us" target="_blank" rel="noopener">https://bookpromoterai.us</a>.</p>
                </div>
            </details>

            <details class="owner-collapsible">
                <summary class="owner-collapsible-heading">Email (SendGrid)</summary>
                <div class="panel owner-settings">
                    {emailStatus}
                    <p class="muted">Add these <strong>Railway variables</strong> for password resets, access-code emails, team invites, and feedback thank-yous:</p>
                    <ul class="plan-features">
                        <li><code>SendGrid__ApiKey</code> (starts with <code>SG.</code>)</li>
                        <li><code>SendGrid__SenderEmail</code> (verified sender in SendGrid)</li>
                        <li><code>SendGrid__SenderName</code> (optional, e.g. Book Promoter AI)</li>
                    </ul>
                    <p class="muted">In SendGrid: Settings &rarr; Sender Authentication &rarr; verify <code>bothamelanief@gmail.com</code> or <code>noreply@bookpromoterai.us</code> after DNS is connected.</p>
                    <p class="muted"><strong>Your action:</strong> Add the three Railway variables above, then Redeploy. Owner checklist will show green when SendGrid is connected.</p>
                </div>
            </details>

            <details class="owner-collapsible">
                <summary class="owner-collapsible-heading">Railway Cleanup (Unused Services)</summary>
                <div class="panel owner-settings">
                    <p class="muted">The app uses SQLite on the BookPromoterAI volume — not Postgres or Redis. Delete these to simplify the project and avoid extra cost:</p>
                    <ol class="plan-features">
                        <li>On the Railway project canvas, right-click <strong>Postgres</strong> &rarr; <strong>Delete Service</strong></li>
                        <li>Right-click <strong>Redis</strong> &rarr; <strong>Delete Service</strong></li>
                        <li>Right-click empty <strong>storage</strong> &rarr; <strong>Delete Service</strong></li>
                    </ol>
                    <p class="muted">Keep only <strong>BookPromoterAI</strong> (with its <code>/data</code> volume).</p>
                </div>
            </details>

            <details class="owner-collapsible" open>
                <summary class="owner-collapsible-heading">Stripe Billing</summary>
                <div class="panel owner-settings">
                    {billingStatus}
                    <p class="muted">Add these <strong>Railway variables</strong> (Settings &rarr; Variables) to go live:</p>
                    <ul class="plan-features">
                        <li><code>Stripe__SecretKey</code>, <code>Stripe__PublishableKey</code>, <code>Stripe__WebhookSecret</code></li>
                    </ul>
                    <p class="muted"><strong>Stripe webhook URL:</strong> use your live app URL + <code>/webhooks/stripe</code> (example: <code>https://bookpromoterai-production.up.railway.app/webhooks/stripe</code>). Events: checkout.session.completed, customer.subscription.updated, customer.subscription.deleted, invoice.payment_failed</p>
                    <p class="muted">Stripe checkout returns to the same URL you use to browse the app. Until <code>bookpromoterai.us</code> DNS is connected, use <code>https://bookpromoterai-production.up.railway.app</code> or set <code>App__PublicBaseUrl</code> in Railway.</p>
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

            <details class="owner-collapsible" open>
                <summary class="owner-collapsible-heading">Subscription Plan Prices</summary>
                <div class="panel owner-settings">
                    <p class="muted">Production defaults: Starter $4.99, Professional $14.99, Publisher $29.99, Agency $49.99. Stripe Price ID is optional — leave blank to charge the Monthly Fee shown here.</p>
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

            {OwnerPromoPage.Render(store, appBaseUrl)}

            <details class="owner-collapsible">
                <summary class="owner-collapsible-heading">Feedback &amp; Suggestions Report</summary>
                <div>
                    {FeedbackLogSection(store)}
                </div>
            </details>
            """;
    }

    static string GoLiveItem(bool done, string text) =>
        $"""<li class="{(done ? "status available" : "muted")}">{(done ? "&#10003;" : "&#9744;")} {text}</li>""";

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
