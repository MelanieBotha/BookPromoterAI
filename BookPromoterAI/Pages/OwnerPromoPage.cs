using System.Text;
namespace BookPromoterAI;

static class OwnerPromoPage
{
    public static string Render(AppStoreDb store, string appBaseUrl, ReleaseNotesCatalog? releaseNotes = null, string? activeSection = null)
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

        var promoSeed = Environment.TickCount ^ Random.Shared.Next();
        var promoPosts = AppPromoGenerator.GeneratePromoPosts(appBaseUrl, promoSeed);
        var logoPreviewUrl = PostBranding.LogoUrlForSite(appBaseUrl);
        var socialAccounts = store.OwnerSocialAccounts;
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
        foreach (var platform in SocialConnectHelper.DefaultPlatforms)
        {
            if (alreadyAdded.Contains(platform)) continue;
            platformOptions.Append(SocialConnectHelper.RenderPlatformOption(platform, settings: settings));
        }
        platformOptions.Append("""<option value="__custom__">Other (type your own)...</option>""");

        var promoCards = new StringBuilder();
        foreach (var platform in AppPromoGenerator.SupportedPlatforms)
        {
            var text = promoPosts[platform];
            var copyId = $"app-promo-{platform.Replace(" ", "").ToLowerInvariant()}";
            var showsLogo = true;
            var logoBlock = showsLogo
                ? $"""<img src="{H.Encode(logoPreviewUrl)}" alt="BookPromoter AI logo" class="promo-logo-thumb">"""
                : "";
            promoCards.Append($"""
                <div class="promo-row plan-row">
                    <span>{H.Encode(platform)}</span>
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
            <details class="owner-collapsible" id="owner-section-owner-social"{open("owner-social")}>
                <summary class="owner-collapsible-heading">BookPromoter AI Brand Social Accounts</summary>
                <div class="panel owner-settings">
                    <p class="muted">Connect social accounts for <strong>BookPromoter AI marketing only</strong> (app promos, release updates). These are separate from <strong>author accounts</strong> on My Account, which authors use to promote their books.</p>
                    {accountNote}
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
                    <p class="muted small-text">Facebook, Bluesky, X, and LinkedIn support live posting when connected.</p>
                    {SocialConnectHelper.NextPlatformHint(settings)}
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
                    <p class="muted">Generate ready-to-share posts that promote BookPromoter AI. Each preview uses a random caption — refresh the page or click <strong>Shuffle previews</strong> for a new variation. Copy, post manually, enable <strong>Auto-post</strong> under Brand Social Accounts, or email registered users on the <strong>brand mailing list</strong> ({brandSubscriberCount} subscriber(s) — separate from author reader lists).</p>
                    {accountNote}
                    {sendGridNote}
                    <p class="muted small-text">All brand posts attach the BookPromoter AI logo image automatically on live platforms.</p>
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
