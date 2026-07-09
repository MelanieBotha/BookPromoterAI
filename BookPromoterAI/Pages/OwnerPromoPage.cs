using System.Text;
namespace BookPromoterAI;

static class OwnerPromoPage
{
    public static string Render(
        AppStoreDb store,
        string appBaseUrl,
        ReleaseNotesCatalog? releaseNotes = null,
        string? activeSection = null,
        bool shufflePromoPreviews = false)
    {
        var open = (string id) => string.Equals(id, activeSection, StringComparison.OrdinalIgnoreCase) ? " open" : "";
        var returnPath = SocialConnectHelper.OwnerReturnPath;
        var version = AppVersion.Display;
        var draft = releaseNotes?.GetDraft(version) ?? ReleaseNoteDraft.ForVersion(version);
        var title = string.IsNullOrWhiteSpace(draft.Title) ? $"BookPromoter AI v{version} — What's new" : draft.Title;
        var alreadyPublished = store.ProductUpdates.Any(u => string.Equals(u.Version, version, StringComparison.OrdinalIgnoreCase));
        var draftNotice = alreadyPublished
            ? $"""<p class="notice success">v{H.Encode(version)} was already published. Bump <code>ReleaseNotes.json</code> and the version in <code>BookPromoterAI.csproj</code> for the next patch.</p>"""
            : draft.HasContent
                ? $"""<p class="notice success">Draft loaded automatically from <code>ReleaseNotes.json</code> for v{H.Encode(version)}. Review and click Publish update.</p>"""
                : $"""<p class="notice error">No release notes found for v{H.Encode(version)}. Add an entry to <code>ReleaseNotes.json</code> when you bump the version.</p>""";

        var now = DateTime.UtcNow;
        var socialAccounts = store.OwnerSocialAccounts;
        var promoAccounts = socialAccounts
            .Where(a => SocialPlatforms.AllowsBrandConnect(a.Platform))
            .OrderBy(a => a.Platform, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var authorOnlyBrandAccounts = socialAccounts
            .Where(a => !SocialPlatforms.AllowsBrandConnect(a.Platform))
            .ToList();
        var weekBaseSeed = shufflePromoPreviews
            ? Random.Shared.Next()
            : AppPromoGenerator.WeeklyPromoSeed(now, "owner-promo-preview");
        var promoPosts = AppPromoGenerator.GeneratePromoPosts(
            promoAccounts.Select(a => a.Platform),
            appBaseUrl,
            weekBaseSeed);
        var logoPreviewUrl = PostBranding.LogoUrlForSite(appBaseUrl);
        var authorOnlyNote = authorOnlyBrandAccounts.Count > 0
            ? $"""<p class="notice">These connected accounts are <strong>author-only</strong> and are not used for BookPromoter AI brand promos: {H.Encode(string.Join(", ", authorOnlyBrandAccounts.Select(a => a.Platform)))}. Use <strong>My Account</strong> for book promos, or remove them here.</p>"""
            : "";
        var accountNote = socialAccounts.Count > 0
            ? $"""<p class="notice success">{socialAccounts.Count} BookPromoter AI brand account(s) connected: {H.Encode(string.Join(", ", socialAccounts.Select(a => a.Platform)))}.</p>"""
            : """<p class="notice error">No brand accounts connected yet. Connect platforms below for BookPromoter AI promotions (separate from author book accounts on My Account).</p>""";

        var connectedRows = new StringBuilder();
        foreach (var account in socialAccounts)
        {
            var status = account.IsLiveConnection
                ? "Live posting"
                : account.ConnectedViaOAuth ? "Simulated" : "Manual";
            connectedRows.Append($"""
                <div class="promo-row plan-row">
                    <span>{H.Encode(account.Platform)}</span>
                    <span>
                        <strong>{H.Encode(account.DisplayName)}</strong>
                        <span class="muted"> @{H.Encode(account.Handle)} &middot; {status}</span>
                    </span>
                    <span>
                        <a class="button secondary small" href="/social-accounts/edit/{account.Id}?return={Uri.EscapeDataString(returnPath)}">Edit</a>
                        <form method="post" action="/social-accounts/delete/{account.Id}" class="inline-form tight">
                            <input type="hidden" name="return" value="{returnPath}">
                            <button class="danger-button small" type="submit">Remove</button>
                        </form>
                    </span>
                </div>
                """);
        }
        if (socialAccounts.Count == 0)
            connectedRows.Append("""<p class="muted">Connect a platform below to enable Post buttons in the promotion section.</p>""");

        var brandScheduleRows = new StringBuilder();
        foreach (var account in socialAccounts)
        {
            var schedule = store.OwnerBrandSchedules.FirstOrDefault(s =>
                s.Platform.Equals(account.Platform, StringComparison.OrdinalIgnoreCase));
            var postsPerWeek = schedule?.PostsPerWeek ?? 1;
            var autoPostChecked = (schedule?.AutoPostEnabled ?? false) ? "checked" : "";
            var autoHint = schedule?.AutoPostEnabled == true
                ? AppStoreDb.FormatNextAutoPostHint(schedule) is string hint
                    ? $"""<p class="muted small-text">{H.Encode(hint)}</p>"""
                    : ""
                : "";
            brandScheduleRows.Append($"""
                <article class="book-row account-schedule-row">
                    <div>
                        <strong>{H.Encode(account.Platform)}</strong>
                        <p class="muted small-text">Auto-post BookPromoter AI promos with logo on all connected platforms.</p>
                        {autoHint}
                    </div>
                    <input type="hidden" name="platform" value="{H.Encode(account.Platform)}">
                    <label>Posts/week
                        <input name="postsPerWeek" type="number" min="0" max="14" value="{postsPerWeek}">
                    </label>
                    <label class="checkbox">
                        <input name="autoPostEnabled" value="{H.Encode(account.Platform)}" type="checkbox" {autoPostChecked}>
                        Auto-post
                    </label>
                </article>
                """);
        }

        var brandScheduleSection = socialAccounts.Count > 0
            ? $"""
                <h3 style="margin-top:24px">Brand auto-post schedule</h3>
                <p class="muted small-text">Promotes BookPromoter AI on a schedule (checks every 5 minutes). Set <strong>posts/week</strong> above 0 and check <strong>Auto-post</strong>. Posts include the BookPromoter AI logo on all live platforms.</p>
                <form method="post" action="/owner/brand-schedule" class="schedule-list">
                    {brandScheduleRows}
                    <button class="button" type="submit">Save brand schedule</button>
                </form>
                """
            : "";

        var brandLogRows = new StringBuilder();
        foreach (var entry in store.OwnerBrandPostingLog.Take(20))
        {
            var statusClass = entry.Success ? "available" : "used";
            var statusText = entry.Success ? "Posted" : "Failed";
            var clicks = entry.ClickCount is int clickCount ? clickCount.ToString() : "—";
            var likes = entry.Success ? entry.LikeCount.ToString() : "—";
            brandLogRows.Append($"""
                <div class="promo-row">
                    <span>{H.Encode(entry.Platform)} &middot; {H.Encode(entry.BookTitle)}</span>
                    <span>{AppTimeZone.FormatWithZone(entry.AttemptedAt, "MMM d, HH:mm")} &middot; {H.Encode(entry.Message)}</span>
                    <span class="muted">{clicks}</span>
                    <span class="muted">{likes}</span>
                    <span class="status {statusClass}">{statusText}</span>
                </div>
                """);
        }
        if (store.OwnerBrandPostingLog.Count == 0)
            brandLogRows.Append("""<p class="muted">No brand posting activity yet. Post manually below or enable Auto-post on a connected brand account.</p>""");

        var promoClickRows = new StringBuilder();
        var promoClicks = store.OwnerBookPromoClicksByPlatform();
        foreach (var (platform, clicks) in promoClicks)
        {
            promoClickRows.Append($"""
                <div class="promo-row">
                    <span>{H.Encode(platform)}</span>
                    <span class="muted">Book link clicks from author promos</span>
                    <span><strong>{clicks}</strong></span>
                </div>
                """);
        }
        if (promoClicks.Count == 0)
            promoClickRows.Append("""<p class="muted">No tracked book-link clicks from social posts yet.</p>""");

        // ── Website clicks driven by brand promos (bookpromoterai.us) ──────
        var websiteClicksByPlatform = store.OwnerBrandWebsiteClicksByPlatform();
        var websiteClicksThisMonth = store.OwnerBrandWebsiteClicksThisMonth();
        var websiteClicksAllTime = store.OwnerBrandWebsiteClicksAllTime();
        var (startClicks, trialClicks) = store.OwnerBrandWebsiteClicksByDestination();
        var websiteByMonth = store.OwnerBrandWebsiteClicksByMonth();
        var websiteMonths = RecentMonths();
        var websiteChart = BuildWebsiteClickChart(websiteByMonth, websiteMonths);
        var websiteMonthlyTable = BuildWebsiteMonthlyTable(websiteByMonth, websiteMonths, websiteClicksByPlatform.Keys.ToList());

        var websiteClickRows = new StringBuilder();
        foreach (var (platform, clicks) in websiteClicksByPlatform)
        {
            var share = websiteClicksAllTime > 0 ? (int)Math.Round(clicks * 100.0 / websiteClicksAllTime) : 0;
            websiteClickRows.Append($"""
                <div class="promo-row">
                    <span>{H.Encode(platform)}</span>
                    <span class="muted">Visits to bookpromoterai.us</span>
                    <span><strong>{clicks}</strong> <span class="muted">({share}%)</span></span>
                </div>
                """);
        }
        if (websiteClicksByPlatform.Count == 0)
            websiteClickRows.Append("""<p class="muted">No website clicks tracked yet. Post BookPromoter AI brand promos — their sign-up and access-code links now carry per-platform tracking.</p>""");

        var websiteClickSection = $"""
            <p class="muted small-text">Visitors who clicked a brand promo's <strong>Create account</strong> or <strong>Access code</strong> link and landed on bookpromoterai.us, attributed to the platform that drove them.</p>
            <div class="analytics-summary-grid">
                <div class="analytics-card">
                    <span class="analytics-num">{websiteClicksThisMonth}</span>
                    <span class="analytics-label">Clicks This Month</span>
                </div>
                <div class="analytics-card">
                    <span class="analytics-num">{websiteClicksAllTime}</span>
                    <span class="analytics-label">Total Clicks (All Time)</span>
                </div>
                <div class="analytics-card">
                    <span class="analytics-num">{startClicks}</span>
                    <span class="analytics-label">Sign-up Page (/start)</span>
                </div>
                <div class="analytics-card">
                    <span class="analytics-num">{trialClicks}</span>
                    <span class="analytics-label">Access Code Page (/trial)</span>
                </div>
            </div>
            <div class="chart-wrap" style="margin-top:16px">
                <p class="muted small-text">Website clicks per month (last 6 months, stacked by platform).</p>
                {websiteChart}
            </div>
            <div class="promo-table" style="margin-top:16px">
                <div class="promo-header">
                    <strong>Platform</strong>
                    <strong>Source</strong>
                    <strong>Clicks (all time)</strong>
                </div>
                {websiteClickRows}
            </div>
            <h4 style="margin-top:20px">Clicks per platform &mdash; last 6 months</h4>
            {websiteMonthlyTable}
            """;

        var brandPostingLogSection = $"""
            <h3 style="margin-top:24px">Brand posting activity log</h3>
            <p class="muted small-text">Recent BookPromoter AI brand posts. Likes and clicks refresh from connected platforms when you open this page (about hourly). Author book promos use tracking links — see totals below.</p>
            <div class="promo-table brand-metrics-table">
                <div class="promo-header">
                    <strong>Post</strong>
                    <strong>When</strong>
                    <strong>Clicks</strong>
                    <strong>Likes</strong>
                    <strong>Status</strong>
                </div>
                {brandLogRows}
            </div>
            <h3 style="margin-top:24px">Book promo link clicks (all authors)</h3>
            <p class="muted small-text">Clicks on book store links attributed to each social platform via tracking URLs in posted ads.</p>
            <div class="promo-table">
                <div class="promo-header">
                    <strong>Platform</strong>
                    <strong>Source</strong>
                    <strong>Clicks</strong>
                </div>
                {promoClickRows}
            </div>
            """;

        var settings = store.Settings;
        var alreadyAdded = socialAccounts.Select(a => a.Platform).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var platformOptions = new StringBuilder();
        platformOptions.Append("""<option value="">Choose a platform...</option>""");
        foreach (var platform in SocialConnectHelper.DefaultPlatforms(settings, brandContext: true))
        {
            if (alreadyAdded.Contains(platform)) continue;
            if (!SocialPlatforms.AllowsBrandConnect(platform)) continue;
            platformOptions.Append(SocialConnectHelper.RenderPlatformOption(platform, settings: settings, brandContext: true));
        }

        var promoCards = new StringBuilder();
        if (promoAccounts.Count == 0)
        {
            promoCards.Append("""<p class="muted">Connect Facebook, Bluesky, X, LinkedIn, or another brand platform above to preview and post BookPromoter AI promos here.</p>""");
        }
        foreach (var account in promoAccounts)
        {
            var platform = account.Platform;
            if (!promoPosts.TryGetValue(platform, out var text)) continue;
            var copyId = $"app-promo-{platform.Replace(" ", "").Replace("(", "").Replace(")", "").ToLowerInvariant()}";
            var showsLogo = true;
            var logoBlock = showsLogo
                ? $"""<img src="{H.Encode(logoPreviewUrl)}" alt="BookPromoter AI logo" class="promo-logo-thumb">"""
                : "";
            var liveBadge = account.IsLiveConnection
                ? """<span class="status available">Live</span> """
                : """<span class="status used">Manual</span> """;
            promoCards.Append($"""
                <div class="promo-row plan-row">
                    <span>{liveBadge}{H.Encode(platform)}</span>
                    <span>
                        <div class="promo-preview-with-image">
                            {logoBlock}
                            <pre class="post-preview">{H.Encode(text)}</pre>
                        </div>
                        <textarea id="{copyId}" class="copy-source" readonly>{H.Encode(text)}</textarea>
                    </span>
                    <span>
                        <button class="button secondary small copy-button" type="button" onclick="copyPromoText('{copyId}', this)">Copy</button>
                        <form method="post" action="/owner/app-promo/post-social" class="inline-form tight">
                            <input type="hidden" name="platform" value="{H.Encode(platform)}">
                            <button class="button small" type="submit">Post</button>
                        </form>
                    </span>
                </div>
                """);
        }

        var historyRows = new StringBuilder();
        foreach (var update in store.ProductUpdates)
        {
            var emailed = update.EmailedAt is not null
                ? $"Emailed {update.EmailsSent} user(s)"
                : "Not emailed";
            var social = update.SocialPostsSent > 0 ? $", {update.SocialPostsSent} social post(s)" : "";
            historyRows.Append($"""
                <div class="promo-row">
                    <span>v{H.Encode(update.Version)}</span>
                    <span>{H.Encode(string.IsNullOrWhiteSpace(update.Title) ? update.CreatedAt.ToString("d MMM yyyy") : update.Title)}</span>
                    <span class="status available">{H.Encode(emailed)}{social}</span>
                </div>
                """);
        }
        if (store.ProductUpdates.Count == 0)
            historyRows.Append("""<p class="muted">No product updates published yet.</p>""");

        var sendGridNote = store.IsSendGridConfigured
            ? ""
            : """<p class="notice error">SendGrid is not configured — emails will not deliver until you add SendGrid variables in Railway.</p>""";

        var brandSettings = store.OwnerBrandMailingListSettings;
        var brandAutoSendChecked = brandSettings.AutoSendEnabled ? "checked" : "";
        var brandRequiresApprovalChecked = brandSettings.RequiresApproval ? "checked" : "";
        var brandAutoHint = AppStoreDb.FormatNextMailingHint(brandSettings) is string brandHint
            ? $"""<p class="muted small-text">{H.Encode(brandHint)}</p>"""
            : "";
        var brandPendingApproval = brandSettings.RequiresApproval
            && !brandSettings.PendingApproved
            && !string.IsNullOrWhiteSpace(brandSettings.PendingSubject);
        var brandApproveSection = brandPendingApproval
            ? """
                <p class="notice">Brand email draft ready — approve to allow auto-send.</p>
                <form method="post" action="/owner/brand-email/approve" class="inline-form" style="margin-top:8px">
                    <button class="button secondary" type="submit">Approve brand draft</button>
                </form>
                """
            : "";
        var brandDraftSubject = H.Encode(brandSettings.PendingSubject);
        var brandDraftBody = H.Encode(brandSettings.PendingBody);
        var brandSubscriberCount = store.OwnerBrandMailingListSubscriberCount;

        return $"""
            <section class="panel owner-settings" id="owner-section-website-analytics">
                <h2>Website Analytics &mdash; Clicks to BookPromoter AI</h2>
                {websiteClickSection}
            </section>

            <details class="owner-collapsible" id="owner-section-owner-social"{open("owner-social")}>
                <summary class="owner-collapsible-heading">BookPromoter AI Brand Social Accounts</summary>
                <div class="panel owner-settings">
                    <p class="muted">Connect social accounts for <strong>BookPromoter AI marketing only</strong> (app promos, release updates). These are separate from <strong>author accounts</strong> on My Account, which authors use to promote their books.</p>
                    {accountNote}
                    {authorOnlyNote}
                    <h3>Connected accounts</h3>
                    <div class="promo-table">
                        <div class="promo-header">
                            <strong>Platform</strong>
                            <strong>Account</strong>
                            <strong>Actions</strong>
                        </div>
                        {connectedRows}
                    </div>
                    <h3 style="margin-top:20px">Connect a brand platform</h3>
                    <p class="muted small-text">Connect buttons appear only for platforms that are configured and ready. Pick your <strong>bookpromoterai</strong> Tumblr blog on Owner; use <strong>My Account</strong> for author book promos on your personal blog.</p>
                    <div class="connect-buttons">
                        {SocialConnectHelper.ConnectButtons(returnPath, settings)}
                    </div>
                    <h3 style="margin-top:20px">Or add manually</h3>
                    <form method="post" action="/social-accounts" class="form">
                        <input type="hidden" name="return" value="{returnPath}">
                        <label>Platform
                            <select name="platform" onchange="toggleOwnerCustomPlatform(this)">{platformOptions}</select>
                        </label>
                        <label class="owner-custom-platform" style="display:none">Custom platform name
                            <input name="customPlatform" placeholder="e.g. Threads">
                        </label>
                        <label>Display Name
                            <input name="displayName" placeholder="BookPromoter AI" required>
                        </label>
                        <label>Handle
                            <input name="handle" placeholder="{BrandConstants.OfficialBlueskyHandle}" required>
                        </label>
                        <button class="button" type="submit">Add account</button>
                    </form>
                    {brandScheduleSection}
                    {brandPostingLogSection}
                </div>
            </details>

            <details class="owner-collapsible" id="owner-section-promote-app"{open("promote-app")}>
                <summary class="owner-collapsible-heading">Promote BookPromoter AI (Social &amp; Email)</summary>
                <div class="panel owner-settings">
                    <p class="muted">Generate ready-to-share posts that promote BookPromoter AI. <strong>Captions rotate automatically each ISO week</strong> — auto-post and manual Post use this week&apos;s variation. Click <strong>Shuffle previews</strong> to try a different caption before posting.</p>
                    {accountNote}
                    {authorOnlyNote}
                    {sendGridNote}
                    <p class="muted small-text">Only <strong>connected brand accounts</strong> appear below. Enable <strong>Auto-post</strong> under Brand Social Accounts, or email registered users on the <strong>brand mailing list</strong> ({brandSubscriberCount} subscriber(s) — separate from author reader lists).</p>
                    <div class="promo-table">
                        <div class="promo-header">
                            <strong>Platform</strong>
                            <strong>Post preview</strong>
                            <strong>Actions</strong>
                        </div>
                        {promoCards}
                    </div>
                    <div class="form-actions" style="margin-top:12px">
                        <a class="button secondary" href="/owner-promos?section=promote-app&amp;shuffle=1">Shuffle previews</a>
                    </div>
                    <form method="post" action="/owner/app-promo/post-social" class="inline-form" style="margin-top:12px">
                        <button class="button secondary" type="submit">Post to all connected accounts</button>
                    </form>

                    <h3 style="margin-top:24px">Brand email auto-send</h3>
                    <p class="muted small-text">Auto-generate BookPromoter AI promo emails to <strong>registered users</strong> (not author reader lists). Checks every 5 minutes.</p>
                    {sendGridNote}
                    <form method="post" action="/owner/brand-email/schedule" class="form">
                        <label>Emails per week
                            <input name="emailsPerWeek" type="number" min="0" max="7" value="{brandSettings.EmailsPerWeek}">
                        </label>
                        <label class="checkbox">
                            <input name="autoSendEnabled" type="checkbox" {brandAutoSendChecked}>
                            Auto-send to registered users
                        </label>
                        <label class="checkbox">
                            <input name="requiresApproval" type="checkbox" {brandRequiresApprovalChecked}>
                            Approval required before sending
                        </label>
                        <button class="button secondary" type="submit">Save brand email schedule</button>
                    </form>
                    {brandAutoHint}
                    {brandApproveSection}
                    <form method="post" action="/owner/brand-email/generate" class="inline-form" style="margin-top:12px">
                        <button class="button secondary" type="submit">Auto-generate brand email</button>
                    </form>

                    <h3 style="margin-top:24px">Email all users (promo)</h3>
                    <form method="post" action="/owner/app-promo/email" class="form">
                        <label>Subject
                            <input name="subject" value="{brandDraftSubject}" required placeholder="Promote your books smarter with BookPromoter AI">
                        </label>
                        <label>Message
                            <textarea name="body" rows="6" required>{brandDraftBody}</textarea>
                        </label>
                        <button class="button" type="submit">Send to {brandSubscriberCount} brand subscriber(s)</button>
                    </form>
                </div>
            </details>

            <details class="owner-collapsible" id="owner-section-product-updates"{open("product-updates")}>
                <summary class="owner-collapsible-heading">Product Updates (email users on release)</summary>
                <div class="panel owner-settings">
                    <p class="muted">When you ship a new version, list what changed. The form below fills from <code>ReleaseNotes.json</code> on every deploy (keep it in sync with <code>BookPromoterAI.csproj</code>). Users receive a structured email with <strong>Updated</strong>, <strong>New</strong>, and <strong>Added</strong> sections.</p>
                    {draftNotice}
                    {sendGridNote}
                    <form method="post" action="/owner/product-update/publish" class="form">
                        <label>Version
                            <input name="version" value="{H.Encode(version)}" required placeholder="1.5.0">
                        </label>
                        <label>Email subject (optional — defaults to version headline)
                            <input name="title" value="{H.Encode(title)}" placeholder="BookPromoter AI v1.5.0 — What's new">
                        </label>
                        <label>Updated (one item per line)
                            <textarea name="updatedItems" rows="4" placeholder="Stripe billing now live&#10;Custom domain support">{H.Encode(draft.UpdatedText)}</textarea>
                        </label>
                        <label>New (one item per line)
                            <textarea name="createdItems" rows="4" placeholder="Go Live checklist on Owner page">{H.Encode(draft.NewText)}</textarea>
                        </label>
                        <label>Added (one item per line)
                            <textarea name="addedItems" rows="4" placeholder="Self-promotion tools for owner">{H.Encode(draft.AddedText)}</textarea>
                        </label>
                        <label class="checkbox-label"><input type="checkbox" name="sendEmail" value="true" checked> Email all registered users</label>
                        <label class="checkbox-label"><input type="checkbox" name="postToSocial" value="true"> Post update to connected social accounts</label>
                        <button class="button" type="submit">Publish update</button>
                    </form>

                    <h3 style="margin-top:24px">Recent updates</h3>
                    <div class="promo-table">
                        <div class="promo-header">
                            <strong>Version</strong>
                            <strong>Title / date</strong>
                            <strong>Delivery</strong>
                        </div>
                        {historyRows}
                    </div>
                </div>
            </details>

            {CopyScript()}
            {OwnerCustomPlatformScript()}
            """;
    }

    // Last 6 calendar months (oldest → newest) as (yyyy-MM, "MMM yyyy").
    static List<(string Key, string Label)> RecentMonths()
    {
        var months = new List<(string, string)>();
        for (var i = 5; i >= 0; i--)
        {
            var d = DateTime.UtcNow.AddMonths(-i);
            months.Add((d.ToString("yyyy-MM"), d.ToString("MMM yyyy")));
        }
        return months;
    }

    // Stacked SVG bar chart: total website clicks per month, coloured by platform.
    static string BuildWebsiteClickChart(
        Dictionary<string, Dictionary<string, int>> byMonth,
        List<(string Key, string Label)> months)
    {
        var platforms = byMonth.Values.SelectMany(m => m.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (platforms.Count == 0)
            return """<p class="muted">No website clicks tracked yet.</p>""";

        var colours = new[] { "#0f766e", "#6366f1", "#f59e0b", "#ef4444", "#10b981", "#8b5cf6", "#f97316", "#06b6d4", "#84cc16", "#ec4899" };
        var chartWidth = 560;
        var chartHeight = 220;
        var barAreaWidth = chartWidth - 60;
        var barWidth = (int)(barAreaWidth / months.Count * 0.6);
        var gap = (int)(barAreaWidth / months.Count);

        int ClicksFor(string monthKey, string platform) =>
            byMonth.TryGetValue(monthKey, out var p) && p.TryGetValue(platform, out var v) ? v : 0;

        var monthlyTotals = months.Select(m => platforms.Sum(p => ClicksFor(m.Key, p))).ToList();
        var maxTotal = Math.Max(1, monthlyTotals.Max());

        var bars = new StringBuilder();
        var labels = new StringBuilder();
        for (var mi = 0; mi < months.Count; mi++)
        {
            var (key, label) = months[mi];
            var x = 50 + mi * gap + (gap - barWidth) / 2;
            var stackY = chartHeight - 30;
            labels.Append($"""<text x="{x + barWidth / 2}" y="{chartHeight - 10}" text-anchor="middle" font-size="11" fill="#667085">{H.Encode(label[..3])}</text>""");
            for (var pi = 0; pi < platforms.Count; pi++)
            {
                var clicks = ClicksFor(key, platforms[pi]);
                if (clicks == 0) continue;
                var barH = Math.Max(1, (int)((double)clicks / maxTotal * (chartHeight - 50)));
                stackY -= barH;
                var colour = colours[pi % colours.Length];
                bars.Append($"""<rect x="{x}" y="{stackY}" width="{barWidth}" height="{barH}" fill="{colour}" rx="2"><title>{H.Encode(platforms[pi])}: {clicks} clicks in {label}</title></rect>""");
            }
        }

        var yAxis = new StringBuilder();
        for (var tick = 0; tick <= 4; tick++)
        {
            var val = (int)(maxTotal * tick / 4.0);
            var y = chartHeight - 30 - (int)((double)tick / 4 * (chartHeight - 50));
            yAxis.Append($"""
                <line x1="45" y1="{y}" x2="{chartWidth - 10}" y2="{y}" stroke="#d7dde8" stroke-width="1"/>
                <text x="40" y="{y + 4}" text-anchor="end" font-size="10" fill="#667085">{val}</text>
                """);
        }

        var legend = new StringBuilder();
        for (var pi = 0; pi < Math.Min(platforms.Count, colours.Length); pi++)
        {
            legend.Append($"""
                <div class="chart-legend-item">
                    <span class="chart-legend-dot" style="background:{colours[pi % colours.Length]}"></span>
                    <span>{H.Encode(platforms[pi])}</span>
                </div>
                """);
        }

        return $"""
            <svg viewBox="0 0 {chartWidth} {chartHeight}" xmlns="http://www.w3.org/2000/svg" class="bar-svg">
                {yAxis}
                {bars}
                {labels}
            </svg>
            <div class="chart-legend">{legend}</div>
            """;
    }

    // Platform rows × month columns breakdown with totals.
    static string BuildWebsiteMonthlyTable(
        Dictionary<string, Dictionary<string, int>> byMonth,
        List<(string Key, string Label)> months,
        List<string> platforms)
    {
        if (platforms.Count == 0)
            return """<p class="muted">No website clicks tracked yet.</p>""";

        int ClicksFor(string monthKey, string platform) =>
            byMonth.TryGetValue(monthKey, out var p) && p.TryGetValue(platform, out var v) ? v : 0;

        var header = new StringBuilder("<tr><th>Platform</th>");
        foreach (var (_, label) in months)
            header.Append($"<th>{H.Encode(label[..3])}</th>");
        header.Append("<th>Total</th></tr>");

        var rows = new StringBuilder();
        foreach (var platform in platforms)
        {
            rows.Append($"<tr><td>{H.Encode(platform)}</td>");
            var rowTotal = 0;
            foreach (var (key, _) in months)
            {
                var v = ClicksFor(key, platform);
                rowTotal += v;
                rows.Append($"<td>{(v > 0 ? v.ToString() : "")}</td>");
            }
            rows.Append($"<td><strong>{rowTotal}</strong></td></tr>");
        }

        rows.Append("<tr class=\"totals-row\"><td><strong>Total</strong></td>");
        var grandTotal = 0;
        foreach (var (key, _) in months)
        {
            var colTotal = platforms.Sum(p => ClicksFor(key, p));
            grandTotal += colTotal;
            rows.Append($"<td><strong>{(colTotal > 0 ? colTotal.ToString() : "")}</strong></td>");
        }
        rows.Append($"<td><strong>{grandTotal}</strong></td></tr>");

        return $"""
            <div class="analytics-table-scroll">
                <table class="analytics-month-table">
                    <thead>{header}</thead>
                    <tbody>{rows}</tbody>
                </table>
            </div>
            """;
    }

    static string OwnerCustomPlatformScript() => """
        <script>
        function toggleOwnerCustomPlatform(select) {
            var custom = document.querySelector('.owner-custom-platform');
            if (custom) custom.style.display = select.value === '__custom__' ? 'block' : 'none';
        }
        </script>
        """;

    static string CopyScript() => """
        <script>
        function copyPromoText(textareaId, button) {
            var textarea = document.getElementById(textareaId);
            if (!textarea) return;
            var text = textarea.value;
            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(text).then(function () {
                    button.textContent = 'Copied!';
                    button.classList.add('copied');
                    setTimeout(function () { button.textContent = 'Copy'; button.classList.remove('copied'); }, 2000);
                });
            } else {
                textarea.style.position = 'static'; textarea.style.opacity = '1';
                textarea.select(); document.execCommand('copy');
            }
        }
        </script>
        """;
}
