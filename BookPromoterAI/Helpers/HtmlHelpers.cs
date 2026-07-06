using System.Text;
using Microsoft.AspNetCore.Antiforgery;
namespace BookPromoterAI;

static class H
{
    // HTML-encodes a string for safe output in HTML.
    public static string Encode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);

    // Trims a description to a maximum number of words.
    public static string LimitWords(string text, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= maxWords ? text.Trim() : string.Join(' ', words.Take(maxWords));
    }

    public static string RenderPage(HttpContext http, string title, string body, AppStoreDb store) =>
        Page(title, body, store, http);

    public static string RenderMarketingPage(HttpContext http, string title, string body, AppStoreDb store, string extraHead = "", string? metaDescription = null) =>
        MarketingPage(title, body, store, http, extraHead, metaDescription);

    // Public marketing site shell — slim nav, no account banner.
    public static string MarketingPage(string title, string body, AppStoreDb store, HttpContext? http = null, string extraHead = "", string? metaDescription = null)
    {
        var csrfMeta = "";
        var csrfScript = "";
        if (http is not null)
        {
            var antiforgery = http.RequestServices.GetRequiredService<IAntiforgery>();
            var tokens = antiforgery.GetAndStoreTokens(http);
            csrfMeta = $"""<meta name="csrf-field" content="{Encode(tokens.FormFieldName)}"><meta name="csrf-token" content="{Encode(tokens.RequestToken ?? "")}">""";
            csrfScript = """
                <script>
                (function () {
                    var field = document.querySelector('meta[name="csrf-field"]');
                    var token = document.querySelector('meta[name="csrf-token"]');
                    if (!field || !token) return;
                    document.querySelectorAll('form[method="post"]').forEach(function (form) {
                        if (form.querySelector('input[name="' + field.content + '"]')) return;
                        var input = document.createElement('input');
                        input.type = 'hidden';
                        input.name = field.content;
                        input.value = token.content;
                        form.appendChild(input);
                    });
                })();
                </script>
                """;
        }

        var softLaunchBanner = "";
        if (http is not null)
        {
            var settings = http.RequestServices.GetService<AppSettings>();
            if (settings?.ShowSoftLaunchBanner == true)
                softLaunchBanner = SoftLaunchBanner(settings);
        }

        var navActions = store.IsLoggedIn && store.HasCustomerAccess
            ? """<a class="button nav-cta" href="/dashboard">Go to Dashboard</a>"""
            : store.IsLoggedIn
                ? """<a class="button secondary" href="/start">Continue setup</a>"""
                : """<a href="/start">Log in</a><a class="button nav-cta" href="/start">Get started</a>""";

        var pageDescription = string.IsNullOrWhiteSpace(metaDescription)
            ? "BookPromoter AI helps authors promote books with AI-generated social posts, click tracking, and a weekly Ad Library."
            : metaDescription;

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="description" content="{Encode(pageDescription)}">
            {extraHead}
            {csrfMeta}
            <link rel="icon" type="image/png" href="/images/BookPromoterAI.logo.png">
            <title>{Encode(title)} - BookPromoter AI</title>
            <style>{Css}</style>
        </head>
        <body class="marketing-body">
            <header class="topbar marketing-topbar">
                <a class="brand" href="/"><img src="/images/BookPromoterAI.logo.png" alt="BookPromoter AI" class="brand-logo"></a>
                <nav class="marketing-nav">
                    <a href="/#features">Features</a>
                    <a href="/#pricing">Pricing</a>
                    <a href="/trial">Access code</a>
                    <a href="/terms">Terms</a>
                    <a href="/privacy">Privacy</a>
                    {navActions}
                </nav>
            </header>
            <main class="page marketing-page">
                {softLaunchBanner}
                {body}
            </main>
            <footer class="app-footer marketing-footer">
                <span>BookPromoter AI</span>
                <span>v{AppVersion.Display}</span>
                <a href="/terms">Terms &amp; Conditions</a>
                <a href="/privacy">Privacy Policy</a>
                <a href="/start">Sign in</a>
                <a href="/trial">Free access code</a>
                <a href="{BrandConstants.OfficialBlueskyUrl}" target="_blank" rel="noopener">@{BrandConstants.OfficialBlueskyHandle}</a>
                <span>&copy; {DateTime.UtcNow.Year} {LegalConstants.ContactName}</span>
            </footer>
            {csrfScript}
        </body>
        </html>
        """;
    }

    // Generates the shared page shell with nav, banner, CSS.
    public static string Page(string title, string body, AppStoreDb store, HttpContext? http = null)
    {
        var csrfMeta = "";
        var csrfScript = "";
        if (http is not null)
        {
            var antiforgery = http.RequestServices.GetRequiredService<IAntiforgery>();
            var tokens = antiforgery.GetAndStoreTokens(http);
            csrfMeta = $"""<meta name="csrf-field" content="{Encode(tokens.FormFieldName)}"><meta name="csrf-token" content="{Encode(tokens.RequestToken ?? "")}">""";
            csrfScript = """
                <script>
                (function () {
                    var field = document.querySelector('meta[name="csrf-field"]');
                    var token = document.querySelector('meta[name="csrf-token"]');
                    if (!field || !token) return;
                    document.querySelectorAll('form[method="post"]').forEach(function (form) {
                        if (form.querySelector('input[name="' + field.content + '"]')) return;
                        var input = document.createElement('input');
                        input.type = 'hidden';
                        input.name = field.content;
                        input.value = token.content;
                        form.appendChild(input);
                    });
                })();
                </script>
                """;
        }

        var softLaunchBanner = "";
        if (http is not null)
        {
            var settings = http.RequestServices.GetService<AppSettings>();
            if (settings?.ShowSoftLaunchBanner == true)
                softLaunchBanner = SoftLaunchBanner(settings);
        }

        var helpPanel = http is not null && store.HasCustomerAccess && !IsReaderFacingPage(http)
            ? HelpGuide.RenderPanel(store, http.Request.Path.Value)
            : "";

        var accountBanner = IsReaderFacingPage(http) ? "" : AccountBanner(store);

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            {csrfMeta}
            <link rel="icon" type="image/png" href="/images/BookPromoterAI.logo.png">
            <title>{Encode(title)} - BookPromoter AI</title>
            <style>{Css}</style>
        </head>
        <body>
            <header class="topbar">
                <a class="brand" href="/"><img src="/images/BookPromoterAI.logo.png" alt="BookPromoter AI" class="brand-logo"></a>
                <nav>
                    {(store.HasCustomerAccess ? "" : """<a href="/start">Start</a>""")}
                    {(store.HasCustomerAccess ? """<a href="/help">Help</a>""" : "")}
                    <a href="/dashboard">Dashboard</a>
                    {(store.HasCustomerAccess ? """<a href="/videos">Videos</a>""" : "")}
                    {ManageDropdown(store)}
                    {AccountDropdown(store)}
                    {(store.IsOwner ? """<a href="/owner-promos">Owner</a>""" : "")}
                </nav>
            </header>
            <main class="page">
                {softLaunchBanner}
                {accountBanner}
                {helpPanel}
                {body}
            </main>
            <footer class="app-footer">
                <span>BookPromoter AI</span>
                <span>v{AppVersion.Display}</span>
                <a href="/terms">Terms &amp; Conditions</a>
                <a href="/privacy">Privacy Policy</a>
                <a href="{BrandConstants.OfficialBlueskyUrl}" target="_blank" rel="noopener">@{BrandConstants.OfficialBlueskyHandle}</a>
                <span>&copy; {DateTime.UtcNow.Year} {LegalConstants.ContactName}</span>
            </footer>
            {csrfScript}
        </body>
        </html>
        """;
    }

    // Combined "Manage" nav item with a dropdown to Team and Clients.
    // Only renders if at least one of those sections is visible to the user.
    static string ManageDropdown(AppStoreDb store)
    {
        // Show Manage dropdown whenever the user has customer access —
        // trial users see all tabs (with upgrade prompts inside them),
        // and paid users see the tabs their plan unlocks.
        if (!store.HasCustomerAccess) return "";

        var menuHtml = """
            <div class="nav-dropdown">
                <button type="button" class="nav-dropdown-toggle" onclick="toggleManageMenu(event)">
                    Manage <span class="caret">&#9662;</span>
                </button>
                <div class="nav-dropdown-menu" id="manage-dropdown-menu">
                    <a href="/team">Team</a>
                    <a href="/clients">Clients</a>
                </div>
            </div>
            """;

        var script = """
            <script>
            function toggleManageMenu(e) {
                e.stopPropagation();
                var menu = document.getElementById('manage-dropdown-menu');
                menu.classList.toggle('open');
            }
            document.addEventListener('click', function (e) {
                var menu = document.getElementById('manage-dropdown-menu');
                if (menu && !menu.contains(e.target) && !e.target.closest('.nav-dropdown-toggle')) {
                    menu.classList.remove('open');
                }
            });
            </script>
            """;

        return menuHtml + script;
    }

    // Combined "My Account" nav item with a dropdown to the main app
    // sections plus the account/profile and billing pages.
    static string AccountDropdown(AppStoreDb store)
    {
        if (!store.IsLoggedIn)
        {
            return store.HasCustomerAccess
                ? """<a href="/billing">Subscription &amp; Billing</a>"""
                : """<a href="/subscription">Subscribe</a>""";
        }

        var billingLabel = store.HasCustomerAccess ? "Subscription &amp; Billing" : "Subscribe";
        var billingHref = store.HasCustomerAccess ? "/billing" : "/subscription";
        var analyticsLink = store.HasCustomerAccess ? """<a href="/analytics">Analytics</a>""" : "";

        var menuHtml = $"""
            <div class="nav-dropdown">
                <button type="button" class="nav-dropdown-toggle" onclick="toggleAccountMenu(event)">
                    My Account <span class="caret">&#9662;</span>
                </button>
                <div class="nav-dropdown-menu" id="account-dropdown-menu">
                    <a href="/books">Books</a>
                    <a href="/schedule">Schedule</a>
                    <a href="/ad-library">Ad Library</a>
                    <a href="/videos">Videos</a>
                    <a href="/mailing-list">Mailing List</a>
                    {analyticsLink}
                    <div class="nav-dropdown-divider"></div>
                    <a href="/my-account">My Account</a>
                    <a href="{billingHref}">{billingLabel}</a>
                    <a href="/help">Help guide</a>
                    <a href="/feedback">Feedback &amp; Suggestions</a>
                </div>
            </div>
            """;

        // Script is a plain (non-interpolated) raw string so the JS curly
        // braces aren't misread as C# interpolation holes.
        var script = """
            <script>
            function toggleAccountMenu(e) {
                e.stopPropagation();
                var menu = document.getElementById('account-dropdown-menu');
                menu.classList.toggle('open');
            }
            document.addEventListener('click', function (e) {
                var menu = document.getElementById('account-dropdown-menu');
                if (menu && !menu.contains(e.target) && !e.target.closest('.nav-dropdown-toggle')) {
                    menu.classList.remove('open');
                }
            });
            </script>
            """;

        return menuHtml + script;
    }

    static string SoftLaunchBanner(AppSettings? settings)
    {
        if (settings?.IsBillingConfigured == true)
        {
            return """
                <section class="soft-launch-banner" role="status">
                    <strong>Live billing</strong>
                    <span>Subscriptions are processed by Stripe. Copy posts from your Ad Library and paste them to social platforms. Automatic posting is coming soon.</span>
                </section>
                """;
        }

        return """
            <section class="soft-launch-banner" role="status">
                <strong>Soft launch</strong>
                <span>Copy posts from your Ad Library and paste them manually to social platforms. Add Stripe API keys in Railway to enable paid subscriptions.</span>
            </section>
            """;
    }

    static bool IsReaderFacingPage(HttpContext? http)
    {
        if (http is null) return false;
        var path = http.Request.Path.Value ?? "";
        return path.StartsWith("/readers/signup", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/readers/unsubscribe", StringComparison.OrdinalIgnoreCase);
    }

    static string AccountBanner(AppStoreDb store)
    {
        var statusClass = store.AccessType switch
        {
            "Free Trial" => "trial",
            "Lifetime Free (Publisher)" => "paid",
            _ when store.CurrentPlan is not null => "paid",
            _ => "locked"
        };

        var cancelledNotice = store.IsCancelled && store.SubscriptionEndsAt is DateTime endsAt
            ? $"""<p class="muted small-text">Your subscription is cancelled. Access continues until <strong>{endsAt:MMMM d, yyyy}</strong>.</p>"""
            : "";

        return $"""
            <section class="account-banner {statusClass}">
                <strong>{Encode(store.AccessStatusText)}</strong>
                {cancelledNotice}
            </section>
            """;
    }

    public const string Css = """
:root{color-scheme:light;--ink:#172033;--muted:#667085;--line:#d7dde8;--paper:#fff;--soft:#f4f7fb;--accent:#0f766e;--accent-dark:#115e59}
*{box-sizing:border-box}
body{margin:0;font-family:Arial,Helvetica,sans-serif;color:var(--ink);background:var(--soft)}
.topbar{min-height:196px;display:flex;align-items:center;justify-content:space-between;gap:18px;padding:14px 28px;background:var(--paper);border-bottom:1px solid var(--line)}
.brand{font-weight:700;color:var(--ink);text-decoration:none;display:flex;align-items:center}
.brand-logo{height:176px;width:auto;display:block}
nav{display:flex;gap:18px;flex-wrap:wrap}
nav a{color:var(--muted);text-decoration:none;font-size:14px}
.nav-dropdown{position:relative;display:inline-block}
.nav-dropdown-toggle{background:none;border:0;color:var(--muted);font-size:14px;font:inherit;cursor:pointer;padding:0;display:flex;align-items:center;gap:4px}
.nav-dropdown-toggle:hover{color:var(--ink)}
.nav-dropdown-toggle .caret{font-size:10px}
.nav-dropdown-menu{display:none;position:absolute;top:calc(100% + 8px);right:0;background:var(--paper);border:1px solid var(--line);border-radius:8px;box-shadow:0 8px 24px rgba(23,32,51,0.12);min-width:200px;z-index:50;overflow:hidden}
.nav-dropdown-menu.open{display:block}
.nav-dropdown-menu a{display:block;padding:12px 16px;color:var(--ink);font-size:14px;text-decoration:none}
.nav-dropdown-menu a:hover{background:var(--soft)}
.nav-dropdown-divider{height:1px;background:var(--line);margin:4px 0}
.page{max-width:1120px;margin:0 auto;padding:28px}
.soft-launch-banner{display:flex;flex-wrap:wrap;gap:8px 12px;align-items:baseline;border-radius:8px;padding:12px 16px;margin-bottom:18px;border:1px solid #fde68a;background:#fefce8;color:#854d0e;font-size:14px;line-height:1.5}
.soft-launch-banner strong{font-weight:700}
.account-banner{border-radius:8px;padding:12px 16px;margin-bottom:18px;border:1px solid var(--line);font-weight:700}
.account-banner.locked{background:#fff7ed;color:#9a3412;border-color:#fed7aa}
.account-banner.trial{background:#ecfeff;color:#155e75;border-color:#67e8f9}
.account-banner.paid{background:#ecfdf5;color:#166534;border-color:#86efac}
.hero{display:flex;align-items:center;justify-content:space-between;gap:24px;padding:32px 0}
.eyebrow{margin:0 0 8px;color:var(--accent);font-weight:700;font-size:13px;text-transform:uppercase}
h1,h2{margin:0 0 16px}
h1{font-size:34px;max-width:680px}
h2{font-size:22px}
.button{display:inline-flex;align-items:center;justify-content:center;border:0;border-radius:6px;padding:11px 16px;background:var(--accent);color:white;font-weight:700;text-decoration:none;cursor:pointer;font:inherit;line-height:1.2;box-sizing:border-box}
.button:hover{background:var(--accent-dark)}
.button.secondary{background:var(--paper);color:var(--accent);border:1px solid var(--accent)}
.button.secondary:hover{background:var(--soft)}
.button.small{padding:7px 12px;font-size:13px}
.danger-button{display:inline-flex;align-items:center;justify-content:center;border:0;border-radius:6px;padding:11px 16px;background:#b91c1c;color:white;font-weight:700;cursor:pointer;font:inherit;line-height:1.2;box-sizing:border-box}
.danger-button:hover{background:#991b1b}
.danger-button.small{padding:7px 12px;font-size:13px}
.row-actions form{margin:0}
.stats{display:grid;grid-template-columns:repeat(4,1fr);gap:14px;margin-bottom:22px}
.book-stats-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:14px;margin-top:12px}
.book-stats-grid>div{background:var(--soft);border:1px solid var(--line);border-radius:8px;padding:16px}
.book-stats-grid>div span{display:block;font-size:32px;font-weight:700;color:var(--accent)}
.book-stats-grid>div small{display:block;font-weight:600;color:var(--ink);margin-top:4px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.book-stats-grid .stat-sub{font-size:11px;color:var(--muted);margin:2px 0 0}
.stat-empty{color:var(--muted);font-size:14px;padding:12px 0}
.stats div,.panel,.post-card{background:var(--paper);border:1px solid var(--line);border-radius:8px}
.stats div{padding:18px}.stats span{display:block;font-size:26px;font-weight:700}
.stats small,.muted,.book-row p,.book-row small{color:var(--muted)}
.panel{padding:22px;margin-bottom:18px}.post-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}
.post-card{padding:16px;scroll-margin-top:80px}.post-card-focused{outline:2px solid var(--accent);outline-offset:2px}.post-card p{white-space:pre-line}
.public-book-page{max-width:900px;margin:0 auto}
.public-book-hero{display:grid;grid-template-columns:220px 1fr;gap:24px;align-items:start}
.public-book-cover{width:100%;max-height:320px;object-fit:cover;border-radius:8px}
.public-book-description{line-height:1.6;margin:16px 0}
.public-book-buy{margin-top:8px}
.public-book-author-cta{text-align:center;margin-top:24px}
.public-book-author-cta h2{margin:8px 0 12px;font-size:22px}
@media(max-width:700px){.public-book-hero{grid-template-columns:1fr}}
.post-card-cover{margin-bottom:10px}
.post-card-cover .book-cover.large,.post-card-cover .cover-placeholder.large{width:100%;height:180px}
.ad-week-collapsible{margin-bottom:12px;border-radius:8px;overflow:hidden;border:1px solid var(--line);background:var(--panel)}
.ad-week-heading{display:flex;align-items:center;justify-content:space-between;gap:12px;padding:14px 18px;background:var(--soft);font-size:15px;font-weight:700;cursor:pointer;list-style:none;user-select:none}
.ad-week-heading::-webkit-details-marker{display:none}
.ad-week-heading::after{content:"▼";font-size:11px;color:var(--muted);transition:transform 0.2s;flex:0 0 auto}
details[open].ad-week-collapsible .ad-week-heading::after{transform:rotate(180deg)}
.ad-week-count{font-size:13px;font-weight:600;color:var(--muted)}
.ad-week-body{padding:14px 18px 18px}
.post-card-header{display:flex;align-items:center;justify-content:space-between;gap:10px;margin-bottom:8px}
.connect-buttons{display:flex;gap:10px;flex-wrap:wrap;margin:12px 0}
.button.platform-disabled,.platform-disabled{display:inline-block;opacity:0.45;cursor:not-allowed;filter:grayscale(1);background:#888!important}
.tiktok-player{max-width:180px;max-height:320px;border-radius:8px;background:#000}
.tiktok-video-row{align-items:center}
.tiktok-video-list{display:flex;flex-direction:column;gap:16px}
.tiktok-studio-layout{display:flex;flex-wrap:wrap;gap:24px;align-items:flex-start}
.tiktok-studio-controls{flex:1;min-width:260px}
.tiktok-studio-preview-wrap{text-align:center}
.tiktok-canvas{width:100%;max-width:270px;height:auto;border-radius:12px;background:#111;box-shadow:0 4px 24px rgba(0,0,0,.25)}
.tiktok-caption-preview{white-space:pre-wrap;margin:8px 0}
.post-card-header div{display:flex;flex-direction:column;gap:2px}
.copy-source{position:absolute;width:1px;height:1px;opacity:0;pointer-events:none;left:-9999px}
.post-card-actions{display:flex;gap:8px;margin-top:8px}
.copy-button.copied{background:var(--accent);color:white;border-color:var(--accent)}
.password-field{display:flex;gap:8px;align-items:center}
.password-field input{flex:1}
.show-password-btn{background:none;border:1px solid var(--line);border-radius:6px;padding:8px 12px;cursor:pointer;font:inherit;color:var(--muted);white-space:nowrap;flex-shrink:0}
.show-password-btn:hover{background:var(--soft);color:var(--ink)}
.hero-actions{display:flex;gap:10px;flex-wrap:wrap;align-items:center}
.author-heading{font-size:18px;margin:18px 0 8px;color:var(--ink);border-bottom:2px solid var(--accent);padding-bottom:6px}
.author-book-group{display:grid;gap:8px;margin-bottom:18px}
.platform-tag{display:inline-block;background:var(--soft);color:var(--accent);font-weight:700;font-size:12px;padding:3px 8px;border-radius:999px;margin:0 0 6px;width:fit-content}
.char-count{font-weight:500;color:var(--muted)}
.char-count-over{color:#b91c1c;font-weight:700}
.split{display:grid;grid-template-columns:380px 1fr;gap:18px;align-items:start}
.form{display:grid;gap:14px}
label:not(.checkbox-label){display:grid;gap:6px;color:var(--muted);font-size:14px}
label.sub-label{margin-top:6px}
input,textarea,select{width:100%;border:1px solid var(--line);border-radius:6px;padding:10px;font:inherit;color:var(--ink);background:white}
textarea{min-height:120px}
.book-row,.schedule-row{display:flex;align-items:center;justify-content:space-between;gap:18px;padding:14px 0;border-top:1px solid var(--line)}
.book-row>div{flex:1}
.book-cover,.cover-placeholder{width:74px;height:104px;border-radius:6px;object-fit:cover;border:1px solid var(--line);background:var(--soft);flex:0 0 auto}
.book-cover.small,.cover-placeholder.small{width:48px;height:68px}
.cover-placeholder{display:grid;place-items:center;color:var(--muted);font-size:12px;text-align:center;padding:8px}
.schedule-list{display:grid;gap:8px}.schedule-row{display:grid;grid-template-columns:160px 180px 1fr auto;gap:14px;align-items:center}
.account-schedule-row{display:grid;grid-template-columns:1fr 140px 180px auto;gap:14px;align-items:center;border-top:1px solid var(--line);padding:14px 0}
.account-schedule-row>div{display:flex;flex-direction:column;gap:2px}
.checkbox{display:flex;align-items:center;gap:8px}.checkbox input{width:auto}
.choice-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:18px}
.plans-grid{grid-template-columns:repeat(4,1fr)}
.plan-card{display:flex;flex-direction:column;gap:10px}
.plan-picker-btn{width:100%;text-align:center;margin-top:auto}
.checkout-layout{display:grid;grid-template-columns:minmax(260px,1fr) minmax(320px,1.25fr);gap:20px;max-width:920px;margin:0 auto 24px;align-items:start}
.checkout-summary{background:var(--soft);position:sticky;top:16px}
.checkout-eyebrow{font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:var(--muted);margin:0 0 4px}
.checkout-plan-name{margin:0 0 4px;font-size:26px}
.checkout-price{font-size:28px;font-weight:700;margin:0 0 12px}
.checkout-price span{font-size:14px;font-weight:500;color:var(--muted)}
.checkout-features{margin-bottom:12px}
.checkout-payment h2{margin-top:0}
.checkout-account{margin:0 0 16px;padding-bottom:12px;border-bottom:1px solid var(--line)}
.checkout-pay-form{margin:0 0 10px}
.checkout-pay-btn{width:100%;padding:14px 18px;font-size:16px;font-weight:600}
.checkout-secure{margin-top:8px}
.checkout-form .checkout-pay-btn{margin-top:8px}
.checkout-hero{margin-bottom:8px}
.plan-features{margin:0;padding-left:18px;color:var(--muted);font-size:14px;flex:1}
.plan-form{display:grid;gap:8px}
.price{font-size:36px;font-weight:800;margin:0 0 10px}.price span{font-size:18px;color:var(--muted);font-weight:400}
.notice{border-radius:6px;padding:12px;font-weight:700;margin-bottom:14px}
.notice.success{background:#dcfce7;color:#166534;border:1px solid #86efac}
.notice.error{background:#fee2e2;color:#991b1b;border:1px solid #fca5a5}
.status{border-radius:999px;padding:6px 10px;font-size:13px;font-weight:700}
.status.available{background:#ccfbf1;color:#115e59}.status.used{background:#e5e7eb;color:#4b5563}
.promo-table{display:grid;border:1px solid var(--line);border-radius:8px;overflow:hidden}
.promo-header,.promo-row{display:grid;grid-template-columns:1.4fr 1.4fr 120px;gap:12px;align-items:center;padding:12px 14px}
.promo-table-actions .promo-header,.promo-table-actions .promo-row{grid-template-columns:1.1fr 1.4fr 90px 80px}
.promo-row.plan-row{grid-template-columns:1fr 1.6fr 1fr}
.mailing-history-header,.mailing-history-row{grid-template-columns:1.2fr 2fr 1fr auto}
.email-body-view{margin-top:12px;padding:16px;background:var(--soft);border:1px solid var(--line);border-radius:8px;white-space:pre-line;line-height:1.6}
.promo-header{background:var(--soft)}.promo-row{border-top:1px solid var(--line)}
.post-preview{white-space:pre-wrap;font-size:13px;color:var(--ink);background:var(--soft);padding:10px;border-radius:8px;margin:0;max-height:160px;overflow:auto}
.promo-preview-with-image{display:flex;gap:12px;align-items:flex-start;margin-bottom:8px}
.promo-logo-thumb{width:72px;height:72px;object-fit:contain;border-radius:8px;background:#fff;border:1px solid var(--line);flex-shrink:0}
.promo-preview-with-image .post-preview{flex:1;min-width:0}
.checkbox-label{display:flex;align-items:flex-start;gap:10px;font-weight:500;margin:8px 0;max-width:100%}
.checkbox-label input[type=checkbox]{width:auto;min-width:16px;flex:0 0 auto;margin-top:2px}
.checkbox-label span{color:var(--muted);font-size:13px;line-height:1.5}
.feedback-table .feedback-header,.feedback-table .feedback-row{grid-template-columns:1.4fr 160px 2fr}
.feedback-row p{margin:0;font-size:13px;color:var(--ink)}
.feedback-category-section{padding:0;overflow:hidden;margin-bottom:24px}
.feedback-category-header{padding:10px 16px;font-weight:700;font-size:14px;text-align:center;letter-spacing:0.5px}
.feedback-table-wrapper{overflow-x:auto}
.feedback-tracker-table{width:100%;border-collapse:collapse;font-size:13px}
.feedback-tracker-table th{background:var(--soft);padding:8px 10px;text-align:left;border:1px solid var(--line);font-weight:600;white-space:nowrap}
.feedback-tracker-table td{padding:8px 10px;border:1px solid var(--line);vertical-align:top}
.feedback-tracker-table tr:hover td{background:#fafafa}
.feedback-tracker-table tr.investigated td{background:#f0fdf4;color:var(--muted)}
.feedback-tracker-table td.center{text-align:center;vertical-align:middle}
.feedback-tracker-table td.email-preview{font-size:12px;color:var(--muted);max-width:220px}
.feedback-tick{font-size:18px;color:#166534;font-weight:700}
.feedback-check-btn{background:none;border:none;font-size:16px;cursor:pointer;color:var(--muted);padding:0}
.owner-settings{margin-top:18px}.inline-form{display:flex;align-items:end;gap:14px;flex-wrap:wrap}.inline-form label{min-width:220px}
.owner-collapsible{margin-bottom:12px;border-radius:8px;overflow:hidden;border:1px solid var(--line)}
.owner-collapsible-heading{display:flex;align-items:center;justify-content:space-between;padding:14px 20px;background:#f0c800;color:#000;font-size:16px;font-weight:700;cursor:pointer;list-style:none;user-select:none}
.owner-collapsible-heading::-webkit-details-marker{display:none}
.owner-collapsible-heading::after{content:"▼";font-size:12px;transition:transform 0.2s}
details[open] .owner-collapsible-heading::after{transform:rotate(180deg)}
.owner-collapsible .panel{border:0;border-radius:0;margin-bottom:0}
.owner-collapsible .owner-settings{margin-top:0}
.inline-form.tight{gap:8px}.inline-form.tight label{min-width:120px}
.row-actions{display:flex;gap:8px;flex-direction:column}
.row-actions .button,.row-actions .danger-button{width:100%}
.link-badges{display:flex;gap:6px;flex-wrap:wrap;margin-top:6px}
.link-badge{background:var(--soft);color:var(--accent);font-size:12px;font-weight:700;padding:3px 8px;border-radius:999px}
.link-badge.muted-badge{color:var(--muted);background:transparent;border:1px solid var(--line)}
.link-list{display:grid;gap:8px;margin-bottom:8px}
.link-row{display:grid;grid-template-columns:1fr 1fr 1.6fr;gap:8px}
.link-row .custom-store{grid-column:1 / -1}
.cover-section{display:grid;gap:8px;border:1px solid var(--line);border-radius:8px;padding:12px;background:var(--soft)}
.inline-pair{display:flex;gap:8px}
.small-text{font-size:12px;margin:2px 0}
.form-actions{display:flex;gap:10px;align-items:center}
.remove-platform-form{display:flex;align-items:center}
.bar-chart{display:grid;gap:10px}
.analytics-summary-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:14px;margin-bottom:18px}
.analytics-card{background:var(--soft);border:1px solid var(--line);border-radius:8px;padding:16px;display:flex;flex-direction:column;gap:4px}
.analytics-card.posted{border-color:#86efac;background:#dcfce7}
.analytics-card.pending{border-color:#fde68a;background:#fefce8}
.analytics-card.failed{border-color:#fca5a5;background:#fee2e2}
.analytics-num{font-size:32px;font-weight:800;color:var(--accent)}
.analytics-label{font-size:12px;color:var(--muted);font-weight:600}
.analytics-performers{display:grid;grid-template-columns:1fr 1fr;gap:14px;margin-bottom:18px}
.analytics-performer-card{border-radius:8px;padding:20px;border:1px solid var(--line)}
.analytics-performer-card.top{background:#ecfdf5;border-color:#86efac}
.analytics-performer-card.lowest{background:#fff7ed;border-color:#fed7aa}
.analytics-performer-label{font-size:12px;font-weight:700;text-transform:uppercase;color:var(--muted);margin:0 0 6px}
.analytics-performer-book{font-size:20px;font-weight:700;margin:0 0 4px;color:var(--ink)}
.analytics-performer-stat{font-size:13px;color:var(--muted);margin:0}
.chart-wrap{display:flex;flex-direction:column;gap:12px}
.bar-svg{width:100%;max-width:700px;height:auto;display:block}
.chart-legend{display:flex;gap:12px;flex-wrap:wrap;margin-top:8px}
.chart-legend-item{display:flex;align-items:center;gap:6px;font-size:12px;color:var(--muted)}
.chart-legend-dot{width:12px;height:12px;border-radius:3px;flex-shrink:0}
.analytics-table-scroll{overflow-x:auto;margin-top:8px}
.analytics-month-table{width:100%;border-collapse:collapse;font-size:13px;min-width:500px}
.analytics-month-table th{background:var(--soft);padding:8px 10px;text-align:right;border:1px solid var(--line);font-weight:600;white-space:nowrap}
.analytics-month-table th:first-child{text-align:left}
.analytics-month-table td{padding:7px 10px;border:1px solid var(--line);text-align:right;color:var(--ink)}
.analytics-month-table td:first-child{text-align:left;font-weight:500}
.analytics-month-table tr.totals-row td{background:var(--soft);border-top:2px solid var(--accent)}
.analytics-month-table td.muted-cell{color:var(--muted)}
.analytics-locked-preview{position:relative;overflow:hidden}
.analytics-preview-blur{position:relative}
.analytics-blur-overlay{position:absolute;inset:0;background:rgba(255,255,255,0.75);backdrop-filter:blur(4px);display:flex;align-items:center;justify-content:center;border-radius:8px}
.search-bar{display:flex;gap:10px;align-items:center}
.search-bar input{flex:1}
.bar-row{display:grid;grid-template-columns:160px 1fr 60px;gap:10px;align-items:center}
.bar-track{background:var(--soft);border:1px solid var(--line);border-radius:6px;height:14px;overflow:hidden}
.bar-fill{background:var(--accent);height:100%;border-radius:6px}
.bar-fill.alt{background:#6366f1}
.bar-label{font-size:13px;color:var(--muted)}
.bar-value{font-size:13px;font-weight:700;text-align:right}
.oauth-panel{max-width:520px}
.oauth-platform-badge{width:64px;height:64px;border-radius:12px;display:flex;align-items:center;justify-content:center;color:white;font-size:22px;font-weight:900;margin-bottom:16px}
@media(max-width:1000px){.plans-grid{grid-template-columns:repeat(2,1fr)}.checkout-layout{grid-template-columns:1fr}.checkout-summary{position:static}}
@media(max-width:800px){.hero,.topbar,.book-row{align-items:flex-start;flex-direction:column}.stats,.post-grid,.split,.schedule-row,.promo-header,.promo-row,.choice-grid,.plans-grid,.link-row,.bar-row{grid-template-columns:1fr}}
.app-footer{text-align:center;padding:18px 28px;border-top:1px solid var(--line);background:var(--paper);color:var(--muted);font-size:12px;display:flex;justify-content:center;gap:24px;margin-top:32px;flex-wrap:wrap}
.marketing-body{background:linear-gradient(180deg,#f8fafc 0%,var(--soft) 240px)}
.marketing-topbar{background:rgba(255,255,255,0.95);backdrop-filter:blur(8px);position:sticky;top:0;z-index:20}
.marketing-nav{display:flex;align-items:center;gap:18px;flex-wrap:wrap}
.marketing-nav .nav-cta{padding:8px 16px;font-size:14px}
.marketing-page{max-width:1100px}
.landing-hero{display:grid;grid-template-columns:1.2fr 1fr;gap:28px;align-items:center;padding:28px 0 40px}
.landing-lead{font-size:18px;line-height:1.6;color:var(--muted);max-width:560px}
.landing-cta-row{display:flex;gap:12px;flex-wrap:wrap;margin:22px 0 12px}
.landing-checklist{margin:0;padding-left:18px;line-height:1.8}
.landing-section{margin:36px 0}
.landing-section-head{margin-bottom:22px}
.landing-section-head h2{margin:6px 0 8px;font-size:28px}
.landing-feature-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:16px}
.landing-feature-card h3{margin:8px 0 6px;font-size:17px}
.landing-feature-card p{margin:0;color:var(--muted);font-size:14px;line-height:1.55}
.landing-feature-icon{font-size:28px;display:block;margin-bottom:4px}
.landing-steps-list{display:grid;grid-template-columns:repeat(3,1fr);gap:20px;margin:0;padding:0;list-style:none;counter-reset:step}
.landing-steps-list li{display:flex;flex-direction:column;gap:6px;padding-left:44px;position:relative}
.landing-steps-list li::before{counter-increment:step;content:counter(step);position:absolute;left:0;top:0;width:32px;height:32px;border-radius:50%;background:var(--accent);color:#fff;display:flex;align-items:center;justify-content:center;font-weight:700;font-size:14px}
.landing-steps-list li span{color:var(--muted);font-size:14px;line-height:1.5}
.landing-plan-card .button{width:100%;text-align:center;margin-top:8px}
.landing-pricing-note{text-align:center;margin-top:16px}
.landing-final-cta{text-align:center;padding:32px 24px}
.landing-final-cta h2{margin-top:0}
.landing-final-cta .landing-cta-row{justify-content:center}
.marketing-footer a,.app-footer a{color:var(--accent);text-decoration:none;font-weight:600}
.legal-page{max-width:820px;margin:0 auto}
.legal-header{margin-bottom:28px}
.legal-intro{line-height:1.65;margin:16px 0 0}
.legal-section{margin-top:28px;padding-top:24px;border-top:1px solid var(--line)}
.legal-section:first-of-type{border-top:0;padding-top:0;margin-top:0}
.legal-section h2{font-size:18px;margin:0 0 12px;color:var(--ink)}
.legal-section p,.legal-section li{line-height:1.65;color:var(--ink);font-size:15px}
.legal-section ul{margin:12px 0;padding-left:22px}
.legal-section li{margin-bottom:8px}
.legal-footer-note{margin-top:32px;padding-top:20px;border-top:1px solid var(--line)}
.legal-accept-panel{max-width:720px}
.legal-accept-summary{margin:24px 0;background:var(--soft)}
.legal-accept-summary h2{font-size:17px;margin:0 0 12px}
.legal-accept-summary ul{margin:0;padding-left:22px;line-height:1.65}
.legal-accept-summary li{margin-bottom:8px}
.signup-legal-consent{margin-top:4px}
.legal-accept-checkbox a{font-weight:600}
.help-guide{border-left:4px solid var(--accent);margin-bottom:22px}
.help-guide-top{display:flex;flex-wrap:wrap;justify-content:space-between;align-items:flex-start;gap:12px;margin-bottom:8px}
.help-guide-title{margin:4px 0 0;font-size:20px}
.help-guide-list{margin:12px 0}
.help-guide-next{margin:12px 0 16px;font-size:14px;line-height:1.55}
.help-guide-actions{display:flex;flex-wrap:wrap;gap:10px;align-items:center}
.help-step-card{margin-bottom:18px}
.help-step-current{border-left:4px solid var(--accent)}
@media(max-width:900px){.landing-hero,.landing-feature-grid,.landing-steps-list{grid-template-columns:1fr}}
""";
}
