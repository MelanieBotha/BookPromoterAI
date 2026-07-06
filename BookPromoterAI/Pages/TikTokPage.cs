using System.Text;

namespace BookPromoterAI;

static class TikTokPage
{
    public static string Render(AppStoreDb store, AppSettings settings, string notice = "")
    {
        var account = store.TikTokAccount;
        var videos = store.TikTokVideos;
        var books = store.Books;

        var connectSection = account is null
            ? BuildConnectSection(settings)
            : $"""
                <div class="notice success">
                    Connected as <strong>{H.Encode(account.DisplayName)}</strong> (@{H.Encode(account.Handle)})
                    · <a href="/social-accounts/connect/TikTok?return=/tiktok">Reconnect</a>
                </div>
                """;

        var bookOptions = new StringBuilder();
        bookOptions.Append("""<option value="">Choose a book (optional)...</option>""");
        foreach (var book in books)
            bookOptions.Append($"""<option value="{book.Id}">{H.Encode(book.Title)}</option>""");

        var videoRows = new StringBuilder();
        foreach (var video in videos)
        {
            var statusClass = video.Status switch
            {
                TikTokVideoStatuses.Sent => "available",
                TikTokVideoStatuses.Failed => "used",
                _ => "used"
            };
            var posted = video.PostedAt is not null
                ? $"""<small class="muted">Sent {H.Encode(AppTimeZone.FormatWithZone(video.PostedAt.Value, "MMM d, HH:mm"))}</small>"""
                : "";
            var error = !string.IsNullOrWhiteSpace(video.ErrorMessage)
                ? $"""<p class="notice error small-text">{H.Encode(video.ErrorMessage)}</p>"""
                : "";
            var postButton = video.Status == TikTokVideoStatuses.Draft && account?.IsLiveConnection == true
                ? $"""<form method="post" action="/tiktok/post/{video.Id}" style="display:inline"><button class="button small" type="submit">Send to TikTok inbox</button></form>"""
                : "";
            var deleteForm = $"""
                <form method="post" action="/tiktok/delete/{video.Id}" style="display:inline" onsubmit="return confirm('Remove this video?');">
                    <button class="danger-button small" type="submit">Remove</button>
                </form>
                """;

            videoRows.Append($"""
                <article class="book-row tiktok-video-row">
                    <div>
                        <strong>{H.Encode(video.Title)}</strong>
                        <p class="muted">{H.Encode(video.BookTitle)}</p>
                        {posted}
                        <small class="status {statusClass}">{H.Encode(video.Status)}</small>
                        {error}
                    </div>
                    <div class="tiktok-video-preview">
                        <video src="{H.Encode(video.VideoUrl)}" controls preload="metadata" class="tiktok-player"></video>
                    </div>
                    <div class="row-actions">
                        {postButton}
                        {deleteForm}
                    </div>
                </article>
                """);
        }

        if (videos.Count == 0)
            videoRows.Append("""<p class="muted">No book promo videos yet. Upload a vertical video (MP4/MOV, 9:16) below.</p>""");

        var apiNote = settings.IsTikTokConfigured
            ? """<p class="muted small-text">Videos upload to your <strong>TikTok inbox</strong> — finish editing and publish in the TikTok app. Until TikTok approves the app for public posting, videos may be private (sandbox mode).</p>"""
            : """<p class="notice">TikTok API credentials are not configured yet. You can still upload videos here; connect and post once the owner adds <code>TikTok__ClientKey</code> and <code>TikTok__ClientSecret</code> on Railway.</p>""";

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">BookTok</p>
                    <h1>TikTok video promos</h1>
                    <p class="muted">Upload short vertical videos to promote your books. Text posts stay in the Ad Library — TikTok lives here.</p>
                </div>
            </section>
            {notice}
            <section class="panel">
                <h2>TikTok account</h2>
                {connectSection}
                {apiNote}
            </section>
            <section class="panel">
                <h2>Upload a book promo video</h2>
                <p class="muted small-text">Recommended: 9:16 vertical, 15–60 seconds, MP4 or MOV, under 1 GB. Add a title and optional caption for TikTok.</p>
                <form method="post" action="/tiktok/upload" enctype="multipart/form-data" class="form">
                    <label>Book
                        <select name="bookId">{bookOptions}</select>
                    </label>
                    <label>Video title
                        <input name="title" placeholder="e.g. New fantasy release — Jenny's Legacy" required maxlength="150">
                    </label>
                    <label>Caption (optional)
                        <textarea name="caption" rows="3" placeholder="Hook line or hashtags for TikTok"></textarea>
                    </label>
                    <label>Video file (MP4, MOV, WEBM, AVI — max 1 GB)
                        <input type="file" name="video" accept="video/mp4,video/quicktime,video/webm,video/x-msvideo" required>
                    </label>
                    <button class="button" type="submit" style="background:#000">Upload video</button>
                </form>
            </section>
            <section class="panel">
                <h2>Your TikTok videos</h2>
                <div class="tiktok-video-list">
                    {videoRows}
                </div>
            </section>
            """;
    }

    static string BuildConnectSection(AppSettings settings)
    {
        if (!settings.IsTikTokConfigured)
        {
            return """
                <p class="muted">Connect your TikTok creator account to send videos to your TikTok inbox.</p>
                <p class="notice">Waiting on TikTok API setup — the app owner must register at <strong>developers.tiktok.com</strong> and add credentials to Railway.</p>
                """;
        }

        return $"""
            <p class="muted">Connect your TikTok creator account to send book promo videos to your TikTok inbox.</p>
            <a class="button" href="/social-accounts/connect/TikTok?return={Uri.EscapeDataString(SocialConnectHelper.TikTokReturnPath)}" style="background:#000">Connect TikTok</a>
            """;
    }
}
