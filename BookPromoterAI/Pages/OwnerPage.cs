using System.Text;
namespace BookPromoterAI;

static class OwnerPage
{
    public static string Render(AppStoreDb store, string notice = "", string appBaseUrl = "", ReleaseNotesCatalog? releaseNotes = null, string? activeSection = null, string facebookDiagnosticsHtml = "")
    {
        if (string.IsNullOrWhiteSpace(appBaseUrl))
            appBaseUrl = "https://bookpromoterai.us";

        if (string.IsNullOrWhiteSpace(facebookDiagnosticsHtml))
            facebookDiagnosticsHtml = FacebookDiagnosticsHtml.RenderPanel(
                [],
                "/owner/facebook-diagnostics",
                "facebook-diagnostics",
                showAuthorAccountsOption: true);

        var open = (string id) => SectionOpen(id, activeSection);

        var (accessAvailable, accessRedeemed, accessRedeemedTotal) = store.GetAccessCodesForDisplay();
        var accessRows = new StringBuilder();
        foreach (var code in accessAvailable)
        {
            var assignee = string.IsNullOrWhiteSpace(code.IntendedRecipientEmail)
                ? "Unassigned"
                : H.Encode(code.IntendedRecipientEmail);
            accessRows.Append($"""
                <div class="promo-row">
                    <span>{H.Encode(code.Code)}</span>
                    <span>{assignee} &middot; {code.FreeTrialDays}-day access</span>
                    <span class="status available">Available</span>
                    <span>{DeletePromoButton(code, "access-codes")}</span>
                </div>
                """);
        }
        foreach (var code in accessRedeemed)
        {
            var redeemedInfo = !string.IsNullOrWhiteSpace(code.RedeemedByEmail)
                ? H.Encode(code.RedeemedByEmail)
                : H.Encode(code.IntendedRecipientEmail ?? "Unknown");
            var redeemedWhen = code.RedeemedAt is DateTime at ? at.ToString("MMM d, yyyy") : "";
            accessRows.Append($"""
                <div class="promo-row">
                    <span>{H.Encode(code.Code)}</span>
                    <span>{redeemedInfo} &middot; {code.FreeTrialDays}-day access{(string.IsNullOrEmpty(redeemedWhen) ? "" : $" &middot; {redeemedWhen}")}</span>
                    <span class="status used">Used</span>
                    <span>{DeletePromoButton(code, "access-codes")}</span>
                </div>
                """);
        }
        if (accessAvailable.Count == 0 && accessRedeemedTotal == 0)
            accessRows.Append("""<p class="muted">No access codes yet. Codes are created automatically when users sign up.</p>""");
        else if (accessRedeemedTotal > accessRedeemed.Count)
            accessRows.Append($"""<p class="muted small-text">Showing {accessRedeemed.Count} of {accessRedeemedTotal} redeemed access codes (most recent first).</p>""");

        var (lifetimeAvailable, lifetimeRedeemed, lifetimeRedeemedTotal) = store.GetLifetimeCodesForDisplay();
        var lifetimeRows = new StringBuilder();
        foreach (var code in lifetimeAvailable)
        {
            var assignee = string.IsNullOrWhiteSpace(code.IntendedRecipientEmail)
                ? "Unassigned"
                : H.Encode(code.IntendedRecipientEmail);
            lifetimeRows.Append($"""
                <div class="promo-row">
                    <span>{H.Encode(code.Code)}</span>
                    <span>{assignee} &middot; Lifetime Free (Publisher)</span>
                    <span class="status available">Available</span>
                    <span>{DeletePromoButton(code, "lifetime")}</span>
                </div>
                """);
        }
        foreach (var code in lifetimeRedeemed)
        {
            var redeemedInfo = !string.IsNullOrWhiteSpace(code.RedeemedByEmail)
                ? H.Encode(code.RedeemedByEmail)
                : H.Encode(code.IntendedRecipientEmail ?? "Unknown");
            var redeemedWhen = code.RedeemedAt is DateTime at ? at.ToString("MMM d, yyyy") : "";
            lifetimeRows.Append($"""
                <div class="promo-row">
                    <span>{H.Encode(code.Code)}</span>
                    <span>{redeemedInfo} &middot; Lifetime Free (Publisher){(string.IsNullOrEmpty(redeemedWhen) ? "" : $" &middot; {redeemedWhen}")}</span>
                    <span class="status used">Used</span>
                    <span>{DeletePromoButton(code, "lifetime")}</span>
                </div>
                """);
        }
        if (lifetimeAvailable.Count == 0 && lifetimeRedeemedTotal == 0)
            lifetimeRows.Append("""<p class="muted">No lifetime codes yet. Generate one below for beta authors.</p>""");
        else if (lifetimeRedeemedTotal > lifetimeRedeemed.Count)
            lifetimeRows.Append($"""<p class="muted small-text">Showing {lifetimeRedeemed.Count} of {lifetimeRedeemedTotal} redeemed lifetime codes (most recent first).</p>""");

        var tierSections = new StringBuilder();
        foreach (var plan in store.Plans.OrderBy(p => p.MonthlyFee))
            tierSections.Append(BuildPlanTierSection(store, plan, activeSection));

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
                {GoLiveItem(store.RailwayCleanupDone, "Railway cleanup: unused Postgres, Redis, and storage services removed")}
            </ul>
            """;

        return $"""
            <section class="panel">
                <h1>Owner</h1>
                <p class="muted">Owner settings for promo codes, plan prices, and Stripe billing. Only visible when you log in with the owner account.</p>
                <p class="muted small-text">App version <strong>v{AppVersion.Display}</strong></p>
            </section>

            {notice}

            <details class="owner-collapsible" id="owner-section-go-live"{open("go-live")}>
                <summary class="owner-collapsible-heading">Go Live Checklist</summary>
                <div class="panel owner-settings">
                    {goLiveChecklist}
                    {bannerStatus}
                </div>
            </details>

            <details class="owner-collapsible" id="owner-section-data-storage"{open("data-storage")}>
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

            <details class="owner-collapsible" id="owner-section-custom-domain"{open("custom-domain")}>
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

            <details class="owner-collapsible" id="owner-section-email"{open("email")}>
                <summary class="owner-collapsible-heading">Email (SendGrid)</summary>
                <div class="panel owner-settings">
                    {emailStatus}
                    <p class="muted">Add these <strong>Railway variables</strong> for password resets, access-code emails, team invites, and feedback thank-yous:</p>
                    <ul class="plan-features">
                        <li><code>SendGrid__ApiKey</code> (starts with <code>SG.</code>)</li>
                        <li><code>SendGrid__SenderEmail</code> (verified sender in SendGrid)</li>
                        <li><code>SendGrid__SenderName</code> (optional, e.g. Book Promoter AI)</li>
                    </ul>
                    <p class="muted">In SendGrid: Settings &rarr; Sender Authentication &rarr; verify <code>{LegalConstants.ContactEmail}</code> or <code>noreply@bookpromoterai.us</code> after DNS is connected.</p>
                    <p class="muted"><strong>Your action:</strong> Add the three Railway variables above, then Redeploy. Owner checklist will show green when SendGrid is connected.</p>
                </div>
            </details>

            <details class="owner-collapsible" id="owner-section-x-api"{open("x-api")}>
                <summary class="owner-collapsible-heading">X (Twitter) API</summary>
                <div class="panel owner-settings">
                    {(store.IsXConfigured
                        ? """<p class="notice success">X API: Connected. Authors and owner can use Sign in with X for live posting.</p>"""
                        : $"""
                            <p class="notice error">X is not configured yet. Authors cannot connect X until these Railway variables are set.</p>
                            <ul class="plan-features">
                                <li><strong>Client ID:</strong> {H.Encode(store.XClientIdStatus)}</li>
                                <li><strong>Client secret:</strong> {H.Encode(store.XClientSecretStatus)}</li>
                            </ul>
                            """)}
                    <p class="muted">Create a project at <a href="https://developer.x.com" target="_blank" rel="noopener">developer.x.com</a> with <strong>OAuth 2.0</strong> enabled and these settings:</p>
                    <ul class="plan-features">
                        <li><strong>Type:</strong> Web App, Confidential client</li>
                        <li><strong>Callback URL:</strong> <code>{H.Encode(XService.CallbackUrl(appBaseUrl.TrimEnd('/')))}</code></li>
                        <li><strong>Scopes:</strong> <code>{H.Encode(XService.Scopes)}</code></li>
                    </ul>
                    <p class="muted">Add Railway variables, then redeploy:</p>
                    <ul class="plan-features">
                        <li><code>X__ClientId</code></li>
                        <li><code>X__ClientSecret</code></li>
                    </ul>
                    <p class="muted">Your X developer account must have API access that allows posting (paid tier may be required). Test from <strong>My Account → Connect X</strong> after deploy.</p>
                </div>
            </details>

            <details class="owner-collapsible" id="owner-section-linkedin-api"{open("linkedin-api")}>
                <summary class="owner-collapsible-heading">LinkedIn API</summary>
                <div class="panel owner-settings">
                    {(store.IsLinkedInConfigured
                        ? """<p class="notice success">LinkedIn API: Connected. Authors and owner can use Sign in with LinkedIn for live posting.</p>"""
                        : $"""
                            <p class="notice error">LinkedIn is not configured yet. Authors cannot connect LinkedIn until these Railway variables are set.</p>
                            <ul class="plan-features">
                                <li><strong>Client ID:</strong> {H.Encode(store.LinkedInClientIdStatus)}</li>
                                <li><strong>Client secret:</strong> {H.Encode(store.LinkedInClientSecretStatus)}</li>
                            </ul>
                            """)}
                    <p class="muted">Create an app at <a href="https://www.linkedin.com/developers/apps" target="_blank" rel="noopener">linkedin.com/developers</a> and enable these products:</p>
                    <ul class="plan-features">
                        <li><strong>Sign In with LinkedIn using OpenID Connect</strong></li>
                        <li><strong>Share on LinkedIn</strong> (required for posting)</li>
                    </ul>
                    <p class="muted">Under <strong>Auth</strong> → <strong>OAuth 2.0 settings</strong>, add this redirect URL:</p>
                    <ul class="plan-features">
                        <li><code>{H.Encode(LinkedInService.CallbackUrl(appBaseUrl.TrimEnd('/')))}</code></li>
                    </ul>
                    <p class="muted">Requested scopes: <code>{H.Encode(LinkedInService.Scopes)}</code></p>
                    <p class="muted">Add Railway variables, then redeploy:</p>
                    <ul class="plan-features">
                        <li><code>LinkedIn__ClientId</code></li>
                        <li><code>LinkedIn__ClientSecret</code></li>
                    </ul>
                    <p class="muted">LinkedIn may require app review before <code>w_member_social</code> works in production. Test from <strong>My Account → Connect LinkedIn</strong> after deploy.</p>
                </div>
            </details>

            <details class="owner-collapsible" id="owner-section-facebook-api"{open("facebook-api")}>
                <summary class="owner-collapsible-heading">Facebook API</summary>
                <div class="panel owner-settings">
                    {(store.IsFacebookOAuthReady
                        ? $"""<p class="notice success">Facebook API: Ready ({(store.Settings.FacebookUsesConfigLogin ? "config_id" : "scope")} OAuth mode).</p>"""
                        : store.IsFacebookConfigured
                            ? $"""
                                <p class="notice error">Facebook credentials are set but OAuth is not ready.</p>
                                <ul class="plan-features">
                                    <li><strong>OAuth mode:</strong> {H.Encode(store.Settings.FacebookOAuthMode)} (scope = default; config needs Login Configuration ID)</li>
                                    <li><strong>Login config ID:</strong> {H.Encode(store.FacebookLoginConfigIdStatus)}</li>
                                </ul>
                                """
                            : $"""
                            <p class="notice error">Facebook is not configured yet. Authors cannot connect Facebook until these Railway variables are set.</p>
                            <ul class="plan-features">
                                <li><strong>App ID:</strong> {H.Encode(store.FacebookAppIdStatus)}</li>
                                <li><strong>App secret:</strong> {H.Encode(store.FacebookAppSecretStatus)}</li>
                                <li><strong>OAuth mode:</strong> scope (default) or config</li>
                            </ul>
                            """)}
                    <p class="muted">Create an app at <a href="https://developers.facebook.com" target="_blank" rel="noopener">developers.facebook.com</a> with use case <strong>Manage everything on your Page</strong>.</p>
                    <p class="muted"><strong>OAuth mode:</strong> Default <code>scope</code> uses Page permissions directly (recommended). Use <code>config</code> only if Login for Business configuration is fully set up.</p>
                    <ol class="plan-features">
                        <li>Railway: <code>Facebook__OAuthMode=scope</code> (default) or <code>config</code></li>
                        <li>Config mode only: Meta → <strong>Facebook Login for Business</strong> → <strong>Configurations</strong></li>
                        <li><strong>Token type:</strong> User access token</li>
                        <li><strong>Assets:</strong> Pages</li>
                        <li><strong>Permissions:</strong> {string.Join(", ", FacebookService.LoginConfigurationPermissions)} (each must be <em>Ready for testing</em> under Use cases → Customize)</li>
                        <li>Copy the <strong>Configuration ID</strong> → Railway variable <code>Facebook__LoginConfigId</code> (config mode only)</li>
                    </ol>
                    <ul class="plan-features">
                        <li><strong>Meta app name:</strong> use <em>AuthorPromoter AI</em> (Meta blocks &ldquo;Book&rdquo; in app names)</li>
                        <li><strong>App ID:</strong> {H.Encode(store.FacebookAppIdStatus)} (must be <code>1820670845576321</code> in Meta)</li>
                        <li><strong>Login config ID:</strong> {H.Encode(store.FacebookLoginConfigIdStatus)}</li>
                        <li><strong>Redirect URIs</strong> — OAuth uses the site URL you are browsing; add <em>all</em> of these in Meta under <strong>Facebook Login → Settings → Valid OAuth Redirect URIs</strong>:
                            <ul class="plan-features">
                                {string.Concat(PublicUrl.FacebookCallbackUrlsForMeta(store.Settings).Select(u => $"<li><code>{H.Encode(u)}</code></li>"))}
                            </ul>
                        </li>
                        <li><strong>App domains</strong> (Settings → Basic): <code>bookpromoterai.us</code></li>
                        <li><strong>Privacy policy URL</strong> (Settings → Basic): <code>https://bookpromoterai.us/privacy</code></li>
                    </ul>
                    <p class="muted"><strong>If Facebook shows &ldquo;Sorry, something went wrong&rdquo;</strong> (before any login screen), check in order:</p>
                    <ol class="plan-features">
                        <li><strong>App roles:</strong> App is Unpublished — only users listed under <strong>App roles → Administrators/Developers</strong> can connect. Add the Facebook account you use when authorizing (the one that admins the Book Promoter AI Page).</li>
                        <li><strong>Redirect URI:</strong> Paste <em>every</em> URI listed above into Meta, click <strong>Save changes</strong>, wait 2–3 minutes, retry in a private window.</li>
                        <li><strong>Go Live:</strong> Facebook Login for Business often fails in Development mode — switch app to <strong>Live</strong> in Meta (App Dashboard → publish) after basic settings are correct.</li>
                        <li><strong>Login configuration:</strong> Token type = <strong>User access token</strong> (not System user). Assets = <strong>Pages</strong>. Config ID in Railway must match Meta exactly.</li>
                        <li><strong>Permissions:</strong> Under <strong>Use cases → Customize</strong>, each permission must show <em>Ready for testing</em> (not &ldquo;Not added&rdquo;).</li>
                        <li><strong>Facebook account:</strong> Log into facebook.com as the Page admin with an app role — not a different personal account.</li>
                    </ol>
                    <p class="muted">Add Railway variables, then redeploy:</p>
                    <ul class="plan-features">
                        <li><code>Facebook__AppId</code></li>
                        <li><code>Facebook__AppSecret</code></li>
                        <li><code>Facebook__LoginConfigId</code></li>
                    </ul>
                    <p class="muted">App stays <strong>Unpublished</strong> for testing as admin. Test brand posting from <strong>Owner → Brand Social → Connect Facebook</strong>.</p>
                    <p class="muted"><strong>Login Configuration token type must be User access token</strong> (not System user). If Meta shows &ldquo;Continue as BookPromoter AI?&rdquo; you are logged into the business portfolio — log out and use your personal Facebook account (Melanie Botha).</p>
                    {facebookDiagnosticsHtml}
                </div>
            </details>

            <details class="owner-collapsible" id="owner-section-reddit-api"{open("reddit-api")}>
                <summary class="owner-collapsible-heading">Reddit API</summary>
                <div class="panel owner-settings">
                    {(store.IsRedditConfigured
                        ? """<p class="notice success">Reddit API: Ready.</p>"""
                        : $"""
                            <p class="notice error">Reddit is not configured yet.</p>
                            <ul class="plan-features">
                                <li><strong>Client ID:</strong> {H.Encode(store.RedditClientIdStatus)}</li>
                                <li><strong>Client secret:</strong> {H.Encode(store.RedditClientSecretStatus)}</li>
                            </ul>
                            """)}
                    <p class="muted">Create a <strong>web app</strong> at <a href="https://www.reddit.com/prefs/apps" target="_blank" rel="noopener">reddit.com/prefs/apps</a> (click <em>create another app...</em>).</p>
                    <p class="muted">Add this redirect URL in your Reddit app:</p>
                    <ul class="plan-features">
                        {string.Concat(PublicUrl.RedditCallbackUrlsForMeta(store.Settings).Select(u => $"<li><code>{H.Encode(u)}</code></li>"))}
                    </ul>
                    <p class="muted">Requested scopes: <code>{H.Encode(RedditService.Scopes)}</code></p>
                    <p class="muted">Add Railway variables, then redeploy:</p>
                    <ul class="plan-features">
                        <li><code>Reddit__ClientId</code></li>
                        <li><code>Reddit__ClientSecret</code></li>
                    </ul>
                    <p class="muted">Authors and owner pick a subreddit when connecting. The first line of each post becomes the Reddit title.</p>
                </div>
            </details>

            <details class="owner-collapsible" id="owner-section-tiktok-api"{open("tiktok-api")}>
                <summary class="owner-collapsible-heading">TikTok API</summary>
                <div class="panel owner-settings">
                    {(store.IsTikTokConfigured
                        ? """<p class="notice success">TikTok API: Ready.</p>"""
                        : $"""
                            <p class="notice error">TikTok is not configured yet.</p>
                            <ul class="plan-features">
                                <li><strong>Client key:</strong> {H.Encode(store.TikTokClientKeyStatus)}</li>
                                <li><strong>Client secret:</strong> {H.Encode(store.TikTokClientSecretStatus)}</li>
                            </ul>
                            """)}
                    <p class="muted">Register at <a href="https://developers.tiktok.com/" target="_blank" rel="noopener">developers.tiktok.com</a> and request <strong>Content Posting API</strong> scopes: <code>{H.Encode(TikTokService.Scopes)}</code>.</p>
                    <p class="muted">OAuth redirect URL:</p>
                    <ul class="plan-features">
                        <li><code>{H.Encode(string.IsNullOrWhiteSpace(appBaseUrl) ? $"https://bookpromoterai.us{TikTokService.CallbackPath}" : TikTokService.CallbackUrl(appBaseUrl))}</code></li>
                    </ul>
                    <p class="muted">Verify domain <code>bookpromoterai.us</code> in TikTok Developer Portal for video pull uploads. Add Railway variables:</p>
                    <ul class="plan-features">
                        <li><code>TikTok__ClientKey</code></li>
                        <li><code>TikTok__ClientSecret</code></li>
                    </ul>
                    <p class="muted">Authors use the <strong>Videos</strong> tab to create book promo videos and download them for manual posting. Direct TikTok API posting is not enabled yet.</p>
                </div>
            </details>

            <details class="owner-collapsible" id="owner-section-railway-cleanup"{open("railway-cleanup")}>
                <summary class="owner-collapsible-heading">Railway Cleanup (Unused Services)</summary>
                <div class="panel owner-settings">
                    <p class="muted">The app uses SQLite on the BookPromoterAI volume — not Postgres or Redis. Delete these to simplify the project and avoid extra cost:</p>
                    <ol class="plan-features">
                        <li>On the Railway project canvas, right-click <strong>Postgres</strong> &rarr; <strong>Delete Service</strong></li>
                        <li>Right-click <strong>Redis</strong> &rarr; <strong>Delete Service</strong></li>
                        <li>Right-click empty <strong>storage</strong> &rarr; <strong>Delete Service</strong></li>
                    </ol>
                    <p class="muted">Keep only <strong>BookPromoterAI</strong> (with its <code>/data</code> volume).</p>
                    {(store.RailwayCleanupDone
                        ? """<p class="notice success">Railway cleanup marked complete.</p>"""
                        : """<p class="notice error">After deleting the extra services, add Railway variable <code>Launch__RailwayCleanupDone=true</code> and redeploy.</p>""")}
                </div>
            </details>

            <details class="owner-collapsible" id="owner-section-stripe"{open("stripe")}>
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

            <details class="owner-collapsible" id="owner-section-payout"{open("payout")}>
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

            <details class="owner-collapsible" id="owner-section-access-codes"{open("access-codes")}>
                <summary class="owner-collapsible-heading">Access Codes (30-Day Access)</summary>
                <div class="panel owner-settings">
                    <p class="muted">Available and active 30-day trial codes only. Users who upgrade to a paid or lifetime plan are removed from this list automatically.</p>
                    <div class="promo-table promo-table-actions">
                        <div class="promo-header">
                            <strong>Code</strong>
                            <strong>Assigned Email / Type</strong>
                            <strong>Status</strong>
                            <strong>Actions</strong>
                        </div>
                        {accessRows}
                    </div>
                </div>
            </details>

            <details class="owner-collapsible" id="owner-section-lifetime"{open("lifetime")}>
                <summary class="owner-collapsible-heading">Lifetime Free Codes (Publisher Tier)</summary>
                <div class="panel owner-settings">
                    <p class="muted">Available codes are ready to share. Redeemed codes show as Used. Delete removes the code and revokes access if it was redeemed.</p>
                    <div class="promo-table promo-table-actions">
                        <div class="promo-header">
                            <strong>Code</strong>
                            <strong>Assigned Email / Type</strong>
                            <strong>Status</strong>
                            <strong>Actions</strong>
                        </div>
                        {lifetimeRows}
                    </div>
                    <form method="post" action="/owner/generate-lifetime-code" class="inline-form">
                        <button class="button" type="submit">Generate New Lifetime Code</button>
                    </form>
                </div>
            </details>

            {tierSections}

            {PromoSection(store, appBaseUrl, releaseNotes, activeSection)}

            <details class="owner-collapsible" id="owner-section-feedback"{open("feedback")}>
                <summary class="owner-collapsible-heading">Feedback &amp; Suggestions Report</summary>
                <div>
                    {FeedbackLogSection(store)}
                </div>
            </details>

            {OwnerScrollScript}
            """;
    }

    static string SectionOpen(string sectionId, string? activeSection) =>
        string.Equals(sectionId, activeSection, StringComparison.OrdinalIgnoreCase) ? " open" : "";

    const string OwnerScrollScript = """
        <script>
        (function () {
            var KEY = 'ownerPageState';
            function saveState() {
                var open = [];
                document.querySelectorAll('details.owner-collapsible[open]').forEach(function (d) {
                    if (d.id) open.push(d.id);
                });
                sessionStorage.setItem(KEY, JSON.stringify({ open: open, scroll: window.scrollY }));
            }
            document.addEventListener('DOMContentLoaded', function () {
                var raw = sessionStorage.getItem(KEY);
                if (raw) {
                    sessionStorage.removeItem(KEY);
                    try {
                        var state = JSON.parse(raw);
                        if (state.open) {
                            state.open.forEach(function (id) {
                                var d = document.getElementById(id);
                                if (d) d.open = true;
                            });
                        }
                        if (typeof state.scroll === 'number') {
                            requestAnimationFrame(function () { window.scrollTo(0, state.scroll); });
                        }
                    } catch (e) {}
                }
                document.querySelectorAll('form').forEach(function (f) {
                    var action = f.getAttribute('action') || '';
                    if (action.indexOf('/owner') === -1 && action.indexOf('/social-accounts') === -1) return;
                    f.addEventListener('submit', saveState);
                });
            });
        })();
        </script>
        """;

    static string PromoSection(AppStoreDb store, string appBaseUrl, ReleaseNotesCatalog? releaseNotes, string? activeSection)
    {
        try
        {
            return OwnerPromoPage.Render(store, appBaseUrl, releaseNotes, activeSection);
        }
        catch (Exception ex)
        {
            return $"""
                <details class="owner-collapsible" id="owner-section-promote"{SectionOpen("promote", activeSection)}>
                    <summary class="owner-collapsible-heading">Promote BookPromoter AI (Social &amp; Email)</summary>
                    <div class="panel owner-settings">
                        <p class="notice error">Promotion tools could not load: {H.Encode(ex.Message)}. Other owner settings below still work. Redeploy v1.5.2 or contact support if this persists.</p>
                    </div>
                </details>
                """;
        }
    }

    static string BuildPlanTierSection(AppStoreDb store, SubscriptionPlan plan, string? activeSection)
    {
        var sectionId = $"tier-{plan.Id}";
        var (members, total) = store.GetPlanMembersForDisplay(plan.Id);
        var memberRows = new StringBuilder();
        foreach (var member in members)
        {
            var statusClass = member.IsCancelled ? "used" : "available";
            var statusText = member.IsCancelled ? "Cancelled" : "Active";
            var endsNote = member.AccessEndsAt is DateTime ends && member.IsCancelled
                ? $" &middot; ends {ends:MMM d, yyyy}"
                : "";
            memberRows.Append($"""
                <div class="promo-row">
                    <span>{H.Encode(member.Email)}</span>
                    <span>{H.Encode(member.AccessType)} &middot; {H.Encode(member.BillingLabel)}{endsNote}</span>
                    <span class="status {statusClass}">{statusText}</span>
                </div>
                """);
        }
        if (total == 0)
            memberRows.Append($"""<p class="muted">No active {H.Encode(plan.Name)} subscribers yet.</p>""");
        else if (total > members.Count)
            memberRows.Append($"""<p class="muted small-text">Showing {members.Count} of {total} {H.Encode(plan.Name)} subscribers (most recent first).</p>""");

        return $"""
            <details class="owner-collapsible" id="owner-section-{sectionId}"{SectionOpen(sectionId, activeSection)}>
                <summary class="owner-collapsible-heading">{H.Encode(plan.Name)} Tier (${plan.MonthlyFee:0.00}/mo)</summary>
                <div class="panel owner-settings">
                    <p class="muted">Active subscribers on the {H.Encode(plan.Name)} plan (up to {PromoConstants.MaxVisiblePromoCodes}). Free trials and lifetime members are listed in their own sections above. Limits: {H.Encode(plan.BookLimitText)} books / {H.Encode(plan.SocialAccountLimitText)} accounts.</p>
                    <div class="promo-table">
                        <div class="promo-header">
                            <strong>Email</strong>
                            <strong>Access / Billing</strong>
                            <strong>Status</strong>
                        </div>
                        {memberRows}
                    </div>
                    <div class="promo-table" style="margin-top:16px">
                        <div class="promo-row plan-row">
                            <span>Monthly fee</span>
                            <span>
                                <form method="post" action="/owner/plan-price" class="inline-form tight">
                                    <input type="hidden" name="planId" value="{H.Encode(plan.Id)}">
                                    <label>Monthly Fee
                                        <input name="monthlyFee" type="number" min="0" step="0.01" value="{plan.MonthlyFee:0.00}">
                                    </label>
                                    <button class="button small" type="submit">Save</button>
                                </form>
                            </span>
                            <span class="status available">{H.Encode(plan.AiPostsPerMonthText)} AI posts/mo</span>
                        </div>
                        <div class="promo-row plan-row">
                            <span>Stripe Price ID</span>
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
                    </div>
                </div>
            </details>
            """;
    }

    static string DeletePromoButton(PromoCode code, string section) => $"""
        <form method="post" action="/owner/promo-code/delete/{code.Id}" class="inline-form tight" onsubmit="return confirm('Delete {H.Encode(code.Code)}? This removes the code and revokes access if it was redeemed.');">
            <input type="hidden" name="section" value="{H.Encode(section)}">
            <button class="danger-button small" type="submit">Delete</button>
        </form>
        """;

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
