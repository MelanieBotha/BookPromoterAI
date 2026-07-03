using System.Text;

namespace BookPromoterAI;

static class SocialConnectHelper
{
    public const string OwnerReturnPath = "/owner-promos?section=owner-social";

    public static readonly string[] DefaultPlatforms =
        ["Facebook", "X", "Instagram", "LinkedIn", "Pinterest", "TikTok", "Bluesky"];

    public static readonly HashSet<string> DisabledPlatforms = new(StringComparer.OrdinalIgnoreCase) { "TikTok" };

    public static bool IsPlatformDisabled(string? platform) =>
        !string.IsNullOrWhiteSpace(platform) && DisabledPlatforms.Contains(platform.Trim());

    public static string DisabledPlatformLabel(string platform) => $"{platform} (video only — coming soon)";

    static readonly Dictionary<string, string> PlatformColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Facebook"] = "#1877F2",
        ["X"] = "#000000",
        ["Instagram"] = "#E4405F",
        ["LinkedIn"] = "#0A66C2",
        ["Pinterest"] = "#E60023",
        ["TikTok"] = "#000000",
        ["Bluesky"] = "#0085FF"
    };

    public static string ResolveReturnUrl(HttpRequest request, string? formReturn = null)
    {
        var value = !string.IsNullOrWhiteSpace(formReturn)
            ? formReturn
            : request.Query["return"].ToString();
        return IsAllowedReturnUrl(value) ? value : "/my-account";
    }

    public static bool IsAllowedReturnUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && (url == OwnerReturnPath || url.StartsWith("/owner-promos", StringComparison.OrdinalIgnoreCase) || url == "/my-account");

    public static string ResolveAccountKind(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith("/owner-promos", StringComparison.OrdinalIgnoreCase)
            ? SocialAccountKinds.Brand
            : SocialAccountKinds.Author;

    public static bool IsBrandContext(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith("/owner-promos", StringComparison.OrdinalIgnoreCase);

    public static string ConnectButtons(string returnUrl)
    {
        var buttons = new StringBuilder();
        foreach (var platform in DefaultPlatforms)
        {
            if (IsPlatformDisabled(platform))
            {
                buttons.Append($"""
                    <span class="button platform-disabled" title="TikTok requires video posts — not supported yet">{H.Encode(DisabledPlatformLabel(platform))}</span>
                    """);
                continue;
            }
            var color = PlatformColors.TryGetValue(platform, out var c) ? c : "#0f766e";
            var href = $"/social-accounts/connect/{Uri.EscapeDataString(platform)}?return={Uri.EscapeDataString(returnUrl)}";
            buttons.Append($"""
                <a class="button" href="{href}" style="background:{color}">
                    Connect {H.Encode(platform)}
                </a>
                """);
        }
        return buttons.ToString();
    }

    public static string RenderPlatformOption(string value, bool selected = false)
    {
        if (IsPlatformDisabled(value))
            return $"""<option value="" disabled>{H.Encode(DisabledPlatformLabel(value))}</option>""";
        var sel = selected ? " selected" : "";
        return $"""<option value="{H.Encode(value)}"{sel}>{H.Encode(value)}</option>""";
    }

    public static string OAuthAuthorizePage(string platformName, string returnUrl, string notice = "")
    {
        var brandContext = IsBrandContext(returnUrl);
        if (PostLimits.IsBluesky(platformName))
            return BlueskyConnectPage(returnUrl, notice, brandContext);

        if (PostLimits.IsX(platformName))
            return XSetupPage(returnUrl, notice, null);

        if (PostLimits.IsLinkedIn(platformName))
            return LinkedInSetupPage(returnUrl, notice, null);

        if (PostLimits.IsFacebook(platformName))
            return FacebookSetupPage(returnUrl, notice, null);

        if (IsPlatformDisabled(platformName))
        {
            return $"""
                <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>{H.Encode(DisabledPlatformLabel(platformName))}</h1></div></section>
                <section class="panel">
                    <p class="notice error">TikTok requires video content. BookPromoter AI text/image posts are not supported for TikTok yet.</p>
                    <a class="button secondary" href="{H.Encode(returnUrl)}">Back</a>
                </section>
                """;
        }
        var brands = new Dictionary<string, (string Color, string Initial)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Facebook"] = ("#1877F2", "f"), ["X"] = ("#000000", "X"), ["Instagram"] = ("#E4405F", "IG"),
            ["LinkedIn"] = ("#0A66C2", "in"), ["Pinterest"] = ("#E60023", "P"), ["TikTok"] = ("#000000", "T"),
            ["Bluesky"] = ("#0085FF", "B"),
        };
        var brand = brands.TryGetValue(platformName, out var b) ? b : ("#0f766e", platformName.Length > 0 ? platformName[0].ToString() : "?");
        var cancelHref = returnUrl;
        var contextNote = brandContext
            ? """<p class="muted">BookPromoter AI brand account — for app promotions only, separate from author book accounts.</p>"""
            : """<p class="muted">Author account — for promoting your books via the Ad Library.</p>""";
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>Connect your {H.Encode(platformName)} account.</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:{brand.Item1}">{H.Encode(brand.Item2)}</div>
                <h2>Authorize BookPromoter AI</h2>
                {contextNote}
                <p class="muted">In a live deployment, this redirects you to {H.Encode(platformName)}'s login screen. Real API credentials are not yet configured — enter details below to simulate a connection.</p>
                <form method="post" action="/social-accounts/oauth-callback/{Uri.EscapeDataString(platformName)}" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <label>Display Name <input name="displayName" value="{H.Encode(platformName)} Account"></label>
                    <label>Handle <input name="handle" placeholder="yourauthorname" required></label>
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:{brand.Item1}">Simulate &amp; Connect</button>
                        <a class="button secondary" href="{H.Encode(cancelHref)}">Cancel</a>
                    </div>
                </form>
            </section>
            """;
    }

    static string BlueskyConnectPage(string returnUrl, string notice, bool brandContext)
    {
        var heading = brandContext
            ? "Connect BookPromoter AI on Bluesky"
            : "Connect your Bluesky account";
        var intro = brandContext
            ? "This account is for <strong>BookPromoter AI promotions only</strong> (app updates, launch posts). It is separate from author accounts you use to promote your books."
            : "Connect your author Bluesky account to auto-post book promotions from the Ad Library.";
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<div class="notice error">{H.Encode(notice)}</div>""";
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>{heading}</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:#0085FF">B</div>
                <h2>Live Bluesky posting</h2>
                <p class="muted">{intro}</p>
                <p class="muted">Create an <strong>App Password</strong> in Bluesky: Settings → Privacy &amp; Security → App Passwords. Name it <em>BookPromoter AI</em>, then paste it below. Your main password is never stored.</p>
                {noticeHtml}
                <form method="post" action="/social-accounts/oauth-callback/{Uri.EscapeDataString("Bluesky")}" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <label>Bluesky handle <input name="handle" placeholder="{H.Encode(BrandConstants.OfficialBlueskyHandle)}" required autocomplete="username"></label>
                    <label>App password <input name="appPassword" type="password" placeholder="xxxx-xxxx-xxxx-xxxx" required autocomplete="off"></label>
                    <label>Display name <input name="displayName" placeholder="BookPromoter AI"></label>
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:#0085FF">Connect &amp; enable live posting</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
            </section>
            """;
    }

    public static string XSetupPage(string returnUrl, string notice, AppSettings? settings)
    {
        var brandContext = IsBrandContext(returnUrl);
        var heading = brandContext
            ? "Connect BookPromoter AI on X"
            : "Connect your X account";
        var intro = brandContext
            ? "This account is for <strong>BookPromoter AI promotions only</strong>. It is separate from author accounts you use to promote your books."
            : "Sign in with X to auto-post book promotions from the Ad Library.";
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<div class="notice error">{H.Encode(notice)}</div>""";
        var configured = settings?.IsXConfigured == true;
        var callbackExample = settings is not null && !string.IsNullOrWhiteSpace(settings.PublicBaseUrl)
            ? XService.CallbackUrl(settings.PublicBaseUrl.TrimEnd('/'))
            : $"https://bookpromoterai.us{XService.CallbackPath}";
        var connectBlock = configured
            ? $"""
                <p class="muted">You will be redirected to X to authorize BookPromoter AI.</p>
                <div class="form-actions">
                    <a class="button" href="/social-accounts/connect/X?return={H.Encode(returnUrl)}" style="background:#000000">Sign in with X</a>
                    <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                </div>
                """
            : """
                <p class="notice error">X API credentials are not configured yet. The app owner must add them in Railway before authors can connect.</p>
                <p class="muted">Owner: open <strong>Owner → X (Twitter) API</strong> for setup steps.</p>
                <a class="button secondary" href="/my-account">Back</a>
                """;
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>{heading}</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:#000000">X</div>
                <h2>Live X posting</h2>
                <p class="muted">{intro}</p>
                <p class="muted small-text">OAuth callback URL for your X developer app: <code>{H.Encode(callbackExample)}</code></p>
                {noticeHtml}
                {connectBlock}
            </section>
            """;
    }

    public static string LinkedInSetupPage(string returnUrl, string notice, AppSettings? settings)
    {
        var brandContext = IsBrandContext(returnUrl);
        var heading = brandContext
            ? "Connect BookPromoter AI on LinkedIn"
            : "Connect your LinkedIn account";
        var intro = brandContext
            ? "This account is for <strong>BookPromoter AI promotions only</strong>. It is separate from author accounts you use to promote your books."
            : "Sign in with LinkedIn to auto-post book promotions from the Ad Library.";
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<div class="notice error">{H.Encode(notice)}</div>""";
        var configured = settings?.IsLinkedInConfigured == true;
        var callbackExample = settings is not null && !string.IsNullOrWhiteSpace(settings.PublicBaseUrl)
            ? LinkedInService.CallbackUrl(settings.PublicBaseUrl.TrimEnd('/'))
            : $"https://bookpromoterai.us{LinkedInService.CallbackPath}";
        var connectBlock = configured
            ? $"""
                <p class="muted">You will be redirected to LinkedIn to authorize BookPromoter AI.</p>
                <div class="form-actions">
                    <a class="button" href="/social-accounts/connect/LinkedIn?return={H.Encode(returnUrl)}" style="background:#0A66C2">Sign in with LinkedIn</a>
                    <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                </div>
                """
            : """
                <p class="notice error">LinkedIn API credentials are not configured yet. The app owner must add them in Railway before authors can connect.</p>
                <p class="muted">Owner: open <strong>Owner → LinkedIn API</strong> for setup steps.</p>
                <a class="button secondary" href="/my-account">Back</a>
                """;
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>{heading}</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:#0A66C2">in</div>
                <h2>Live LinkedIn posting</h2>
                <p class="muted">{intro}</p>
                <p class="muted small-text">OAuth redirect URL for your LinkedIn developer app: <code>{H.Encode(callbackExample)}</code></p>
                {noticeHtml}
                {connectBlock}
            </section>
            """;
    }

    public static string FacebookSetupPage(string returnUrl, string notice, AppSettings? settings, HttpRequest? request = null)
    {
        var brandContext = IsBrandContext(returnUrl);
        var heading = brandContext
            ? "Connect Book Promoter AI on Facebook"
            : "Connect your Facebook Page";
        var intro = brandContext
            ? "Sign in with Facebook to post to your <strong>Book Promoter AI</strong> Page for app promotions."
            : "Sign in with your personal Facebook account, then choose an <strong>author Facebook Page</strong> you manage. Meta does not allow apps to post to personal profile timelines — only to Pages.";
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<div class="notice error">{H.Encode(notice)}</div>""";
        var configured = settings?.IsFacebookConfigured == true;
        var oauthReady = settings?.IsFacebookOAuthReady == true;
        var callbackUrls = settings is not null
            ? string.Join(" ", PublicUrl.FacebookCallbackUrlsForMeta(settings).Select(u => $"<code>{H.Encode(u)}</code>"))
            : $"<code>https://bookpromoterai.us{FacebookService.CallbackPath}</code>";
        var activeCallback = request is not null && settings is not null
            ? $"""<p class="notice">OAuth mode: <strong>{H.Encode(settings.FacebookUsesConfigLogin ? "config_id (Login for Business)" : "scope (Page permissions)")}</strong>. Redirect URI: <code>{H.Encode(PublicUrl.FacebookCallbackUrl(request, settings))}</code></p>"""
            : "";
        var connectBlock = !configured
            ? """
                <p class="notice error">Facebook API credentials are not configured yet. The app owner must add them in Railway before authors can connect.</p>
                <p class="muted">Owner: open <strong>Owner → Facebook API</strong> for setup steps.</p>
                <a class="button secondary" href="/my-account">Back</a>
                """
            : !oauthReady
                ? """
                    <p class="notice error">Facebook App ID and secret are set, but OAuth is not ready.</p>
                    <p class="muted">Scope mode (default) only needs App ID + secret. Config mode also needs <code>Facebook__LoginConfigId</code> in Railway.</p>
                    <a class="button secondary" href="/my-account">Back</a>
                    """
                : $"""
                <p class="muted">You will sign in with Facebook as yourself, then connect a <strong>Facebook Page</strong> for your author brand (not your personal news feed). If Facebook shows a previous connection, click <strong>Edit settings</strong> and pick your author Page — not the BookPromoter AI business Page.</p>
                <div class="form-actions">
                    <a class="button" href="/social-accounts/connect/Facebook?return={H.Encode(returnUrl)}" style="background:#1877F2">Sign in with Facebook</a>
                    <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                </div>
                """;
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>{heading}</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:#1877F2">f</div>
                <h2>Live Facebook Page posting</h2>
                <p class="muted">{intro}</p>
                <p class="muted small-text">Add these OAuth redirect URLs in your Meta app (we use the host you are browsing): {callbackUrls}</p>
                {activeCallback}
                {noticeHtml}
                {connectBlock}
            </section>
            """;
    }

    public static string FacebookPagePickPage(FacebookPagePickPending pending, string token, string? notice = null)
    {
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<div class="notice error">{H.Encode(notice)}</div>""";
        var options = new StringBuilder();
        foreach (var page in pending.Pages)
        {
            options.Append($"""
                <label class="plan-option">
                    <input type="radio" name="pageId" value="{H.Encode(page.Id)}" required>
                    <span>
                        <strong>{H.Encode(page.Name)}</strong>
                        <span class="muted"> @{H.Encode(page.Handle)}</span>
                    </span>
                </label>
                """);
        }

        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>Choose your author Facebook Page</h1></div></section>
            <section class="panel oauth-panel">
                <p class="muted">BookPromoter AI posts to a Facebook <strong>Page</strong>, not your personal profile. Pick the Page you use for your author brand.</p>
                {noticeHtml}
                <form method="post" action="/social-accounts/connect/Facebook/select-page" class="stacked-form">
                    <input type="hidden" name="token" value="{H.Encode(token)}">
                    <fieldset class="plan-options">{options}</fieldset>
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:#1877F2">Connect this Page</button>
                        <a class="button secondary" href="{H.Encode(pending.ReturnUrl)}">Cancel</a>
                    </div>
                </form>
            </section>
            """;
    }

    public static string InstagramSetupPage(string returnUrl, string notice, AppSettings? settings, HttpRequest? request = null)
    {
        var brandContext = IsBrandContext(returnUrl);
        var heading = brandContext
            ? "Connect Book Promoter AI on Instagram"
            : "Connect your Instagram account";
        var intro = brandContext
            ? "Sign in with Facebook to post to the <strong>Book Promoter AI</strong> Instagram account linked to your business Page."
            : "Sign in with Facebook to connect your <strong>Instagram Business or Creator</strong> account. It must be linked to a Facebook Page you manage (set this up in Meta Business Suite).";
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<div class="notice error">{H.Encode(notice)}</div>""";
        var configured = settings?.IsFacebookConfigured == true;
        var oauthReady = settings?.IsFacebookOAuthReady == true;
        var callbackUrls = settings is not null
            ? string.Join(" ", PublicUrl.InstagramCallbackUrlsForMeta(settings).Select(u => $"<code>{H.Encode(u)}</code>"))
            : $"<code>https://bookpromoterai.us{InstagramService.CallbackPath}</code>";
        var activeCallback = request is not null && settings is not null
            ? $"""<p class="notice">OAuth redirect URI: <code>{H.Encode(PublicUrl.InstagramOAuthRedirectUrl(request, settings))}</code> (same as Facebook — required for Meta to return to the app)</p>"""
            : "";
        var connectBlock = !configured
            ? """
                <p class="notice error">Meta API credentials are not configured yet. The app owner must add Facebook App ID and secret in Railway before authors can connect Instagram.</p>
                <p class="muted">Owner: open <strong>Owner → Facebook API</strong> for setup steps (Instagram uses the same Meta app).</p>
                <a class="button secondary" href="/my-account">Back</a>
                """
            : !oauthReady
                ? """
                    <p class="notice error">Meta App ID and secret are set, but OAuth is not ready.</p>
                    <a class="button secondary" href="/my-account">Back</a>
                    """
                : $"""
                <p class="muted">Complete these steps before connecting:</p>
                <ol class="plan-features">
                    <li>Switch Instagram to a <strong>Business or Creator</strong> account.</li>
                    <li>In <a href="https://business.facebook.com/settings/instagram-account-v2" target="_blank" rel="noopener">Meta Business Suite</a>, link IG to your Facebook Page.</li>
                    <li>Owner: add Instagram redirect URIs in Meta (see <strong>Owner → Instagram API</strong>).</li>
                </ol>
                <p class="muted">Instagram posting uses the same Meta app as Facebook. Personal Instagram accounts cannot be connected via the API.</p>
                <p class="muted"><strong>If Meta shows &ldquo;Got it&rdquo; but never returns to BookPromoter AI:</strong> Meta linked the app as a Business Integration without sending an OAuth code. Remove <strong>AuthorPromoter AI</strong> at <a href="https://www.facebook.com/settings?tab=business_tools" target="_blank" rel="noopener">Business integrations</a>, then connect again — you should land back on BookPromoter AI automatically.</p>
                <div class="form-actions">
                    <a class="button" href="/social-accounts/connect/Instagram?return={H.Encode(returnUrl)}" style="background:linear-gradient(45deg,#f09433,#e6683c,#dc2743,#cc2366,#bc1888)">Sign in with Facebook for Instagram</a>
                    <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                </div>
                """;
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>{heading}</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:linear-gradient(45deg,#f09433,#e6683c,#dc2743,#cc2366,#bc1888)">IG</div>
                <h2>Live Instagram posting</h2>
                <p class="muted">{intro}</p>
                <p class="muted small-text">Add these OAuth redirect URLs in your Meta app: {callbackUrls}</p>
                {activeCallback}
                {noticeHtml}
                {connectBlock}
            </section>
            """;
    }

    public static string InstagramPagePickPage(InstagramPagePickPending pending, string token, string? notice = null)
    {
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<div class="notice error">{H.Encode(notice)}</div>""";
        var options = new StringBuilder();
        foreach (var account in pending.Accounts)
        {
            options.Append($"""
                <label class="plan-option">
                    <input type="radio" name="igUserId" value="{H.Encode(account.IgUserId)}" required>
                    <span>
                        <strong>@{H.Encode(account.IgUsername)}</strong>
                        <span class="muted"> via {H.Encode(account.PageName)}</span>
                    </span>
                </label>
                """);
        }

        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>Choose your Instagram account</h1></div></section>
            <section class="panel oauth-panel">
                <p class="muted">Pick the Instagram Business or Creator account you use for your author brand.</p>
                {noticeHtml}
                <form method="post" action="/social-accounts/connect/Instagram/select-account" class="stacked-form">
                    <input type="hidden" name="token" value="{H.Encode(token)}">
                    <fieldset class="plan-options">{options}</fieldset>
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:linear-gradient(45deg,#f09433,#e6683c,#dc2743,#cc2366,#bc1888)">Connect this account</button>
                        <a class="button secondary" href="{H.Encode(pending.ReturnUrl)}">Cancel</a>
                    </div>
                </form>
            </section>
            """;
    }
}
