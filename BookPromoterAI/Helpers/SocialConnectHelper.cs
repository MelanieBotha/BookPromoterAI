using System.Text;

namespace BookPromoterAI;

static class SocialConnectHelper
{
    public const string OwnerReturnPath = "/owner-promos";
    public const string OwnerVideosReturnPath = "/owner-promos?section=owner-videos";
    public const string VideosReturnPath = "/videos";

    public static string[] DefaultPlatforms(AppSettings? settings, bool brandContext = false) =>
        SocialPlatforms.ReadyConnectBarNames(settings, brandContext).ToArray();

    /// <summary>Platforms with live OAuth or app-password connect and posting.</summary>
    public static bool IsPlatformLive(string? platform, AppSettings? settings = null, bool brandContext = false) =>
        SocialPlatforms.IsReadyToConnect(platform, settings, brandContext);

    public static bool IsPlatformDisabled(string? platform, AppSettings? settings = null, bool brandContext = false) =>
        !SocialPlatforms.IsReadyToConnect(platform, settings, brandContext);

    public static string DisabledPlatformReason(string platform, AppSettings? settings = null) =>
        SocialPlatforms.DisabledReason(platform);

    public static string DisabledPlatformLabel(string platform, AppSettings? settings = null) =>
        $"{DisplayPlatformName(platform)} ({DisabledPlatformReason(platform, settings)})";

    static string DisplayPlatformName(string platform) =>
        SocialPlatforms.NormalizeName(platform) switch
        {
            "X" => "X",
            _ => platform.Trim()
        };

    static string NormalizePlatformName(string platform) => SocialPlatforms.NormalizeName(platform);

    public static string ResolveReturnUrl(HttpRequest request, string? formReturn = null)
    {
        var value = !string.IsNullOrWhiteSpace(formReturn)
            ? formReturn
            : request.Query["return"].ToString();
        return IsAllowedReturnUrl(value) ? value : "/my-account";
    }

    public static bool IsAllowedReturnUrl(string? url) =>
        url == OwnerReturnPath
        || url == OwnerVideosReturnPath
        || url == "/my-account"
        || url == VideosReturnPath
        || (url?.StartsWith("/owner-promos?", StringComparison.OrdinalIgnoreCase) == true);

    public static string ResolveAccountKind(string? returnUrl) =>
        IsBrandContext(returnUrl) ? SocialAccountKinds.Brand : SocialAccountKinds.Author;

    public static bool IsBrandContext(string? returnUrl) =>
        returnUrl == OwnerReturnPath
        || returnUrl == OwnerVideosReturnPath
        || (returnUrl?.StartsWith("/owner-promos?", StringComparison.OrdinalIgnoreCase) == true);

    public static string ConnectButtons(string returnUrl, AppSettings? settings = null)
    {
        var brandContext = IsBrandContext(returnUrl);
        var buttons = new StringBuilder();
        foreach (var platform in SocialPlatforms.ReadyConnectBarNames(settings, brandContext))
        {
            var color = SocialPlatforms.Color(platform);
            var href = $"/social-accounts/connect/{Uri.EscapeDataString(platform)}?return={Uri.EscapeDataString(returnUrl)}";
            buttons.Append($"""
                <a class="button" href="{href}" style="background:{color}">
                    Connect {H.Encode(platform)}
                </a>
                """);
        }

        if (buttons.Length == 0)
        {
            return brandContext
                ? """<p class="muted">No brand connect buttons yet. Add API keys under <strong>Owner → Social Media APIs</strong>, or connect <strong>Bluesky</strong>, <strong>Discord</strong>, or <strong>Telegram</strong> (no extra keys needed).</p>"""
                : """<p class="muted">No connect buttons yet. <strong>Bluesky</strong>, <strong>Discord</strong>, and <strong>Telegram</strong> work without Owner API keys. Other platforms appear here after the app owner adds credentials in <strong>Owner → Social Media APIs</strong>.</p>""";
        }

        return buttons.ToString();
    }

    public static string RenderPlatformOption(
        string value,
        bool selected = false,
        AppSettings? settings = null,
        bool brandContext = false)
    {
        if (!selected && !SocialPlatforms.IsReadyToConnect(value, settings, brandContext))
            return "";
        var sel = selected ? " selected" : "";
        return $"""<option value="{H.Encode(value)}"{sel}>{H.Encode(value)}</option>""";
    }

    public static string NextPlatformHint(AppSettings? settings = null) => "";

    public static string OAuthAuthorizePage(string platformName, string returnUrl, string notice = "", AppSettings? settings = null)
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

        if (PostLimits.IsTikTok(platformName))
            return TikTokSetupPage(returnUrl, notice, settings);

        if (PostLimits.IsMastodon(platformName))
            return MastodonConnectPage(returnUrl, notice, brandContext);

        if (PostLimits.IsDiscord(platformName))
            return DiscordConnectPage(returnUrl, notice, brandContext);

        if (PostLimits.IsTelegram(platformName))
            return TelegramConnectPage(returnUrl, notice, brandContext);

        if (PostLimits.IsTumblr(platformName))
            return TumblrSetupPage(returnUrl, notice, settings);

        if (PostLimits.IsFlickr(platformName))
            return FlickrSetupPage(returnUrl, notice, settings);

        if (PostLimits.IsWordPress(platformName))
            return WordPressConnectPage(returnUrl, notice, brandContext);

        if (PostLimits.IsMedium(platformName))
            return MediumConnectPage(returnUrl, notice, brandContext);

        if (IsPlatformDisabled(platformName, settings, brandContext))
        {
            var reason = DisabledPlatformReason(platformName, settings);
            return $"""
                <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>{H.Encode(DisabledPlatformLabel(platformName, settings))}</h1></div></section>
                <section class="panel">
                    <p class="notice error">{H.Encode(char.ToUpper(reason[0]) + reason[1..])}.</p>
                    <a class="button secondary" href="{H.Encode(returnUrl)}">Back</a>
                </section>
                """;
        }

        var brand = SocialPlatforms.Brand(platformName);
        var cancelHref = returnUrl;
        var contextNote = brandContext
            ? """<p class="muted">BookPromoter AI brand account — for app promotions only, separate from author book accounts.</p>"""
            : """<p class="muted">Author account — for promoting your books via the Ad Library.</p>""";
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>Connect your {H.Encode(platformName)} account.</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:{brand.Color}">{H.Encode(brand.Initial)}</div>
                <h2>Authorize BookPromoter AI</h2>
                {contextNote}
                <p class="muted">In a live deployment, this redirects you to {H.Encode(platformName)}'s login screen. Real API credentials are not yet configured — enter details below to simulate a connection.</p>
                <form method="post" action="/social-accounts/oauth-callback/{Uri.EscapeDataString(platformName)}" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <label>Display Name <input name="displayName" value="{H.Encode(platformName)} Account"></label>
                    <label>Handle <input name="handle" placeholder="yourauthorname" required></label>
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:{brand.Color}">Simulate &amp; Connect</button>
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
                <p class="muted">Owner: open <strong>Owner → Social Media APIs → X (Twitter)</strong> for setup steps.</p>
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
                <p class="muted">Owner: open <strong>Owner → Social Media APIs → LinkedIn</strong> for setup steps.</p>
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
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<div class="notice error">{H.Encode(notice)}</div>""";
        var configured = settings?.IsFacebookConfigured == true;
        var oauthReady = settings?.IsFacebookOAuthReady == true;

        if (!brandContext)
            return AuthorFacebookConnectPage(returnUrl, noticeHtml, configured, oauthReady);

        var heading = "Connect Book Promoter AI on Facebook";
        var intro = "Sign in with Facebook to post to your <strong>Book Promoter AI</strong> Page for app promotions.";
        var callbackUrls = settings is not null
            ? string.Join(" ", PublicUrl.FacebookCallbackUrlsForMeta(settings).Select(u => $"<code>{H.Encode(u)}</code>"))
            : $"<code>https://bookpromoterai.us{FacebookService.CallbackPath}</code>";
        var activeCallback = request is not null && settings is not null
            ? BuildFacebookOAuthDiagnostics(settings, request, brandContext: true)
            : "";
        var brandSteps = """
            <div class="notice error">
                <strong>Critical — use the right Facebook account</strong>
                <p>If the top-right of Facebook shows <strong>BookPromoter</strong> or &ldquo;Continue as BookPromoter AI?&rdquo;, you are on the <em>business portfolio</em> account. That causes an endless loop.</p>
                <ol class="plan-features">
                    <li>Open <a href="https://www.facebook.com/logout.php" target="_blank" rel="noopener">facebook.com/logout</a> (or click <strong>Not BookPromoter AI? Log into another account</strong> on Meta&rsquo;s dialog).</li>
                    <li>Sign in as <strong>Melanie Botha</strong> (your personal profile — the one that admins the Book Promoter AI Page).</li>
                    <li>Remove <strong>AuthorPromoter AI</strong> from <a href="https://www.facebook.com/settings?tab=business_tools&amp;section=active" target="_blank" rel="noopener">Business integrations → Active</a> (must be <strong>0 active</strong>).</li>
                    <li>On Meta&rsquo;s dialog: click <strong>Edit settings</strong> (never Continue alone) → tick <strong>Book Promoter AI</strong> only → Save.</li>
                    <li><strong>Book Promoter AI missing from the list?</strong> That Page is linked to your Meta Business portfolio — the app must request <code>business_management</code> (v1.9.63+). Also confirm Melanie Botha (personal) has <strong>Full control</strong> on the Page in <a href="https://business.facebook.com/settings/pages" target="_blank" rel="noopener">Business Suite → Pages → Book Promoter AI → Page access</a>.</li>
                    <li>Do <strong>not</strong> connect <strong>Melanie Botha Novels</strong> for brand posting — that is your author Page, not Book Promoter AI.</li>
                    <li>Success = browser returns to <strong>bookpromoterai.us</strong>.</li>
                </ol>
            </div>
            """;
        var brandConnectBlock = !configured
            ? """
                <p class="notice error">Facebook API credentials are not configured yet. The app owner must add them in Railway before authors can connect.</p>
                <p class="muted">Owner: open <strong>Owner → Social Media APIs → Facebook</strong> for setup steps.</p>
                <a class="button secondary" href="/my-account">Back</a>
                """
            : settings?.IsBrandFacebookOAuthReady != true
                ? """
                    <p class="notice error">Facebook API credentials are not configured yet.</p>
                    <p class="muted">Owner: open <strong>Owner → Social Media APIs → Facebook</strong> for setup steps.</p>
                    <a class="button secondary" href="/owner-promos?section=owner-social">Back</a>
                    """
                : settings.FacebookUsesConfigLogin
                    ? $"""
                {brandSteps}
                <form method="post" action="/social-accounts/connect/Facebook/start" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <input type="hidden" name="mode" value="config">
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:#1877F2">Open Meta Login for Business</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
                <form method="post" action="/social-accounts/connect/Facebook/start" class="form" style="margin-top:1rem">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <input type="hidden" name="mode" value="scope">
                    <button class="button secondary" type="submit">Try standard Page login instead</button>
                </form>
                """
                    : $"""
                {brandSteps}
                <form method="post" action="/social-accounts/connect/Facebook/start" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <input type="hidden" name="mode" value="scope">
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:#1877F2">Connect Book Promoter AI Page</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
                {(settings.HasFacebookLoginConfigId
                    ? $"""
                <form method="post" action="/social-accounts/connect/Facebook/start" class="form" style="margin-top:1rem">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <input type="hidden" name="mode" value="config">
                    <button class="button secondary" type="submit">Alternate: Login for Business (config_id)</button>
                </form>
                """
                    : "")}
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
                {brandConnectBlock}
            </section>
            """;
    }

    static string AuthorFacebookConnectPage(string returnUrl, string noticeHtml, bool configured, bool oauthReady)
    {
        var connectBlock = !configured || !oauthReady
            ? """
                <p class="notice error">Facebook is not available right now. Please try again later.</p>
                <a class="button secondary" href="/my-account">Back to My Account</a>
                """
            : $"""
                <form method="post" action="/social-accounts/connect/Facebook/start" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:#1877F2">Sign in with Facebook</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
                """;
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>Connect your Facebook Page</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:#1877F2">f</div>
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
                <p class="muted">Owner: open <strong>Owner → Social Media APIs → Reddit</strong> for setup steps.</p>
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

    public static string TikTokSetupPage(string returnUrl, string notice, AppSettings? settings)
    {
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<div class="notice error">{H.Encode(notice)}</div>""";
        var configured = settings?.IsTikTokConfigured == true;
        var callbackExample = settings is not null && !string.IsNullOrWhiteSpace(settings.PublicBaseUrl)
            ? TikTokService.CallbackUrl(settings.PublicBaseUrl.TrimEnd('/'))
            : $"https://bookpromoterai.us{TikTokService.CallbackPath}";
        var connectBlock = configured
            ? $"""
                <p class="muted">Authorize BookPromoter AI to send book promo videos to your TikTok inbox.</p>
                <form method="post" action="/social-accounts/connect/TikTok/start" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:#000">Sign in with TikTok</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
                """
            : """
                <p class="notice error">TikTok API credentials are not configured yet. Register at developers.tiktok.com and add TikTok__ClientKey and TikTok__ClientSecret on Railway.</p>
                <a class="button secondary" href="/videos">Back to Videos</a>
                """;
        return $"""
            <section class="hero"><div><p class="eyebrow">TikTok</p><h1>Connect your TikTok account</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:#000">T</div>
                <h2>Book promo videos</h2>
                <p class="muted">Videos upload to your TikTok inbox for final edits in the TikTok app. Vertical 9:16 works best.</p>
                <p class="muted small-text">OAuth redirect URL: <code>{H.Encode(callbackExample)}</code></p>
                {noticeHtml}
                {connectBlock}
            </section>
            """;
    }

    static string BuildFacebookOAuthDiagnostics(AppSettings settings, HttpRequest request, bool brandContext)
    {
        var redirect = PublicUrl.FacebookCallbackUrl(request, settings);
        var facebook = request.HttpContext.RequestServices.GetRequiredService<FacebookService>();
        var scopeDiag = facebook.DescribeOAuth(redirect, brandContext, forceScope: true, forceConfig: false);
        var configDiag = brandContext && settings.HasFacebookLoginConfigId
            ? facebook.DescribeOAuth(redirect, brandContext, forceScope: false, forceConfig: true)
            : null;
        var defaultFlow = brandContext && settings.FacebookUsesConfigLogin ? configDiag : scopeDiag;
        var configLine = configDiag is null
            ? ""
            : $"""<li><strong>Login for Business:</strong> {(configDiag.Ready ? H.Encode(configDiag.FlowLabel) : H.Encode(configDiag.Error ?? "not ready"))} · config {H.Encode(configDiag.ConfigIdMasked)}</li>""";
        return $"""
            <details class="notice" style="margin-top:1rem">
                <summary><strong>OAuth diagnostics (v{H.Encode(AppVersion.Display)})</strong></summary>
                <ul class="plan-features small-text">
                    <li><strong>Railway OAuth mode:</strong> <code>{H.Encode(settings.FacebookOAuthMode)}</code> (scope = recommended)</li>
                    <li><strong>App ID:</strong> <code>{H.Encode(scopeDiag.AppIdMasked)}</code> (expect 1820…6321)</li>
                    <li><strong>Redirect URI:</strong> <code>{H.Encode(redirect)}</code></li>
                    <li><strong>Default connect:</strong> {(defaultFlow?.Ready == true ? H.Encode(defaultFlow.FlowLabel) : H.Encode(defaultFlow?.Error ?? "not ready"))}</li>
                    <li><strong>Page permissions (scope):</strong> {(scopeDiag.Ready ? H.Encode(scopeDiag.FlowLabel) : H.Encode(scopeDiag.Error ?? "not ready"))}</li>
                    {(brandContext ? configLine : "")}
                    <li><strong>Meta Login Configuration</strong> must use token type <em>User access token</em> (not System user) and Assets = Pages.</li>
                </ul>
            </details>
            """;
    }

    public static string MastodonConnectPage(string returnUrl, string? notice, bool brandContext = false)
    {
        var brand = SocialPlatforms.Brand("Mastodon");
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<p class="notice">{H.Encode(notice)}</p>""";
        var contextNote = brandContext
            ? """<p class="muted">Brand account — app promotions only.</p>"""
            : """<p class="muted">Author account — posts from your Ad Library schedule.</p>""";
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>Connect Mastodon</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:{brand.Color}">{H.Encode(brand.Initial)}</div>
                <h2>Your Mastodon instance</h2>
                {contextNote}
                {noticeHtml}
                <p class="muted">Enter the server where your account lives (e.g. <code>mastodon.social</code>). We register BookPromoter AI with that instance and redirect you to approve posting.</p>
                <form method="post" action="/social-accounts/connect/Mastodon/start" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <label>Instance host <input name="instance" placeholder="mastodon.social" required></label>
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:{brand.Color}">Continue to Mastodon</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
            </section>
            """;
    }

    public static string DiscordConnectPage(string returnUrl, string? notice, bool brandContext = false)
    {
        var brand = SocialPlatforms.Brand("Discord");
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<p class="notice">{H.Encode(notice)}</p>""";
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>Connect Discord</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:{brand.Color}">{H.Encode(brand.Initial)}</div>
                <h2>Channel webhook</h2>
                {noticeHtml}
                <p class="muted">Create a webhook in your Discord channel (Channel settings → Integrations → Webhooks). BookPromoter AI will post scheduled promos there automatically, including the book cover or BookPromoter AI logo when available.</p>
                <form method="post" action="/social-accounts/oauth-callback/Discord" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <label>Display name <input name="displayName" value="Discord Channel"></label>
                    <label>Webhook URL <input name="webhookUrl" type="url" placeholder="https://discord.com/api/webhooks/..." required></label>
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:{brand.Color}">Connect &amp; save</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
            </section>
            """;
    }

    public static string TelegramConnectPage(string returnUrl, string? notice, bool brandContext = false)
    {
        var brand = SocialPlatforms.Brand("Telegram");
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<p class="notice error">{H.Encode(notice)}</p>""";
        var contextNote = brandContext
            ? """<p class="muted">Connect a <strong>BookPromoter AI</strong> Telegram channel or group for brand promos.</p>"""
            : """<p class="muted">Connect your author Telegram channel or group for book promos.</p>""";
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>Connect Telegram</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:{brand.Color}">{H.Encode(brand.Initial)}</div>
                <h2>Bot + channel</h2>
                {contextNote}
                {noticeHtml}
                <ol class="muted small-text">
                    <li>In Telegram, open <strong>@BotFather</strong> and create a bot. Copy the <strong>bot token</strong>.</li>
                    <li>Add the bot to your channel or group as an <strong>admin</strong> (channels need post permission).</li>
                    <li>Paste the channel chat ID (often <code>-100…</code>) or public username like <code>@YourChannel</code>.</li>
                </ol>
                <p class="muted small-text">BookPromoter AI posts your caption plus the book cover or brand logo when available.</p>
                <form method="post" action="/social-accounts/oauth-callback/Telegram" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <label>Display name <input name="displayName" placeholder="My Author Channel"></label>
                    <label>Bot token <input name="botToken" placeholder="123456:ABC..." required autocomplete="off"></label>
                    <label>Chat ID <input name="chatId" placeholder="-1001234567890 or @YourChannel" required></label>
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:{brand.Color}">Connect &amp; save</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
            </section>
            """;
    }

    public static string TumblrSetupPage(string returnUrl, string? notice, AppSettings? settings)
    {
        var brandContext = IsBrandContext(returnUrl);
        var brand = SocialPlatforms.Brand("Tumblr");
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<p class="notice">{H.Encode(notice)}</p>""";
        var configured = settings?.IsTumblrConfigured == true;
        var callbackExample = settings is not null && !string.IsNullOrWhiteSpace(settings.PublicBaseUrl)
            ? TumblrService.CallbackUrl(settings.PublicBaseUrl.TrimEnd('/'))
            : $"https://bookpromoterai.us{TumblrService.CallbackPath}";
        var contextCopy = brandContext
            ? """<p class="muted">Sign in with the Tumblr account that owns your <strong>BookPromoter AI</strong> blog (e.g. <code>bookpromoterai.tumblr.com</code>). App promos with logo and links to <code>/start</code> and <code>/trial</code> will post here — separate from author book promos on My Account.</p>"""
            : """<p class="muted">Sign in with your <strong>author</strong> Tumblr account. BookPromoter AI will post text and book-cover photos to the blog you choose — not the BookPromoter AI brand blog (connect that on Owner → Brand Social).</p>""";
        var connectBlock = configured
            ? $"""
                {contextCopy}
                <form method="post" action="/social-accounts/connect/Tumblr/start" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:{brand.Color}">Sign in with Tumblr</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
                """
            : """
                <p class="notice error">Tumblr API credentials are not configured yet. The app owner must add them in Railway before authors can connect.</p>
                <p class="muted">Owner: open <strong>Owner → Social Media APIs → Tumblr</strong> for setup steps.</p>
                <a class="button secondary" href="/my-account">Back</a>
                """;
        var heading = brandContext ? "Connect BookPromoter AI on Tumblr" : "Connect Tumblr";
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>{heading}</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:{brand.Color}">{H.Encode(brand.Initial)}</div>
                <h2>Live Tumblr posting</h2>
                <p class="muted small-text">OAuth callback URL for your Tumblr app: <code>{H.Encode(callbackExample)}</code></p>
                {noticeHtml}
                {connectBlock}
            </section>
            """;
    }

    static string WordPressConnectPage(string returnUrl, string notice, bool brandContext)
    {
        var heading = brandContext
            ? "Connect BookPromoter AI on WordPress"
            : "Connect your WordPress blog";
        var intro = brandContext
            ? "Post BookPromoter AI app promos to your <strong>brand WordPress site</strong> (e.g. bookpromoterai.wordpress.com)."
            : "Auto-post book promos as blog posts on your author WordPress site.";
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<div class="notice error">{H.Encode(notice)}</div>""";
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>{heading}</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:#21759B">WP</div>
                <h2>Live WordPress posting</h2>
                <p class="muted">{intro}</p>
                <p class="muted">In WordPress admin: <strong>Users → Profile → Application Passwords</strong>. Create one named <em>BookPromoter AI</em> and paste it below. Works on self-hosted WordPress and WordPress.com sites that support application passwords.</p>
                {noticeHtml}
                <form method="post" action="/social-accounts/oauth-callback/{Uri.EscapeDataString("WordPress")}" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <label>Site URL <input name="siteUrl" placeholder="https://yourblog.com" required autocomplete="url"></label>
                    <label>WordPress username <input name="username" placeholder="admin" required autocomplete="username"></label>
                    <label>Application password <input name="appPassword" type="password" placeholder="xxxx xxxx xxxx xxxx" required autocomplete="off"></label>
                    <label>Display name <input name="displayName" placeholder="{(brandContext ? "BookPromoter AI" : "My Author Blog")}"></label>
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:#21759B">Connect &amp; enable live posting</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
            </section>
            """;
    }

    public static string FlickrSetupPage(string returnUrl, string? notice, AppSettings? settings)
    {
        var brandContext = IsBrandContext(returnUrl);
        var brand = SocialPlatforms.Brand("Flickr");
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<div class="notice error">{H.Encode(notice)}</div>""";
        var configured = settings?.IsFlickrConfigured == true;
        var callbackExample = configured && !string.IsNullOrWhiteSpace(settings?.PublicBaseUrl)
            ? FlickrService.CallbackUrl(settings.PublicBaseUrl.TrimEnd('/'))
            : $"https://bookpromoterai.us{FlickrService.CallbackPath}";
        var contextNote = brandContext
            ? """<p class="muted">Sign in with the Flickr account for <strong>BookPromoter AI</strong> brand promos. Posts upload your book cover or brand logo with caption and tags.</p>"""
            : """<p class="muted">Sign in with your <strong>author</strong> Flickr account. BookPromoter AI uploads book-cover photos with your promo caption.</p>""";
        var connectBlock = configured
            ? $"""
                <form method="post" action="/social-accounts/connect/Flickr/start" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:{brand.Color}">Sign in with Flickr</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
                """
            : """
                <p class="notice error">Flickr API credentials are not configured yet. The app owner must add them in Railway before authors can connect.</p>
                <p class="muted">Owner: open <strong>Owner → Social Media APIs → Flickr</strong> for setup steps.</p>
                """;
        var heading = brandContext ? "Connect BookPromoter AI on Flickr" : "Connect Flickr";
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>{heading}</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:{brand.Color}">{H.Encode(brand.Initial)}</div>
                <h2>Live Flickr posting</h2>
                {contextNote}
                <p class="notice">Flickr only allows <strong>Pro</strong> subscribers to create API keys. Free Flickr accounts cannot connect for auto-posting.</p>
                <p class="muted small-text">OAuth callback URL for your Flickr app: <code>{H.Encode(callbackExample)}</code></p>
                {noticeHtml}
                {connectBlock}
            </section>
            """;
    }

    static string MediumConnectPage(string returnUrl, string notice, bool brandContext)
    {
        var heading = brandContext
            ? "Connect BookPromoter AI on Medium"
            : "Connect your Medium account";
        var intro = brandContext
            ? "Publish BookPromoter AI app promos as Medium stories on your <strong>brand Medium profile</strong>."
            : "Auto-post book promos as Medium stories on your author profile.";
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<div class="notice error">{H.Encode(notice)}</div>""";
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>{heading}</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:#000000">Me</div>
                <h2>Live Medium posting</h2>
                <p class="muted">{intro}</p>
                <p class="notice">Medium stopped issuing <strong>new</strong> integration tokens. Auto-posting only works if you already have a legacy token from <a href="https://medium.com/me/settings" target="_blank" rel="noopener">medium.com/me/settings</a> → <strong>Integration tokens</strong>. If that section is missing, API posting is not available on your account yet.</p>
                {noticeHtml}
                <form method="post" action="/social-accounts/oauth-callback/{Uri.EscapeDataString("Medium")}" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <label>Integration token <input name="integrationToken" type="password" placeholder="paste your Medium token" required autocomplete="off"></label>
                    <label>Display name <input name="displayName" placeholder="{(brandContext ? "BookPromoter AI" : "My Medium")}"></label>
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:#000000">Connect &amp; enable live posting</button>
                        <a class="button secondary" href="{H.Encode(returnUrl)}">Cancel</a>
                    </div>
                </form>
            </section>
            """;
    }

    public static string TumblrBlogPickPage(TumblrBlogPickPending pending, string token, string? notice = null)
    {
        var brandContext = SocialAccountKinds.IsBrand(pending.Kind);
        var noticeHtml = string.IsNullOrWhiteSpace(notice) ? "" : $"""<div class="notice error">{H.Encode(notice)}</div>""";
        var options = new StringBuilder();
        foreach (var blog in pending.Blogs)
        {
            var primary = blog.Primary ? " <span class=\"muted\">(primary)</span>" : "";
            options.Append($"""
                <label class="plan-option">
                    <input type="radio" name="blogId" value="{H.Encode(blog.Identifier)}" required>
                    <span>
                        <strong>{H.Encode(blog.Title)}</strong>
                        <span class="muted"> {H.Encode(blog.Identifier)}{primary}</span>
                    </span>
                </label>
                """);
        }

        var pickIntro = brandContext
            ? """<p class="muted">Choose your <strong>BookPromoter AI</strong> Tumblr blog (e.g. <code>bookpromoterai.tumblr.com</code>) for app marketing posts.</p>"""
            : """<p class="muted">BookPromoter AI will publish scheduled book promos to the author blog you select.</p>""";
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>Choose your Tumblr blog</h1></div></section>
            <section class="panel oauth-panel">
                {pickIntro}
                {noticeHtml}
                <form method="post" action="/social-accounts/connect/Tumblr/select-blog" class="stacked-form">
                    <input type="hidden" name="token" value="{H.Encode(token)}">
                    <fieldset class="plan-options">{options}</fieldset>
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:#36465D">Connect this blog</button>
                        <a class="button secondary" href="{H.Encode(pending.ReturnUrl)}">Cancel</a>
                    </div>
                </form>
            </section>
            """;
    }
}
