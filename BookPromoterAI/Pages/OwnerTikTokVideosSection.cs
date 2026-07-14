using System.Text;

namespace BookPromoterAI;

/// <summary>Owner → App Videos: BookPromoter AI brand TikTok promos (logo + narration).</summary>
static class OwnerTikTokVideosSection
{
    public static string Render(AppStoreDb store, string appBaseUrl, string? activeSection = null, string noticeHtml = "")
    {
        if (!store.IsOwner) return "";

        store.EnsureBrandTikTokSchedule();
        var queued = store.EnsureBrandWeeklyVideos(appBaseUrl);
        if (string.IsNullOrWhiteSpace(noticeHtml) && queued > 0)
            noticeHtml = $"""<div class="notice success">Queued {queued} BookPromoter AI promo video(s) for this week.</div>""";

        var openAttr = string.Equals(activeSection, "owner-videos", StringComparison.OrdinalIgnoreCase) ? " open" : "";
        var (weekNum, yearNum, weekLabel) = AdWeek.For(DateTime.UtcNow);
        var thisWeek = store.BrandTikTokVideosThisWeek;
        var all = store.BrandTikTokVideos
            .Where(v => !(v.WeekNumber == weekNum && v.WeekYear == yearNum))
            .ToList();
        var account = store.BrandTikTokAccount;
        var live = account?.IsLiveConnection == true;
        var (videosPerWeek, sentThisWeek, generatedThisWeek, autoPost) = store.GetBrandTikTokWeekQuota();
        var connectHref = $"/social-accounts/connect/TikTok?return={Uri.EscapeDataString(SocialConnectHelper.OwnerVideosReturnPath)}";

        var removeBtn = account is not null
            ? $"""
                <form method="post" action="/social-accounts/delete/{account.Id}" onsubmit="return confirm('Disconnect brand TikTok?');" style="margin:0;display:inline">
                    <input type="hidden" name="return" value="{H.Encode(SocialConnectHelper.OwnerVideosReturnPath)}">
                    <button type="submit" class="danger-button small">Remove TikTok</button>
                </form>
                """
            : "";

        var thisWeekRows = new StringBuilder();
        foreach (var video in thisWeek)
            thisWeekRows.Append(RenderRow(video, live, store.IsTikTokConfigured));
        if (thisWeek.Count == 0)
            thisWeekRows.Append("""<p class="muted">No brand videos this week yet. Set videos/week above 0 and click Generate.</p>""");

        var allRows = new StringBuilder();
        foreach (var video in all)
            allRows.Append(RenderRow(video, live, store.IsTikTokConfigured));
        if (all.Count == 0)
            allRows.Append("""<p class="muted">Earlier brand videos will appear here.</p>""");

        var weekActionLabel = thisWeek.Count == 0 ? "Generate this week's videos" : "Regenerate this week's videos";
        var weekConfirm = thisWeek.Count == 0 ? "" : "Replace this week's brand videos?";

        return $"""
            <details class="owner-collapsible" id="owner-section-owner-videos"{openAttr}>
                <summary class="owner-collapsible-heading">App Videos (TikTok)</summary>
                <div class="panel owner-settings">
                    <p class="muted">60-second BookPromoter AI promo videos with the app logo and narrated pitch. Send to the <strong>brand</strong> TikTok inbox (separate from author book videos on the Videos tab).</p>
                    {noticeHtml}
                    {(!store.IsTikTokConfigured
                        ? """<div class="notice error">Configure TikTok__ClientKey / TikTok__ClientSecret first (Owner → Social Media APIs).</div>"""
                        : $"""
                            <h3>Brand TikTok account</h3>
                            {(account is not null
                                ? $"""<p class="muted">Connected as <strong>{H.Encode(string.IsNullOrWhiteSpace(account.DisplayName) ? account.Handle : account.DisplayName)}</strong>.</p>"""
                                : """<p class="muted">Connect the BookPromoter AI TikTok account to push promo videos to its inbox.</p>""")}
                            <div class="row-actions" style="gap:0.75rem;flex-wrap:wrap;margin:0.75rem 0">
                                <a class="button small" style="background:#000" href="{connectHref}">{(account is not null ? "Reconnect TikTok" : "Connect TikTok")}</a>
                                {removeBtn}
                            </div>
                            """)}
                    <h3>Weekly schedule</h3>
                    <p class="muted small-text">Owner brand cap: up to <strong>7 videos/week</strong> (not author plan limits). This week: <strong>{generatedThisWeek}</strong> generated, <strong>{sentThisWeek}</strong> sent.</p>
                    <form method="post" action="/owner/videos/schedule" class="form" style="margin:0.75rem 0">
                        <label>Videos per week
                            <input name="videosPerWeek" type="number" min="0" max="7" value="{videosPerWeek}" required>
                        </label>
                        <label class="checkbox-row">
                            <input type="checkbox" name="autoPost" value="1" {(autoPost ? "checked" : "")}>
                            Auto-send Ready brand videos to TikTok inbox
                        </label>
                        <button class="button" type="submit">Save app video schedule</button>
                    </form>
                    <h3>{H.Encode(weekLabel)}</h3>
                    <div class="row-actions" style="margin:0.75rem 0">
                        <form method="post" action="/owner/videos/regenerate-week" onsubmit="{(string.IsNullOrEmpty(weekConfirm) ? "" : $"return confirm('{H.Encode(weekConfirm)}');")}">
                            <button class="button secondary" type="submit">{H.Encode(weekActionLabel)}</button>
                        </form>
                    </div>
                    <div class="tiktok-video-list">{thisWeekRows}</div>
                    <h3 style="margin-top:1.5rem">Earlier brand videos</h3>
                    <div class="tiktok-video-list">{allRows}</div>
                </div>
            </details>
            {ClientScript()}
            """;
    }

    static string RenderRow(TikTokVideo video, bool tiktokConnected, bool tiktokConfigured)
    {
        var statusLabel = video.Status switch
        {
            TikTokVideoStatuses.Ready => tiktokConnected ? "Ready to post or download" : "Ready to download",
            TikTokVideoStatuses.Rendering => "Generating…",
            TikTokVideoStatuses.Failed => "Failed",
            TikTokVideoStatuses.Sent => "Sent to TikTok inbox",
            _ => video.Status
        };
        var statusClass = video.Status switch
        {
            TikTokVideoStatuses.Ready => "available",
            TikTokVideoStatuses.Rendering => "pending",
            TikTokVideoStatuses.Failed => "used",
            TikTokVideoStatuses.Sent => "available",
            _ => "available"
        };
        var canDownload = !string.IsNullOrWhiteSpace(video.VideoUrl)
            && video.Status is TikTokVideoStatuses.Ready or TikTokVideoStatuses.Sent;
        var preview = canDownload
            ? $"""<video src="{H.Encode(video.VideoUrl)}" controls preload="metadata" class="tiktok-player"></video>"""
            : video.Status == TikTokVideoStatuses.Rendering
                ? """<div class="tiktok-video-placeholder muted">Rendering app promo…</div>"""
                : """<div class="tiktok-video-placeholder muted">No preview</div>""";
        var download = canDownload
            ? $"""<a class="button small" href="{H.Encode(video.VideoUrl)}" download="BookPromoterAI-promo.mp4">Download</a>"""
            : "";
        var push = "";
        if (video.Status == TikTokVideoStatuses.Ready && canDownload && tiktokConnected)
            push = $"""<button type="button" class="button small" style="background:#000" onclick="ownerPostTikTok({video.Id}, this)">Push to TikTok</button>""";
        else if (video.Status == TikTokVideoStatuses.Sent && tiktokConnected && canDownload)
            push = $"""<button type="button" class="button small secondary" onclick="ownerPostTikTok({video.Id}, this)">Push again</button>""";
        else if (canDownload && tiktokConfigured && !tiktokConnected)
            push = $"""<a class="button small" style="background:#000" href="/social-accounts/connect/TikTok?return={Uri.EscapeDataString(SocialConnectHelper.OwnerVideosReturnPath)}">Connect to push</a>""";

        var retry = video.Status == TikTokVideoStatuses.Failed && video.AutoGenerated
            ? $"""<button type="button" class="button small secondary" onclick="ownerRetryVideo({video.Id}, this)">Retry</button>"""
            : "";
        var regenerate = video.Status is TikTokVideoStatuses.Ready or TikTokVideoStatuses.Sent && video.AutoGenerated
            ? $"""<button type="button" class="button small secondary" onclick="ownerRegenVideo({video.Id}, this)">Regenerate</button>"""
            : "";
        var errorNote = !string.IsNullOrWhiteSpace(video.ErrorMessage)
            ? $"""<p class="notice error small-text">{H.Encode(video.ErrorMessage)}</p>"""
            : "";

        return $"""
            <article class="book-row tiktok-video-row" id="owner-video-{video.Id}">
                <div>
                    <strong>{H.Encode(video.Title)}</strong>
                    <p class="muted">BookPromoter AI</p>
                    <small class="status {statusClass}">{H.Encode(statusLabel)}</small>
                    {errorNote}
                    <p class="muted small-text tiktok-caption-preview">{H.Encode(video.Caption)}</p>
                </div>
                <div class="tiktok-video-preview">{preview}</div>
                <div class="row-actions">
                    {push}
                    {download}
                    {retry}
                    {regenerate}
                    <form method="post" action="/owner/videos/delete/{video.Id}" style="display:inline" onsubmit="return confirm('Remove this brand video?');">
                        <button type="submit" class="danger-button small">Remove</button>
                    </form>
                </div>
            </article>
            """;
    }

    static string ClientScript() => """
        <script>
        async function ownerPostTikTok(id, btn) {
            if (btn) { btn.disabled = true; btn.textContent = 'Sending…'; }
            try {
                var res = await fetch('/owner/videos/post/' + id + '?ajax=1', { method: 'POST' });
                var data = await res.json();
                if (data.ok) location.href = '/owner-promos?section=owner-videos&posted=1';
                else { alert(data.error || 'Push failed'); if (btn) { btn.disabled = false; btn.textContent = 'Push to TikTok'; } }
            } catch (e) { alert('Push failed'); if (btn) { btn.disabled = false; btn.textContent = 'Push to TikTok'; } }
        }
        async function ownerRetryVideo(id, btn) {
            if (btn) btn.disabled = true;
            await fetch('/owner/videos/retry/' + id + '?ajax=1', { method: 'POST' });
            location.reload();
        }
        async function ownerRegenVideo(id, btn) {
            if (btn) btn.disabled = true;
            await fetch('/owner/videos/regenerate/' + id + '?ajax=1', { method: 'POST' });
            location.reload();
        }
        </script>
        """;
}
