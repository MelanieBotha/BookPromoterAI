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

    public static string RenderPage(HttpContext http, string title, string body, AppStoreDb store, string? mainClass = null) =>
        Page(title, body, store, http, mainClass);

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
                    <a href="/community">Community</a>
                    <a href="/app-feedback">Reviews</a>
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
                <a href="/community">Community</a>
                <a href="/app-feedback">Reviews &amp; Forum</a>
                <a href="{BrandConstants.OfficialBlueskyUrl}" target="_blank" rel="noopener">@{BrandConstants.OfficialBlueskyHandle}</a>
                {CommunityFooterExtras(store)}
                <span>&copy; {DateTime.UtcNow.Year} {LegalConstants.ContactName}</span>
            </footer>
            {csrfScript}
        </body>
        </html>
        """;
    }

    // Generates the shared page shell with nav, banner, CSS.
    public static string Page(string title, string body, AppStoreDb store, HttpContext? http = null, string? mainClass = null)
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

        var pageClass = string.IsNullOrWhiteSpace(mainClass) ? "page" : $"page {Encode(mainClass)}";
        var currentPath = http?.Request.Path.Value ?? "";
        var sidebar = AppSidebar(store, currentPath);
        var sidebarScript = """
            <script>
            function toggleAppSidebar(e) {
                if (e) e.stopPropagation();
                document.body.classList.toggle('sidebar-open');
            }
            function closeAppSidebar() {
                document.body.classList.remove('sidebar-open');
            }
            </script>
            """;

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
        <body class="app-body">
            <div class="app-shell">
                {sidebar}
                <div class="app-main">
                    <header class="app-mobile-bar">
                        <button type="button" class="sidebar-toggle" onclick="toggleAppSidebar(event)" aria-label="Open menu">&#9776;</button>
                        <a class="app-mobile-brand" href="/dashboard">BookPromoter AI</a>
                    </header>
                    <main class="{pageClass}">
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
                        <a href="/community">Community</a>
                        <a href="{BrandConstants.OfficialBlueskyUrl}" target="_blank" rel="noopener">@{BrandConstants.OfficialBlueskyHandle}</a>
                        {CommunityFooterExtras(store)}
                        <span>&copy; {DateTime.UtcNow.Year} {LegalConstants.ContactName}</span>
                    </footer>
                </div>
            </div>
            <div class="sidebar-backdrop" onclick="closeAppSidebar()"></div>
            {sidebarScript}
            {csrfScript}
        </body>
        </html>
        """;
    }

    // Manager.io-style flat left sidebar — all sections visible, no dropdowns.
    static string AppSidebar(AppStoreDb store, string currentPath)
    {
        var billingHref = store.HasCustomerAccess ? "/billing" : "/subscription";
        var billingLabel = store.HasCustomerAccess ? "Billing" : "Subscribe";
        var bookCount = store.IsLoggedIn ? store.Books.Count : 0;
        var teamCount = store.IsLoggedIn && store.HasCustomerAccess ? store.TeamMembers.Count : 0;
        var clientCount = store.IsLoggedIn && store.HasCustomerAccess ? store.Clients.Count : 0;
        var videoCount = store.IsLoggedIn && store.HasCustomerAccess ? store.TikTokVideos.Count : 0;

        var sb = new StringBuilder();
        sb.Append("""
            <aside class="app-sidebar" id="app-sidebar">
                <a class="sidebar-brand" href="/dashboard">
                    <img src="/images/BookPromoterAI.logo.png" alt="BookPromoter AI" class="sidebar-logo">
                </a>
                <nav class="sidebar-nav">
            """);

        void Link(string href, string label, string icon, string match, int? count = null)
        {
            var active = NavActive(currentPath, match) ? " active" : "";
            var countHtml = count is int n
                ? $"""<span class="sidebar-count">{n}</span>"""
                : "";
            sb.Append($"""
                <a class="sidebar-link{active}" href="{href}">
                    <span class="sidebar-icon" aria-hidden="true">{icon}</span>
                    <span class="sidebar-label">{label}</span>
                    {countHtml}
                </a>
                """);
        }

        void Divider() => sb.Append("""<div class="sidebar-divider"></div>""");

        // Simple line-style SVG icons (Manager-like).
        static string Icon(string paths) =>
            $"""<svg class="sidebar-svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">{paths}</svg>""";

        var iDash = Icon("""<rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/>""");
        var iBook = Icon("""<path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>""");
        var iCal = Icon("""<rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>""");
        var iAds = Icon("""<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/>""");
        var iVid = Icon("""<rect x="2" y="6" width="14" height="12" rx="2"/><polygon points="16 10 22 7 22 17 16 14"/>""");
        var iMail = Icon("""<path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/>""");
        var iChart = Icon("""<line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/>""");
        var iTeam = Icon("""<path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/>""");
        var iClient = Icon("""<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>""");
        var iForum = Icon("""<path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>""");
        var iFeed = Icon("""<path d="M12 20h9"/><path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4Z"/>""");
        var iHelp = Icon("""<circle cx="12" cy="12" r="10"/><path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"/><line x1="12" y1="17" x2="12.01" y2="17"/>""");
        var iUser = Icon("""<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>""");
        var iBill = Icon("""<rect x="1" y="4" width="22" height="16" rx="2"/><line x1="1" y1="10" x2="23" y2="10"/>""");
        var iOwner = Icon("""<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"/>""");
        var iStart = Icon("""<polygon points="5 3 19 12 5 21 5 3"/>""");

        if (!store.HasCustomerAccess)
            Link("/start", "Start", iStart, "/start");

        Link("/dashboard", "Dashboard", iDash, "/dashboard");

        if (store.IsLoggedIn)
        {
            Link("/books", "Books", iBook, "/books", bookCount);
            Link("/schedule", "Schedule", iCal, "/schedule");
            Link("/ad-library", "Ad Library", iAds, "/ad-library");
            Link("/videos", "Videos", iVid, "/videos", store.HasCustomerAccess ? videoCount : null);
            Link("/mailing-list", "Mailing List", iMail, "/mailing-list");
            if (store.HasCustomerAccess)
                Link("/analytics", "Analytics", iChart, "/analytics");
        }

        if (store.HasCustomerAccess)
        {
            Divider();
            Link("/team", "Team", iTeam, "/team", teamCount);
            Link("/clients", "Clients", iClient, "/clients", clientCount);
        }

        Divider();
        Link("/app-feedback?tab=forum", "Forum", iForum, "/app-feedback");
        if (store.IsLoggedIn)
            Link("/feedback", "Feedback", iFeed, "/feedback");
        if (store.HasCustomerAccess)
            Link("/help", "Help", iHelp, "/help");

        Divider();
        if (store.IsLoggedIn)
        {
            Link("/my-account", "My Account", iUser, "/my-account");
            Link(billingHref, billingLabel, iBill, "/billing");
        }
        else
        {
            Link(billingHref, billingLabel, iBill, "/billing");
        }

        if (store.IsOwner)
        {
            Divider();
            Link("/owner-promos", "Owner", iOwner, "/owner");
        }

        sb.Append("""
                </nav>
            </aside>
            """);
        return sb.ToString();
    }

    static bool NavActive(string? path, string match)
    {
        path ??= "";
        var q = path.IndexOf('?');
        if (q >= 0) path = path[..q];
        path = path.TrimEnd('/');
        if (path.Length == 0) path = "/";

        match = match.TrimEnd('/');
        if (match.Equals("/dashboard", StringComparison.OrdinalIgnoreCase))
            return path.Equals("/dashboard", StringComparison.OrdinalIgnoreCase);
        if (match.Equals("/billing", StringComparison.OrdinalIgnoreCase))
            return path.StartsWith("/billing", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/subscription", StringComparison.OrdinalIgnoreCase);
        if (match.Equals("/owner", StringComparison.OrdinalIgnoreCase))
            return path.StartsWith("/owner", StringComparison.OrdinalIgnoreCase);
        if (match.Equals("/app-feedback", StringComparison.OrdinalIgnoreCase))
            return path.StartsWith("/app-feedback", StringComparison.OrdinalIgnoreCase);
        if (match.Equals("/videos", StringComparison.OrdinalIgnoreCase))
            return path.StartsWith("/videos", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/tiktok", StringComparison.OrdinalIgnoreCase);
        if (match.Equals("/feedback", StringComparison.OrdinalIgnoreCase))
            return path.Equals("/feedback", StringComparison.OrdinalIgnoreCase);

        return path.Equals(match, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(match + "/", StringComparison.OrdinalIgnoreCase);
    }

    static string CommunityFooterExtras(AppStoreDb store)
    {
        var baseUrl = store.Settings.PublicBaseUrl.TrimEnd('/');
        var links = CommunityLinks.RenderFooterLinks(store.GetBrandCommunityProfile(baseUrl));
        return string.IsNullOrEmpty(links) ? "" : links;
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
:root{color-scheme:light;--ink:#172033;--muted:#667085;--line:#d7dde8;--paper:#fff;--soft:#f4f7fb;--accent:#0f766e;--accent-dark:#115e59;--sidebar-w:240px;--nav-active-bg:#e6f4f2;--nav-active:#0f766e}
*{box-sizing:border-box}
body{margin:0;font-family:Arial,Helvetica,sans-serif;color:var(--ink);background:var(--soft)}
.app-body{min-height:100vh}
.app-shell{display:flex;min-height:100vh;align-items:stretch}
.app-sidebar{width:var(--sidebar-w);flex:0 0 var(--sidebar-w);background:var(--paper);border-right:1px solid var(--line);display:flex;flex-direction:column;position:sticky;top:0;height:100vh;overflow-y:auto;z-index:40}
.sidebar-brand{display:flex;align-items:center;justify-content:center;padding:16px 18px 12px;border-bottom:1px solid var(--line);text-decoration:none}
.sidebar-logo{height:56px;width:auto;display:block}
.sidebar-nav{display:flex;flex-direction:column;padding:10px 10px 24px;gap:2px}
.sidebar-link{display:flex;align-items:center;gap:10px;padding:9px 12px;border-radius:6px;color:var(--ink);text-decoration:none;font-size:14px;line-height:1.25}
.sidebar-link:hover{background:var(--soft)}
.sidebar-link.active{background:var(--nav-active-bg);color:var(--nav-active);font-weight:600}
.sidebar-icon{flex:0 0 20px;width:20px;height:20px;display:inline-flex;align-items:center;justify-content:center;color:var(--muted)}
.sidebar-link.active .sidebar-icon{color:var(--nav-active)}
.sidebar-svg{width:18px;height:18px;display:block}
.sidebar-label{flex:1 1 auto;min-width:0}
.sidebar-count{flex:0 0 auto;font-size:12px;color:var(--muted);font-weight:600;min-width:1.5em;text-align:right}
.sidebar-link.active .sidebar-count{color:var(--nav-active)}
.sidebar-divider{height:1px;background:var(--line);margin:8px 6px}
.app-main{flex:1 1 auto;min-width:0;display:flex;flex-direction:column}
.app-mobile-bar{display:none;align-items:center;gap:12px;padding:10px 14px;background:var(--paper);border-bottom:1px solid var(--line);position:sticky;top:0;z-index:30}
.sidebar-toggle{border:1px solid var(--line);background:var(--paper);border-radius:6px;width:40px;height:36px;font-size:20px;cursor:pointer;color:var(--ink);line-height:1}
.app-mobile-brand{font-weight:700;color:var(--ink);text-decoration:none;font-size:15px}
.sidebar-backdrop{display:none}
.topbar{min-height:72px;display:flex;align-items:center;justify-content:space-between;gap:18px;padding:14px 28px;background:var(--paper);border-bottom:1px solid var(--line)}
.brand{font-weight:700;color:var(--ink);text-decoration:none;display:flex;align-items:center}
.brand-logo{height:56px;width:auto;display:block}
.marketing-topbar .brand-logo{height:56px}
nav{display:flex;gap:18px;flex-wrap:wrap}
nav a{color:var(--muted);text-decoration:none;font-size:14px}
.page{max-width:1120px;width:100%;margin:0 auto;padding:28px}
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
.public-book-follow{margin-top:28px;padding-top:20px;border-top:1px solid var(--line)}
.public-book-follow .eyebrow{margin-bottom:4px}
.public-book-follow-links{margin-top:10px}
.public-book-author-cta{text-align:center;margin-top:24px}
.public-book-author-cta h2{margin:8px 0 12px;font-size:22px}
@media(max-width:700px){.public-book-hero{grid-template-columns:1fr}}
.post-card-cover{margin-bottom:10px}
.post-card-cover .book-cover.large,.post-card-cover .cover-placeholder.large{width:100%;height:180px}
.ad-week-collapsible{margin-bottom:12px;border-radius:8px;border:1px solid var(--line);background:var(--paper)}
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
.tiktok-video-preview{display:flex;flex-direction:column;align-items:center;gap:10px}
.tiktok-download-btn{margin-top:4px}
.tiktok-video-placeholder{min-height:200px;max-width:180px;display:grid;place-items:center;padding:16px;border-radius:8px;background:var(--soft);font-size:13px;text-align:center}
.tiktok-studio-layout{display:flex;flex-wrap:wrap;gap:24px;align-items:flex-start}
.tiktok-studio-controls{flex:1;min-width:260px}
.tiktok-studio-preview-wrap{text-align:center}
.tiktok-canvas{width:100%;max-width:270px;height:auto;border-radius:12px;background:#111;box-shadow:0 4px 24px rgba(0,0,0,.25)}
.tiktok-caption-preview{white-space:pre-wrap;margin:8px 0}
.post-card-header div{display:flex;flex-direction:column;gap:2px}
.copy-source{position:absolute;width:1px;height:1px;opacity:0;pointer-events:none;left:-9999px}
.post-card-actions{display:flex;gap:8px;margin-top:8px;flex-wrap:wrap}
.page-ad-library{max-width:none;width:100%;padding:24px clamp(20px,3vw,48px) 40px}
.page-ad-library .post-grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:16px}
.page-ad-library .post-card{display:flex;flex-direction:column;min-width:0;padding:16px}
.page-ad-library .post-card-cover{flex:0 0 auto}
.page-ad-library .post-card p{flex:1 1 auto;line-height:1.5;word-break:break-word;overflow-wrap:anywhere;font-size:14px}
.page-ad-library .post-card-actions{align-items:flex-start;margin-top:auto}
@media(max-width:1500px){.page-ad-library .post-grid{grid-template-columns:repeat(3,minmax(0,1fr))}}
@media(max-width:1050px){.page-ad-library .post-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:800px){.page-ad-library .post-grid{grid-template-columns:1fr}}
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
.status.available{background:#ccfbf1;color:#115e59}.status.pending{background:#fef3c7;color:#92400e}.status.used{background:#e5e7eb;color:#4b5563}
.promo-table{display:grid;border:1px solid var(--line);border-radius:8px;overflow:hidden}
.promo-header,.promo-row{display:grid;grid-template-columns:1.4fr 1.4fr 120px;gap:12px;align-items:center;padding:12px 14px}
.promo-table-actions .promo-header,.promo-table-actions .promo-row{grid-template-columns:1.1fr 1.4fr 90px 80px}
.brand-metrics-table .promo-header,.brand-metrics-table .promo-row{grid-template-columns:1.2fr 1.5fr 72px 72px 92px}
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
.owner-api-group{background:#fff}
.owner-api-summary{margin:0;padding:12px 20px;border-bottom:1px solid var(--line);font-size:14px}
.owner-api-group-inner{padding:10px 12px 14px}
.owner-collapsible-nested{margin-bottom:8px;border-radius:6px}
.owner-collapsible-nested .owner-collapsible-heading{background:#f5e082;font-size:14px;padding:10px 16px;font-weight:600}
.owner-api-badge{font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:0.04em;padding:2px 8px;border-radius:999px;margin-left:8px}
.owner-api-badge.ready{background:#166534;color:#fff}
.owner-api-badge.pending{background:#7f1d1d;color:#fff}
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
@media(max-width:900px){
.app-sidebar{position:fixed;left:0;top:0;transform:translateX(-105%);transition:transform .2s ease;box-shadow:none}
body.sidebar-open .app-sidebar{transform:translateX(0);box-shadow:8px 0 24px rgba(23,32,51,0.12)}
.app-mobile-bar{display:flex}
.sidebar-backdrop{display:none;position:fixed;inset:0;background:rgba(23,32,51,0.35);z-index:35}
body.sidebar-open .sidebar-backdrop{display:block}
.app-main .page{padding:20px 16px}
}
@media(max-width:800px){.hero,.topbar,.book-row{align-items:flex-start;flex-direction:column}.stats,.post-grid,.split,.schedule-row,.promo-header,.promo-row,.choice-grid,.plans-grid,.link-row,.bar-row{grid-template-columns:1fr}}
.app-footer{text-align:center;padding:18px 28px;border-top:1px solid var(--line);background:var(--paper);color:var(--muted);font-size:12px;display:flex;justify-content:center;gap:24px;margin-top:auto;flex-wrap:wrap}
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
