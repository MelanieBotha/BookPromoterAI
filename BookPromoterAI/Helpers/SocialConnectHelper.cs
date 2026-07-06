using System.Text;

namespace BookPromoterAI;

static class SocialConnectHelper
{
    public const string OwnerReturnPath = "/owner-promos";

    public static readonly string[] DefaultPlatforms =
        ["Facebook", "X", "Reddit", "LinkedIn", "Pinterest", "TikTok", "Bluesky"];

    public static readonly HashSet<string> DisabledPlatforms = new(StringComparer.OrdinalIgnoreCase) { "TikTok" };

    public static bool IsPlatformDisabled(string? platform) =>
        !string.IsNullOrWhiteSpace(platform) && DisabledPlatforms.Contains(platform.Trim());

    public static string DisabledPlatformLabel(string platform) => $"{platform} (video only — coming soon)";

    static readonly Dictionary<string, string> PlatformColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Facebook"] = "#1877F2",
        ["X"] = "#000000",
        ["Reddit"] = "#FF4500",
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
        url == OwnerReturnPath || url == "/my-account";

    public static string ResolveAccountKind(string? returnUrl) =>
        returnUrl == OwnerReturnPath ? SocialAccountKinds.Brand : SocialAccountKinds.Author;

    public static bool IsBrandContext(string? returnUrl) =>
        returnUrl == OwnerReturnPath;

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

        if (PostLimits.IsReddit(platformName))
            return RedditSetupPage(returnUrl, notice, null);

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
            ["Facebook"] = ("#1877F2", "f"), ["X"] = ("#000000", "X"), ["Reddit"] = ("#FF4500", "R"),
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
            ? $"""<p class="notice">OAuth: <strong>{H.Encode(brandContext ? "Login for Business (config_id)" : settings.FacebookUsesConfigLogin ? "config_id" : "scope")}</strong>. Redirect URI: <code>{H.Encode(PublicUrl.FacebookCallbackUrl(request, settings))}</code></p>"""
            : "";
        var brandSteps = brandContext
            ? """
                <div class="notice">
                    <strong>Owner brand connect uses Facebook Login for Business</strong> (not the old scope login that loops on &ldquo;Continue as BookPromoter AI?&rdquo;).
                    <ol class="plan-features">
                        <li>Remove <strong>AuthorPromoter AI</strong> from <a href="https://www.facebook.com/settings?tab=business_tools&amp;section=active" target="_blank" rel="noopener">Business integrations</a> if listed.</li>
                        <li>Click the button below — Meta will show <strong>Login for Business</strong> (Choose Pages → Review → Save).</li>
                        <li>Sign in as <strong>Melanie Botha</strong> (personal account). If Meta says BookPromoter AI, click <strong>Not BookPromoter AI? Log into another account</strong>.</li>
                        <li>Select <strong>Book Promoter AI</strong> Page only → Save → you must return to <strong>bookpromoterai.us</strong>.</li>
                    </ol>
                </div>
                """
            : "";
        var connectHref = $"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&go=1";
        var brandConnectBlock = !configured
            ? """
                <p class="notice error">Facebook API credentials are not configured yet. The app owner must add them in Railway before authors can connect.</p>
                <p class="muted">Owner: open <strong>Owner → Facebook API</strong> for setup steps.</p>
                <a class="button secondary" href="/my-account">Back</a>
                """
            : settings?.IsBrandFacebookOAuthReady != true
                ? """
                    <p class="notice error">Brand Facebook connect requires <code>Facebook__LoginConfigId</code> in Railway (Facebook Login for Business configuration in Meta).</p>
                    <p class="muted">Owner: open <strong>Owner → Facebook API</strong>, copy the Configuration ID from Meta, add it in Railway, redeploy, then try again.</p>
                    <a class="button secondary" href="/owner-promos?section=owner-social">Back</a>
                    """
                : $"""
                {brandSteps}
                <form method="post" action="/social-accounts/connect/Facebook/start" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:#1877F2">Open Meta Login for Business</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
                <p class="muted small-text">If Meta shows &ldquo;Sorry, something went wrong&rdquo;, verify <code>Facebook__LoginConfigId</code> in Railway matches Meta → Facebook Login for Business → Configurations. Or use fallback below.</p>
                <form method="post" action="/social-accounts/connect/Facebook/start" class="form" style="margin-top:1rem">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <input type="hidden" name="mode" value="scope">
                    <button class="button secondary" type="submit">Fallback: standard Page login (use Edit settings on Meta)</button>
                </form>
                """;
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
                : brandContext
                    ? brandConnectBlock
                    : $"""
                <p class="muted">You will sign in with Facebook as yourself, then connect a <strong>Facebook Page</strong> for your author brand (not your personal news feed). If Facebook shows a previous connection, click <strong>Edit settings</strong> and pick your author Page — not the BookPromoter AI business Page.</p>
                <div class="form-actions">
                    <a class="button" href="{H.Encode(connectHref)}" style="background:#1877F2">Sign in with Facebook</a>
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
        var brandContext = SocialAccountKinds.IsBrand(pending.Kind);
        var heading = brandContext ? "Choose the Book Promoter AI Page" : "Choose your author Facebook Page";
        var intro = brandContext
            ? "Pick <strong>Book Promoter AI</strong> (Page ID should start with 1210…). Do not pick Melanie Botha Novels or other author Pages."
            : "BookPromoter AI posts to a Facebook <strong>Page</strong>, not your personal profile. Pick the Page you use for your author brand.";
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
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>{heading}</h1></div></section>
            <section class="panel oauth-panel">
                <p class="muted">{intro}</p>
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

    public static string RedditSetupPage(string returnUrl, string notice, AppSettings? settings, HttpRequest? request = null)
    {
        var brandContext = IsBrandContext(returnUrl);
        var heading = brandContext
            ? "Connect BookPromoter AI on Reddit"
            : "Connect your Reddit account";
        var intro = brandContext
            ? "Post app promotions to a subreddit you moderate or where self-promotion is allowed."
            : "Post book promotions to a subreddit you can submit to (for example a genre or self-promo community).";
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<div class="notice error">{H.Encode(notice)}</div>""";
        var configured = settings?.IsRedditConfigured == true;
        var callbackExample = settings is not null && !string.IsNullOrWhiteSpace(settings.PublicBaseUrl)
            ? RedditService.CallbackUrl(settings.PublicBaseUrl.TrimEnd('/'))
            : $"https://bookpromoterai.us{RedditService.CallbackPath}";
        var defaultSub = brandContext ? "BookPromoterAI" : "";
        var connectBlock = configured
            ? $"""
                <p class="muted">The first line of each post becomes the Reddit title; the rest is the post body.</p>
                <form method="post" action="/social-accounts/connect/Reddit/start" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <label>Subreddit (without r/) <input name="subreddit" value="{H.Encode(defaultSub)}" placeholder="selfpublish" required></label>
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:#FF4500">Sign in with Reddit</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
                """
            : """
                <p class="notice error">Reddit API credentials are not configured yet. The app owner must add them in Railway before authors can connect.</p>
                <p class="muted">Owner: open <strong>Owner → Reddit API</strong> for setup steps.</p>
                <a class="button secondary" href="/my-account">Back</a>
                """;
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>{heading}</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:#FF4500">R</div>
                <h2>Live Reddit posting</h2>
                <p class="muted">{intro}</p>
                <p class="muted small-text">OAuth redirect URL for your Reddit app: <code>{H.Encode(callbackExample)}</code></p>
                {noticeHtml}
                {connectBlock}
            </section>
            """;
    }
}
