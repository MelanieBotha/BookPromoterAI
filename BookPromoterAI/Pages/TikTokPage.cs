using System.Text;
using System.Text.Json;

namespace BookPromoterAI;

static class TikTokPage
{
    public static string Render(AppStoreDb store, PostGenerator generator, string notice = "")
    {
        var videos = store.TikTokVideos;
        var thisWeekVideos = store.TikTokVideosThisWeek;
        var (_, _, currentWeekLabel) = AdWeek.For(DateTime.UtcNow);
        var books = store.Books;

        var bookOptions = new StringBuilder();
        var booksData = new List<object>();
        bookOptions.Append("""<option value="">Choose a book...</option>""");
        foreach (var book in books)
        {
            var purchaseUrl = book.Links.FirstOrDefault()?.Url ?? "";
            var caption = generator.GenerateTikTokCaption(book, purchaseUrl, book.PostVariantSeed);
            var script = TikTokCaptionScript.Build(caption, book.Title, book.AuthorName);
            var excerpt = string.IsNullOrWhiteSpace(book.ReadAloudExcerpt)
                ? ReadAloudScript.LimitWords(book.Description)
                : book.ReadAloudExcerpt;
            bookOptions.Append($"""<option value="{book.Id}">{H.Encode(book.Title)}</option>""");
            booksData.Add(new
            {
                id = book.Id,
                title = book.Title,
                author = book.AuthorName,
                coverUrl = string.IsNullOrWhiteSpace(book.CoverImageUrl) ? "" : book.CoverImageUrl,
                caption,
                excerpt,
                script = new
                {
                    hook = script.Hook,
                    title = script.Title,
                    author = script.Author,
                    chunks = script.Chunks,
                    hashtags = script.Hashtags,
                    link = script.Link,
                    cta = script.Cta
                }
            });
        }

        var booksJson = JsonSerializer.Serialize(booksData);
        var hasBooks = books.Count > 0;

        var videoRows = new StringBuilder();
        foreach (var video in videos)
            videoRows.Append(RenderVideoRow(video));

        var thisWeekRows = new StringBuilder();
        foreach (var video in thisWeekVideos)
            thisWeekRows.Append(RenderVideoRow(video));

        if (thisWeekVideos.Count == 0)
            thisWeekRows.Append("""<p class="muted">Weekly videos generate automatically for each book with a cover. Add books with covers, then check back in a few minutes.</p>""");

        var refreshScript = thisWeekVideos.Any(v => v.Status == TikTokVideoStatuses.Rendering)
            ? """<script>setTimeout(() => location.reload(), 45000);</script>"""
            : "";

        if (videos.Count == 0)
            videoRows.Append("""<p class="muted">No videos yet. Your weekly batch will appear above, or create one manually below.</p>""");

        var noBooksNotice = hasBooks
            ? ""
            : """<div class="notice error">Add at least one book under <a href="/books">Books</a> before creating a video.</div>""";

        var studio = hasBooks
            ? $"""
                <section class="panel tiktok-studio">
                    <h2>Create a book promo video</h2>
                    <p class="muted small-text">TikTok-length videos (60 seconds). Promo: animated captions. Narrated: read-aloud voice with synced subtitles — no ElevenLabs.</p>
                    <div class="tiktok-studio-layout">
                        <div class="tiktok-studio-controls form">
                            <label>Book
                                <select id="tiktok-book" onchange="tiktokOnBookChange()">{bookOptions}</select>
                            </label>
                            <label>Video style
                                <select id="tiktok-style" onchange="tiktokOnStyleChange()">
                                    <option value="promo">Promo (animated caption)</option>
                                    <option value="narrated">Narrated excerpt (read-aloud)</option>
                                </select>
                            </label>
                            <label id="tiktok-excerpt-wrap" style="display:none">Read-aloud excerpt
                                <textarea id="tiktok-excerpt" rows="5" placeholder="Paste a chapter sample (up to ~155 words for a 60s TikTok)."></textarea>
                                <span class="muted small-text">Built-in speech on our server — no ElevenLabs or paid voice API.</span>
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
                    <p class="muted">60-second book promos auto-generate every week. Download and post to TikTok, Reels, or Shorts.</p>
                </div>
            </section>
            {notice}
            {noBooksNotice}
            {studio}
            <section class="panel">
                <h2>This week's videos</h2>
                <p class="muted small-text">{H.Encode(currentWeekLabel)} — one 60-second narrated video per book (from your description, excerpt, and cover). Refreshes every Monday.</p>
                <div class="tiktok-video-list">
                    {thisWeekRows}
                </div>
            </section>
            <section class="panel">
                <h2>All videos</h2>
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
            {refreshScript}
            """;
    }

    static string RenderVideoRow(TikTokVideo video)
    {
        var statusLabel = video.Status switch
        {
            TikTokVideoStatuses.Ready => "Ready to download",
            TikTokVideoStatuses.Rendering => "Generating…",
            TikTokVideoStatuses.Failed => "Failed",
            TikTokVideoStatuses.Sent => "Posted",
            _ => video.Status
        };
        var statusClass = video.Status switch
        {
            TikTokVideoStatuses.Ready => "available",
            TikTokVideoStatuses.Rendering => "pending",
            TikTokVideoStatuses.Failed => "used",
            _ => "available"
        };
        var captionBlock = !string.IsNullOrWhiteSpace(video.Caption)
            ? $"""
                <p class="muted small-text tiktok-caption-preview">{H.Encode(video.Caption)}</p>
                <button type="button" class="button small secondary" onclick="copyTikTokCaption(this)" data-caption="{H.Encode(video.Caption)}">Copy caption</button>
                """
            : "";
        var weekNote = video.AutoGenerated && !string.IsNullOrWhiteSpace(video.WeekLabel)
            ? $"""<p class="muted small-text">{H.Encode(video.WeekLabel)}</p>"""
            : "";
        var errorNote = video.Status == TikTokVideoStatuses.Failed && !string.IsNullOrWhiteSpace(video.ErrorMessage)
            ? $"""<p class="notice error small-text">{H.Encode(video.ErrorMessage)}</p>"""
            : "";
        var preview = video.Status == TikTokVideoStatuses.Ready && !string.IsNullOrWhiteSpace(video.VideoUrl)
            ? $"""<video src="{H.Encode(video.VideoUrl)}" controls preload="metadata" class="tiktok-player"></video>"""
            : video.Status == TikTokVideoStatuses.Rendering
                ? """<div class="tiktok-video-placeholder muted">Rendering 60s video with read-aloud voice…</div>"""
                : """<div class="tiktok-video-placeholder muted">No preview</div>""";
        var download = video.Status == TikTokVideoStatuses.Ready && !string.IsNullOrWhiteSpace(video.VideoUrl)
            ? $"""<a class="button small" href="{H.Encode(video.VideoUrl)}" download>Download</a>"""
            : "";

        return $"""
            <article class="book-row tiktok-video-row">
                <div>
                    <strong>{H.Encode(video.Title)}</strong>
                    <p class="muted">{H.Encode(video.BookTitle)}</p>
                    {weekNote}
                    <small class="status {statusClass}">{H.Encode(statusLabel)}</small>
                    {errorNote}
                    {captionBlock}
                </div>
                <div class="tiktok-video-preview">
                    {preview}
                </div>
                <div class="row-actions">
                    {download}
                    <form method="post" action="/videos/delete/{video.Id}" style="display:inline" onsubmit="return confirm('Remove this video?');">
                        <button class="danger-button small" type="submit">Remove</button>
                    </form>
                </div>
            </article>
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
            var durationMs = 60000;
            var maxDurationMs = 60000;
            var currentScript = null;
            var videoStyle = 'promo';
            var narratedAnimFrame = null;
            var ctaTailMs = 4000;

            var scenes = [
                { id: 'hook', start: 0, end: 8000 },
                { id: 'cover', start: 8000, end: 22000 },
                { id: 'chunks', start: 22000, end: 42000 },
                { id: 'hashtags', start: 42000, end: 52000 },
                { id: 'cta', start: 52000, end: 60000 }
            ];

            function bookById(id) {
                return books.find(function (b) { return String(b.id) === String(id); });
            }

            function easeOut(t) { return 1 - Math.pow(1 - t, 3); }
            function easeInOut(t) { return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2; }
            function clamp(v, lo, hi) { return Math.max(lo, Math.min(hi, v)); }
            function sceneAt(ms) {
                for (var i = 0; i < scenes.length; i++) {
                    if (ms >= scenes[i].start && ms < scenes[i].end) return scenes[i];
                }
                return scenes[scenes.length - 1];
            }

            function drawCoverBg(progress, dim) {
                var w = canvas.width, h = canvas.height;
                ctx.fillStyle = '#0a0a0a';
                ctx.fillRect(0, 0, w, h);
                if (coverImg.complete && coverImg.naturalWidth > 0) {
                    var zoom = 1 + (progress || 0) * 0.12;
                    var iw = coverImg.naturalWidth, ih = coverImg.naturalHeight;
                    var scale = Math.max(w / iw, h / ih) * zoom;
                    var dw = iw * scale, dh = ih * scale;
                    var dx = (w - dw) / 2, dy = (h - dh) / 2;
                    ctx.drawImage(coverImg, dx, dy, dw, dh);
                }
                if (dim > 0) {
                    ctx.fillStyle = 'rgba(0,0,0,' + dim + ')';
                    ctx.fillRect(0, 0, w, h);
                }
            }

            function drawGradientFooter() {
                var w = canvas.width, h = canvas.height;
                var grad = ctx.createLinearGradient(0, h * 0.5, 0, h);
                grad.addColorStop(0, 'rgba(0,0,0,0)');
                grad.addColorStop(1, 'rgba(0,0,0,0.88)');
                ctx.fillStyle = grad;
                ctx.fillRect(0, h * 0.4, w, h * 0.6);
            }

            function wrapText(context, text, x, y, maxWidth, lineHeight, maxLines) {
                var words = String(text || '').split(' ');
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
                if (line.trim()) lines.push(line.trim());
                if (maxLines && lines.length > maxLines) {
                    lines = lines.slice(0, maxLines);
                    lines[maxLines - 1] = lines[maxLines - 1].replace(/\.\.\.$/, '') + '…';
                }
                var startY = y - ((lines.length - 1) * lineHeight) / 2;
                for (var i = 0; i < lines.length; i++) {
                    context.fillText(lines[i], x, startY + i * lineHeight);
                }
                return lines.length;
            }

            function drawHookScene(local, script) {
                var w = canvas.width, h = canvas.height;
                var grad = ctx.createLinearGradient(0, 0, w, h);
                grad.addColorStop(0, '#1a0a2e');
                grad.addColorStop(0.5, '#0d0d1a');
                grad.addColorStop(1, '#0a0a0a');
                ctx.fillStyle = grad;
                ctx.fillRect(0, 0, w, h);
                if (coverImg.complete && coverImg.naturalWidth > 0) {
                    ctx.globalAlpha = 0.25 * easeOut(local);
                    drawCoverBg(0, 0);
                    ctx.globalAlpha = 1;
                }
                var alpha = easeOut(clamp(local * 1.4, 0, 1));
                var slide = (1 - easeOut(clamp(local * 1.2, 0, 1))) * 40;
                var hookLines = (script.hook || '').split('\n').filter(function (l) { return l.trim(); });
                var mainHook = hookLines[0] || script.title || '';
                var subHook = hookLines[1] || '';
                ctx.save();
                ctx.globalAlpha = alpha;
                ctx.fillStyle = '#fff';
                ctx.textAlign = 'center';
                ctx.font = 'bold 52px system-ui, sans-serif';
                wrapText(ctx, mainHook, w / 2, h * 0.42 - slide, w - 100, 58, 4);
                if (subHook) {
                    ctx.font = '32px system-ui, sans-serif';
                    ctx.fillStyle = '#d8d8ff';
                    wrapText(ctx, subHook, w / 2, h * 0.62 - slide, w - 100, 40, 3);
                }
                ctx.restore();
                ctx.font = '20px system-ui, sans-serif';
                ctx.fillStyle = 'rgba(255,255,255,0.45)';
                ctx.textAlign = 'center';
                ctx.fillText('📚 BookTok', w / 2, h * 0.12);
            }

            function drawCoverScene(local, script) {
                drawCoverBg(local, 0);
                drawGradientFooter();
                var fade = easeOut(clamp((local - 0.15) * 2, 0, 1));
                ctx.save();
                ctx.globalAlpha = fade;
                ctx.fillStyle = '#fff';
                ctx.textAlign = 'center';
                ctx.font = 'bold 44px system-ui, sans-serif';
                wrapText(ctx, script.title || '', canvas.width / 2, canvas.height * 0.72, canvas.width - 80, 50, 3);
                if (script.author) {
                    ctx.font = '28px system-ui, sans-serif';
                    ctx.fillStyle = '#e8e8e8';
                    ctx.fillText(script.author, canvas.width / 2, canvas.height * 0.86);
                }
                ctx.restore();
            }

            function drawChunksScene(local, script) {
                drawCoverBg(0.5, 0.55);
                var chunks = script.chunks && script.chunks.length ? script.chunks : [script.hook || script.title];
                var idx = Math.min(chunks.length - 1, Math.floor(local * chunks.length));
                var chunkLocal = (local * chunks.length) - idx;
                var pop = easeOut(clamp(chunkLocal * 2.5, 0, 1));
                var scale = 0.85 + pop * 0.15;
                var alpha = easeOut(clamp(chunkLocal * 3, 0, 1));
                ctx.save();
                ctx.globalAlpha = alpha;
                ctx.translate(canvas.width / 2, canvas.height * 0.48);
                ctx.scale(scale, scale);
                ctx.fillStyle = '#fff';
                ctx.textAlign = 'center';
                ctx.font = 'bold 48px system-ui, sans-serif';
                wrapText(ctx, chunks[idx], 0, 0, canvas.width - 120, 54, 4);
                ctx.restore();
                ctx.font = '22px system-ui, sans-serif';
                ctx.fillStyle = 'rgba(255,255,255,0.5)';
                ctx.textAlign = 'center';
                ctx.fillText((idx + 1) + ' / ' + chunks.length, canvas.width / 2, canvas.height * 0.88);
            }

            function drawHashtagsScene(local, script) {
                drawCoverBg(0.3, 0.7);
                var alpha = easeOut(clamp(local * 1.5, 0, 1));
                ctx.save();
                ctx.globalAlpha = alpha;
                ctx.fillStyle = '#7dd3fc';
                ctx.textAlign = 'center';
                ctx.font = 'bold 36px system-ui, sans-serif';
                wrapText(ctx, script.hashtags || '#BookTok #Books', canvas.width / 2, canvas.height * 0.45, canvas.width - 80, 44, 5);
                ctx.restore();
                if (script.title) {
                    ctx.globalAlpha = alpha * 0.8;
                    ctx.fillStyle = '#fff';
                    ctx.font = '26px system-ui, sans-serif';
                    ctx.textAlign = 'center';
                    wrapText(ctx, '"' + script.title + '"', canvas.width / 2, canvas.height * 0.72, canvas.width - 80, 32, 2);
                }
            }

            function drawCtaScene(local, script) {
                drawCoverBg(0.6, 0.75);
                var pulse = 0.9 + 0.1 * Math.sin(local * Math.PI * 2);
                var alpha = easeOut(clamp(local * 1.8, 0, 1));
                ctx.save();
                ctx.globalAlpha = alpha;
                ctx.translate(canvas.width / 2, canvas.height * 0.44);
                ctx.scale(pulse, pulse);
                ctx.fillStyle = '#fff';
                ctx.textAlign = 'center';
                ctx.font = 'bold 40px system-ui, sans-serif';
                wrapText(ctx, script.cta || 'Link in bio 📚', 0, 0, canvas.width - 100, 46, 2);
                ctx.restore();
                if (script.link) {
                    ctx.globalAlpha = alpha * 0.7;
                    ctx.font = '22px system-ui, sans-serif';
                    ctx.fillStyle = '#a5f3fc';
                    ctx.textAlign = 'center';
                    var shortLink = script.link.length > 42 ? script.link.slice(0, 39) + '…' : script.link;
                    ctx.fillText(shortLink, canvas.width / 2, canvas.height * 0.58);
                }
                ctx.globalAlpha = alpha * 0.6;
                ctx.font = '20px system-ui, sans-serif';
                ctx.fillStyle = '#ccc';
                ctx.fillText('BookPromoter AI', canvas.width / 2, canvas.height * 0.92);
            }

            function drawFrame(elapsedMs) {
                var t = elapsedMs;
                var scene = sceneAt(t);
                var local = (t - scene.start) / (scene.end - scene.start);
                var script = currentScript || { hook: '', title: '', author: '', chunks: [], hashtags: '', link: '', cta: '' };
                switch (scene.id) {
                    case 'hook': drawHookScene(local, script); break;
                    case 'cover': drawCoverScene(local, script); break;
                    case 'chunks': drawChunksScene(local, script); break;
                    case 'hashtags': drawHashtagsScene(local, script); break;
                    case 'cta': drawCtaScene(local, script); break;
                }
            }

            function animatePreview() {
                if (animFrame) cancelAnimationFrame(animFrame);
                var start = performance.now();
                function tick(now) {
                    var elapsed = (now - start) % durationMs;
                    drawFrame(elapsed);
                    animFrame = requestAnimationFrame(tick);
                }
                animFrame = requestAnimationFrame(tick);
            }

            window.tiktokOnBookChange = function () {
                var book = bookById(document.getElementById('tiktok-book').value);
                if (!book) return;
                document.getElementById('tiktok-title').value = book.title;
                document.getElementById('tiktok-caption').value = book.caption || '';
                document.getElementById('tiktok-excerpt').value = book.excerpt || '';
                currentScript = book.script || null;
                if (book.coverUrl) {
                    coverImg.onload = function () {
                        if (videoStyle === 'narrated') startNarratedPreview();
                        else animatePreview();
                    };
                    coverImg.src = book.coverUrl;
                } else {
                    if (videoStyle === 'narrated') startNarratedPreview();
                    else drawFrame(0);
                }
            };

            function uploadRecordedBlob(blob, mime, bookId, title, caption, status, btn) {
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
                        resumePreview();
                    })
                    .catch(function () {
                        status.textContent = 'Upload failed. Try again.';
                        btn.disabled = false;
                        resumePreview();
                    });
            }

            function resumePreview() {
                if (videoStyle === 'narrated') startNarratedPreview();
                else animatePreview();
            }

            function clampSpeechMs(ms) {
                return Math.min(ms, maxDurationMs - ctaTailMs);
            }

            function totalVideoMs(speechMs) {
                return Math.min(maxDurationMs, clampSpeechMs(speechMs) + ctaTailMs);
            }

            function drawNarratedFrame(elapsedMs, beats, speechMs, script) {
                speechMs = clampSpeechMs(speechMs);
                var progress = speechMs > 0 ? Math.min(1, elapsedMs / speechMs) : 0;
                drawCoverBg(progress * 0.85, 0.42);
                drawGradientFooter();
                if (elapsedMs >= speechMs) {
                    drawCtaScene((elapsedMs - speechMs) / ctaTailMs, script);
                    return;
                }
                var beat = null;
                for (var i = 0; i < beats.length; i++) {
                    if (elapsedMs >= beats[i].startMs && elapsedMs < beats[i].endMs) { beat = beats[i]; break; }
                }
                if (!beat && beats.length) beat = beats[beats.length - 1];
                var local = beat ? (elapsedMs - beat.startMs) / Math.max(1, beat.endMs - beat.startMs) : 0;
                var alpha = easeOut(clamp(local * 2.2, 0, 1));
                ctx.save();
                ctx.globalAlpha = alpha;
                ctx.fillStyle = '#fff';
                ctx.textAlign = 'center';
                ctx.font = 'bold 42px system-ui, sans-serif';
                wrapText(ctx, beat ? beat.text : '', canvas.width / 2, canvas.height * 0.5, canvas.width - 90, 48, 6);
                ctx.restore();
                ctx.fillStyle = 'rgba(255,255,255,0.75)';
                ctx.font = '26px system-ui, sans-serif';
                ctx.textAlign = 'center';
                wrapText(ctx, script.title || '', canvas.width / 2, canvas.height * 0.82, canvas.width - 80, 32, 2);
                if (script.author) {
                    ctx.fillStyle = 'rgba(255,255,255,0.55)';
                    ctx.font = '22px system-ui, sans-serif';
                    ctx.fillText(script.author, canvas.width / 2, canvas.height * 0.9);
                }
            }

            function estimateBeatsFromText(text) {
                var parts = text.split(/[.!?]+\s*/).filter(function (s) { return s.trim(); });
                if (!parts.length) parts = [text];
                var wpm = 165;
                var totalMs = clampSpeechMs((text.split(/\s+/).length / wpm) * 60 * 1000);
                var totalChars = Math.max(1, parts.join('').length);
                var cursor = 0;
                return parts.map(function (p) {
                    var ms = totalMs * (p.length / totalChars);
                    var beat = { text: p, startMs: cursor, endMs: cursor + ms };
                    cursor += ms;
                    return beat;
                });
            }

            function startNarratedPreview() {
                if (animFrame) cancelAnimationFrame(animFrame);
                if (narratedAnimFrame) cancelAnimationFrame(narratedAnimFrame);
                var excerpt = document.getElementById('tiktok-excerpt').value.trim();
                var beats = estimateBeatsFromText(excerpt || 'Sample read-aloud preview.');
                var speechMs = beats.length ? beats[beats.length - 1].endMs : 12000;
                var totalMs = totalVideoMs(speechMs);
                var script = currentScript || { title: '', author: '', cta: 'Link in bio 📚', link: '' };
                var start = performance.now();
                function tick(now) {
                    var elapsed = (now - start) % totalMs;
                    drawNarratedFrame(elapsed, beats, speechMs, script);
                    narratedAnimFrame = requestAnimationFrame(tick);
                }
                narratedAnimFrame = requestAnimationFrame(tick);
            }

            window.tiktokOnStyleChange = function () {
                videoStyle = document.getElementById('tiktok-style').value;
                var excerptWrap = document.getElementById('tiktok-excerpt-wrap');
                excerptWrap.style.display = videoStyle === 'narrated' ? '' : 'none';
                if (animFrame) cancelAnimationFrame(animFrame);
                if (narratedAnimFrame) cancelAnimationFrame(narratedAnimFrame);
                if (videoStyle === 'narrated') startNarratedPreview();
                else animatePreview();
            };

            function createPromoVideo() {
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
                status.textContent = 'Creating video (60 seconds)...';
                if (animFrame) cancelAnimationFrame(animFrame);

                var stream = canvas.captureStream(30);
                var mime = MediaRecorder.isTypeSupported('video/webm;codecs=vp9')
                    ? 'video/webm;codecs=vp9' : 'video/webm';
                var recorder = new MediaRecorder(stream, { mimeType: mime, videoBitsPerSecond: 2500000 });
                var chunks = [];
                recorder.ondataavailable = function (e) { if (e.data.size) chunks.push(e.data); };
                recorder.onstop = function () {
                    var blob = new Blob(chunks, { type: mime });
                    uploadRecordedBlob(blob, mime, bookId, title, caption, status, btn);
                };
                recordStart = performance.now();
                recorder.start(200);
                function recordTick(now) {
                    var elapsed = now - recordStart;
                    drawFrame(elapsed);
                    if (elapsed < durationMs) requestAnimationFrame(recordTick);
                    else recorder.stop();
                }
                requestAnimationFrame(recordTick);
            }

            function createNarratedVideo() {
                var bookId = document.getElementById('tiktok-book').value;
                var title = document.getElementById('tiktok-title').value.trim();
                var caption = document.getElementById('tiktok-caption').value.trim();
                var excerpt = document.getElementById('tiktok-excerpt').value.trim();
                var status = document.getElementById('tiktok-create-status');
                var btn = document.getElementById('tiktok-create-btn');
                if (!bookId) { status.textContent = 'Choose a book first.'; return; }
                if (!title) { status.textContent = 'Enter a video title.'; return; }
                if (!excerpt) { status.textContent = 'Add a read-aloud excerpt (paste a chapter sample).'; return; }
                if (!coverImg.complete || !coverImg.naturalWidth) {
                    status.textContent = 'Waiting for cover image — add a cover in Books or pick another title.';
                    return;
                }
                btn.disabled = true;
                status.textContent = 'Generating read-aloud voice...';
                if (animFrame) cancelAnimationFrame(animFrame);
                if (narratedAnimFrame) cancelAnimationFrame(narratedAnimFrame);

                var fd = new FormData();
                fd.append('text', excerpt);
                var csrfField = document.querySelector('meta[name="csrf-field"]');
                var csrfToken = document.querySelector('meta[name="csrf-token"]');
                if (csrfField && csrfToken) fd.append(csrfField.content, csrfToken.content);

                fetch('/videos/speech', { method: 'POST', body: fd })
                    .then(function (r) { return r.json(); })
                    .then(function (data) {
                        if (data.error || !data.wavBase64) {
                            status.textContent = data.error || 'Speech not available — try Promo style.';
                            btn.disabled = false;
                            resumePreview();
                            return;
                        }
                        var raw = atob(data.wavBase64);
                        var bytes = new Uint8Array(raw.length);
                        for (var i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
                        return recordNarratedWithAudio(bytes.buffer, data.beats, clampSpeechMs(data.durationMs), bookId, title, caption, status, btn);
                    })
                    .catch(function () {
                        status.textContent = 'Could not generate speech. Try again.';
                        btn.disabled = false;
                        resumePreview();
                    });
            }

            function recordNarratedWithAudio(arrayBuffer, beats, speechMs, bookId, title, caption, status, btn) {
                var script = currentScript || { title: '', author: '', cta: 'Link in bio 📚', link: '' };
                speechMs = clampSpeechMs(speechMs);
                var totalMs = totalVideoMs(speechMs);
                var audioCtx = new (window.AudioContext || window.webkitAudioContext)();
                return audioCtx.decodeAudioData(arrayBuffer).then(function (audioBuffer) {
                    status.textContent = 'Recording narrated video...';
                    var source = audioCtx.createBufferSource();
                    source.buffer = audioBuffer;
                    var dest = audioCtx.createMediaStreamDestination();
                    source.connect(dest);
                    var canvasStream = canvas.captureStream(30);
                    var tracks = canvasStream.getVideoTracks().concat(dest.stream.getAudioTracks());
                    var combined = new MediaStream(tracks);
                    var mime = MediaRecorder.isTypeSupported('video/webm;codecs=vp9,opus')
                        ? 'video/webm;codecs=vp9,opus'
                        : (MediaRecorder.isTypeSupported('video/webm;codecs=vp9') ? 'video/webm;codecs=vp9' : 'video/webm');
                    var recorder = new MediaRecorder(combined, { mimeType: mime, videoBitsPerSecond: 2500000 });
                    var chunks = [];
                    recorder.ondataavailable = function (e) { if (e.data.size) chunks.push(e.data); };
                    recorder.onstop = function () {
                        audioCtx.close();
                        uploadRecordedBlob(new Blob(chunks, { type: mime }), mime, bookId, title, caption, status, btn);
                    };
                    var startAt = audioCtx.currentTime;
                    source.start(0);
                    if (speechMs < audioBuffer.duration * 1000)
                        source.stop(startAt + speechMs / 1000);
                    recorder.start(200);
                    function tick() {
                        var elapsedMs = (audioCtx.currentTime - startAt) * 1000;
                        drawNarratedFrame(elapsedMs, beats, speechMs, script);
                        if (elapsedMs < totalMs) requestAnimationFrame(tick);
                        else recorder.stop();
                    }
                    requestAnimationFrame(tick);
                }).catch(function () {
                    status.textContent = 'Could not play audio for recording.';
                    btn.disabled = false;
                    audioCtx.close();
                    resumePreview();
                });
            }

            window.tiktokCreateVideo = function () {
                if (document.getElementById('tiktok-style').value === 'narrated') createNarratedVideo();
                else createPromoVideo();
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
