using System.Text;
using System.Text.Json;

namespace BookPromoterAI;

static class TikTokPage
{
    public static string Render(AppStoreDb store, PostGenerator generator, string notice = "")
    {
        var videos = store.TikTokVideos;
        var books = store.Books;

        var bookOptions = new StringBuilder();
        var booksData = new List<object>();
        bookOptions.Append("""<option value="">Choose a book...</option>""");
        foreach (var book in books)
        {
            var purchaseUrl = book.Links.FirstOrDefault()?.Url ?? "";
            var caption = generator.GenerateTikTokCaption(book, purchaseUrl, book.PostVariantSeed);
            bookOptions.Append($"""<option value="{book.Id}">{H.Encode(book.Title)}</option>""");
            booksData.Add(new
            {
                id = book.Id,
                title = book.Title,
                author = book.AuthorName,
                coverUrl = string.IsNullOrWhiteSpace(book.CoverImageUrl) ? "" : book.CoverImageUrl,
                caption
            });
        }

        var booksJson = JsonSerializer.Serialize(booksData);
        var hasBooks = books.Count > 0;

        var videoRows = new StringBuilder();
        foreach (var video in videos)
        {
            var statusLabel = video.Status switch
            {
                TikTokVideoStatuses.Ready => "Ready to post",
                TikTokVideoStatuses.Sent => "Posted",
                _ => video.Status
            };
            var captionBlock = !string.IsNullOrWhiteSpace(video.Caption)
                ? $"""
                    <p class="muted small-text tiktok-caption-preview">{H.Encode(video.Caption)}</p>
                    <button type="button" class="button small secondary" onclick="copyTikTokCaption(this)" data-caption="{H.Encode(video.Caption)}">Copy caption</button>
                    """
                : "";

            videoRows.Append($"""
                <article class="book-row tiktok-video-row">
                    <div>
                        <strong>{H.Encode(video.Title)}</strong>
                        <p class="muted">{H.Encode(video.BookTitle)}</p>
                        <small class="status available">{H.Encode(statusLabel)}</small>
                        {captionBlock}
                    </div>
                    <div class="tiktok-video-preview">
                        <video src="{H.Encode(video.VideoUrl)}" controls preload="metadata" class="tiktok-player"></video>
                    </div>
                    <div class="row-actions">
                        <a class="button small" href="{H.Encode(video.VideoUrl)}" download>Download</a>
                        <form method="post" action="/videos/delete/{video.Id}" style="display:inline" onsubmit="return confirm('Remove this video?');">
                            <button class="danger-button small" type="submit">Remove</button>
                        </form>
                    </div>
                </article>
                """);
        }

        if (videos.Count == 0)
            videoRows.Append("""<p class="muted">No videos yet. Create one below from a book cover and AI caption.</p>""");

        var noBooksNotice = hasBooks
            ? ""
            : """<div class="notice error">Add at least one book under <a href="/books">Books</a> before creating a video.</div>""";

        var studio = hasBooks
            ? $"""
                <section class="panel tiktok-studio">
                    <h2>Create a book promo video</h2>
                    <p class="muted small-text">We build a short vertical video from your book cover and an AI hook. Download it and post to TikTok, Instagram Reels, or YouTube Shorts when you are ready.</p>
                    <div class="tiktok-studio-layout">
                        <div class="tiktok-studio-controls form">
                            <label>Book
                                <select id="tiktok-book" onchange="tiktokOnBookChange()">{bookOptions}</select>
                            </label>
                            <label>Video title
                                <input id="tiktok-title" maxlength="150" placeholder="Shown on the video">
                            </label>
                            <label>Caption (copy when you post)
                                <textarea id="tiktok-caption" rows="4" placeholder="Hook + hashtags"></textarea>
                            </label>
                            <p id="tiktok-create-status" class="muted small-text" aria-live="polite"></p>
                            <button type="button" class="button" id="tiktok-create-btn" style="background:#000" onclick="tiktokCreateVideo()">Create video</button>
                        </div>
                        <div class="tiktok-studio-preview-wrap">
                            <p class="muted small-text">Preview (9:16)</p>
                            <canvas id="tiktok-canvas" class="tiktok-canvas" width="720" height="1280"></canvas>
                        </div>
                    </div>
                </section>
                <script type="application/json" id="tiktok-books-data">{booksJson}</script>
                {TikTokStudioScript()}
                """
            : "";

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Book promos</p>
                    <h1>Videos</h1>
                    <p class="muted">Create short vertical book promos. Download and post to your favorite video platform.</p>
                </div>
            </section>
            {notice}
            {noBooksNotice}
            {studio}
            <section class="panel">
                <h2>Your videos</h2>
                <div class="tiktok-video-list">
                    {videoRows}
                </div>
            </section>
            <details class="panel">
                <summary><strong>Or upload your own video</strong></summary>
                <p class="muted small-text">Already have a video file? Upload it here to store the caption and download link.</p>
                <form method="post" action="/videos/upload" enctype="multipart/form-data" class="form">
                    <label>Book
                        <select name="bookId">{bookOptions}</select>
                    </label>
                    <label>Video title
                        <input name="title" required maxlength="150">
                    </label>
                    <label>Caption
                        <textarea name="caption" rows="3"></textarea>
                    </label>
                    <label>Video file (MP4, MOV, WEBM — max 1 GB)
                        <input type="file" name="video" accept="video/mp4,video/quicktime,video/webm,video/x-msvideo" required>
                    </label>
                    <button class="button secondary" type="submit">Upload video</button>
                </form>
            </details>
            """;
    }

    static string TikTokStudioScript() => """
        <script>
        (function () {
            var books = [];
            try {
                books = JSON.parse(document.getElementById('tiktok-books-data').textContent || '[]');
            } catch (e) { books = []; }

            var canvas = document.getElementById('tiktok-canvas');
            var ctx = canvas.getContext('2d');
            var coverImg = new Image();
            coverImg.crossOrigin = 'anonymous';
            var animFrame = null;
            var recordStart = 0;
            var durationMs = 12000;

            function bookById(id) {
                return books.find(function (b) { return String(b.id) === String(id); });
            }

            function drawFrame(progress) {
                var w = canvas.width, h = canvas.height;
                ctx.fillStyle = '#0a0a0a';
                ctx.fillRect(0, 0, w, h);
                var zoom = 1 + progress * 0.08;
                if (coverImg.complete && coverImg.naturalWidth > 0) {
                    var iw = coverImg.naturalWidth, ih = coverImg.naturalHeight;
                    var scale = Math.max(w / iw, h / ih) * zoom;
                    var dw = iw * scale, dh = ih * scale;
                    var dx = (w - dw) / 2, dy = (h - dh) / 2;
                    ctx.drawImage(coverImg, dx, dy, dw, dh);
                }
                var grad = ctx.createLinearGradient(0, h * 0.55, 0, h);
                grad.addColorStop(0, 'rgba(0,0,0,0)');
                grad.addColorStop(1, 'rgba(0,0,0,0.85)');
                ctx.fillStyle = grad;
                ctx.fillRect(0, h * 0.45, w, h * 0.55);
                var title = document.getElementById('tiktok-title').value || '';
                var book = bookById(document.getElementById('tiktok-book').value);
                var author = book && book.author ? book.author : '';
                ctx.fillStyle = '#fff';
                ctx.textAlign = 'center';
                ctx.font = 'bold 42px system-ui, sans-serif';
                wrapText(ctx, title, w / 2, h * 0.72, w - 80, 48);
                if (author) {
                    ctx.font = '28px system-ui, sans-serif';
                    ctx.fillStyle = '#e0e0e0';
                    ctx.fillText(author, w / 2, h * 0.88);
                }
                ctx.font = '22px system-ui, sans-serif';
                ctx.fillStyle = 'rgba(255,255,255,0.7)';
                ctx.fillText('BookPromoter AI', w / 2, h * 0.95);
            }

            function wrapText(context, text, x, y, maxWidth, lineHeight) {
                var words = text.split(' ');
                var line = '';
                var lines = [];
                for (var n = 0; n < words.length; n++) {
                    var test = line + words[n] + ' ';
                    if (context.measureText(test).width > maxWidth && n > 0) {
                        lines.push(line.trim());
                        line = words[n] + ' ';
                    } else {
                        line = test;
                    }
                }
                lines.push(line.trim());
                var startY = y - ((lines.length - 1) * lineHeight) / 2;
                for (var i = 0; i < lines.length; i++) {
                    context.fillText(lines[i], x, startY + i * lineHeight);
                }
            }

            function animatePreview() {
                if (animFrame) cancelAnimationFrame(animFrame);
                var start = performance.now();
                function tick(now) {
                    var p = ((now - start) % durationMs) / durationMs;
                    drawFrame(p);
                    animFrame = requestAnimationFrame(tick);
                }
                animFrame = requestAnimationFrame(tick);
            }

            window.tiktokOnBookChange = function () {
                var book = bookById(document.getElementById('tiktok-book').value);
                if (!book) return;
                document.getElementById('tiktok-title').value = book.title;
                document.getElementById('tiktok-caption').value = book.caption || '';
                if (book.coverUrl) {
                    coverImg.onload = animatePreview;
                    coverImg.src = book.coverUrl;
                } else {
                    drawFrame(0);
                }
            };

            window.tiktokCreateVideo = function () {
                var bookId = document.getElementById('tiktok-book').value;
                var title = document.getElementById('tiktok-title').value.trim();
                var caption = document.getElementById('tiktok-caption').value.trim();
                var status = document.getElementById('tiktok-create-status');
                var btn = document.getElementById('tiktok-create-btn');
                if (!bookId) { status.textContent = 'Choose a book first.'; return; }
                if (!title) { status.textContent = 'Enter a video title.'; return; }
                if (!coverImg.complete || !coverImg.naturalWidth) {
                    status.textContent = 'Waiting for cover image — add a cover in Books or pick another title.';
                    return;
                }
                btn.disabled = true;
                status.textContent = 'Creating video (about 12 seconds)...';
                if (animFrame) cancelAnimationFrame(animFrame);

                var stream = canvas.captureStream(30);
                var mime = MediaRecorder.isTypeSupported('video/webm;codecs=vp9')
                    ? 'video/webm;codecs=vp9' : 'video/webm';
                var recorder = new MediaRecorder(stream, { mimeType: mime, videoBitsPerSecond: 2500000 });
                var chunks = [];
                recorder.ondataavailable = function (e) { if (e.data.size) chunks.push(e.data); };
                recorder.onstop = function () {
                    var blob = new Blob(chunks, { type: mime });
                    var fd = new FormData();
                    fd.append('bookId', bookId);
                    fd.append('title', title);
                    fd.append('caption', caption);
                    fd.append('video', blob, 'bookpromo.webm');
                    var csrfField = document.querySelector('meta[name="csrf-field"]');
                    var csrfToken = document.querySelector('meta[name="csrf-token"]');
                    if (csrfField && csrfToken) fd.append(csrfField.content, csrfToken.content);
                    fetch('/videos/create', { method: 'POST', body: fd })
                        .then(function (r) {
                            if (r.redirected) { window.location.href = r.url; return; }
                            status.textContent = 'Could not save video. Try again.';
                            btn.disabled = false;
                            animatePreview();
                        })
                        .catch(function () {
                            status.textContent = 'Upload failed. Try again.';
                            btn.disabled = false;
                            animatePreview();
                        });
                };
                recordStart = performance.now();
                recorder.start(200);
                function recordTick(now) {
                    var p = Math.min(1, (now - recordStart) / durationMs);
                    drawFrame(p);
                    if (p < 1) requestAnimationFrame(recordTick);
                    else recorder.stop();
                }
                requestAnimationFrame(recordTick);
            };

            window.copyTikTokCaption = function (btn) {
                var text = btn.getAttribute('data-caption') || '';
                navigator.clipboard.writeText(text).then(function () {
                    btn.textContent = 'Copied!';
                    setTimeout(function () { btn.textContent = 'Copy caption'; }, 2000);
                });
            };

            if (books.length > 0) {
                document.getElementById('tiktok-book').selectedIndex = 1;
                tiktokOnBookChange();
            }
        })();
        </script>
        """;
}
