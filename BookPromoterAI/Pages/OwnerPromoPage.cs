using System.Text;
namespace BookPromoterAI;

static class OwnerPromoPage
{
    public static string Render(AppStoreDb store, string appBaseUrl)
    {
        var promoPosts = AppPromoGenerator.GeneratePromoPosts(appBaseUrl);
        var socialAccounts = store.OwnerSocialAccounts;
        var accountNote = socialAccounts.Count > 0
            ? $"""<p class="notice success">{socialAccounts.Count} social account(s) connected — posts will go to: {H.Encode(string.Join(", ", socialAccounts.Select(a => a.Platform)))}.</p>"""
            : """<p class="notice error">No social accounts connected yet. Connect platforms under <strong>My Account</strong> to auto-post app promotions.</p>""";

        var promoCards = new StringBuilder();
        foreach (var platform in AppPromoGenerator.SupportedPlatforms)
        {
            var text = promoPosts[platform];
            var copyId = $"app-promo-{platform.Replace(" ", "").ToLowerInvariant()}";
            promoCards.Append($"""
                <div class="promo-row plan-row">
                    <span>{H.Encode(platform)}</span>
                    <span><textarea id="{copyId}" class="copy-source" readonly>{H.Encode(text)}</textarea>
                        <pre class="post-preview">{H.Encode(text)}</pre>
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

        var defaultPromoEmail = $"""
            Hi,

            BookPromoter AI helps authors promote books with AI-generated social posts, click tracking, and a weekly Ad Library.

            Start here: {appBaseUrl.TrimEnd('/')}/start
            Free access code: {appBaseUrl.TrimEnd('/')}/trial

            — The BookPromoter AI Team
            """;

        return $"""
            <details class="owner-collapsible" open>
                <summary class="owner-collapsible-heading">Promote BookPromoter AI (Social &amp; Email)</summary>
                <div class="panel owner-settings">
                    <p class="muted">Generate ready-to-share posts that promote BookPromoter AI. Copy to your social accounts, post to connected accounts, or email all {store.RegisteredUserCount} registered user(s).</p>
                    {accountNote}
                    {sendGridNote}
                    <div class="promo-table">
                        <div class="promo-header">
                            <strong>Platform</strong>
                            <strong>Post preview</strong>
                            <strong>Actions</strong>
                        </div>
                        {promoCards}
                    </div>
                    <form method="post" action="/owner/app-promo/post-social" class="inline-form" style="margin-top:12px">
                        <button class="button secondary" type="submit">Post to all connected accounts</button>
                    </form>

                    <h3 style="margin-top:24px">Email all users (promo)</h3>
                    <form method="post" action="/owner/app-promo/email" class="form">
                        <label>Subject
                            <input name="subject" value="Promote your books smarter with BookPromoter AI" required>
                        </label>
                        <label>Message
                            <textarea name="body" rows="6" required>{H.Encode(defaultPromoEmail.Trim())}</textarea>
                        </label>
                        <button class="button" type="submit">Send to all {store.RegisteredUserCount} users</button>
                    </form>
                </div>
            </details>

            <details class="owner-collapsible" open>
                <summary class="owner-collapsible-heading">Product Updates (email users on release)</summary>
                <div class="panel owner-settings">
                    <p class="muted">When you ship a new version, list what changed. Users receive a structured email with <strong>Updated</strong>, <strong>New</strong>, and <strong>Added</strong> sections. Optionally post to your connected social accounts too.</p>
                    {sendGridNote}
                    <form method="post" action="/owner/product-update/publish" class="form">
                        <label>Version
                            <input name="version" value="{H.Encode(AppVersion.Display)}" required placeholder="1.5.0">
                        </label>
                        <label>Email subject (optional — defaults to version headline)
                            <input name="title" placeholder="BookPromoter AI v1.5.0 — What's new">
                        </label>
                        <label>Updated (one item per line)
                            <textarea name="updatedItems" rows="4" placeholder="Stripe billing now live&#10;Custom domain support"></textarea>
                        </label>
                        <label>New (one item per line)
                            <textarea name="createdItems" rows="4" placeholder="Go Live checklist on Owner page"></textarea>
                        </label>
                        <label>Added (one item per line)
                            <textarea name="addedItems" rows="4" placeholder="Self-promotion tools for owner"></textarea>
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
            """;
    }

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
