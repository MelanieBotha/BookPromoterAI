using System.Text;
using System.Text.Json;
namespace BookPromoterAI;

static class AdLibraryPage
{
    public static string Render(AppStoreDb store, string search, string notice, string focus, HttpRequest request, AppSettings settings)
    {
        var appBaseUrl = PublicUrl.Base(request, settings);
        var filtered = store.GeneratedAds.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(a =>
                a.BookTitle.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.Platform.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.PostText.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var now = DateTime.UtcNow;
        var appUrl = appBaseUrl.TrimEnd('/');
        var monthAds = filtered.Where(a => a.GeneratedAt.Year == now.Year && a.GeneratedAt.Month == now.Month).ToList();

        var byWeek = monthAds
            .GroupBy(a => (a.WeekYear, a.WeekNumber, a.WeekLabel))
            .OrderByDescending(g => g.Key.WeekYear)
            .ThenByDescending(g => g.Key.WeekNumber)
            .ToList();

        var weekSections = new StringBuilder();
        if (byWeek.Count == 0)
        {
            weekSections.Append($"""
                <section class="panel">
                    <p class="muted">
                        {(string.IsNullOrWhiteSpace(search)
                            ? "No posts generated this month yet. Click \"Generate This Week's Posts\" to create your first batch."
                            : $"No posts matched \"{H.Encode(search)}\" this month.")}
                    </p>
                </section>
                """);
        }

        foreach (var week in byWeek)
        {
            var cards = new StringBuilder();
            foreach (var ad in week.OrderByDescending(a => a.GeneratedAt))
            {
                var book = store.Books.FirstOrDefault(b => b.Id == ad.BookId);
                var coverPath = !string.IsNullOrWhiteSpace(book?.CoverImageUrl) ? book!.CoverImageUrl : ad.CoverImageUrl;
                var coverSrc = PostBranding.AbsoluteImageUrl(appBaseUrl, coverPath);
                var cover = string.IsNullOrWhiteSpace(coverPath)
                    ? """<div class="cover-placeholder large">No cover</div>"""
                    : $"""<img class="book-cover large" src="{H.Encode(coverSrc)}" alt="{H.Encode(ad.BookTitle)} cover">""";

                var searchField = string.IsNullOrWhiteSpace(search) ? "" : $"""<input type="hidden" name="search" value="{H.Encode(search)}">""";
                var regenButton = book is not null
                    ? $"""<form method="post" action="/ad-library/regenerate/{ad.Id}">{searchField}<button class="button secondary small" type="submit">Regenerate</button></form>"""
                    : """<p class="muted small-text">Book removed.</p>""";

                // Post text is stored in a hidden textarea (preserves exact
                // text/line breaks) and copied to the clipboard via JS.
                var copyId = $"post-text-{ad.Id}";

                // Status badge reflects auto-posting state: Pending (awaiting
                // schedule or approval), Posted (sent), Failed (API error).
                var statusClass = ad.PostStatus switch
                {
                    "Posted" => "available",
                    "Failed" => "used",
                    _ => "used"
                };
                var statusBadge = $"""<span class="status {statusClass}">{H.Encode(ad.PostStatus)}</span>""";

                var schedule = store.Schedules.FirstOrDefault(s => s.Platform.Equals(ad.Platform, StringComparison.OrdinalIgnoreCase));
                var needsApproval = (schedule?.RequiresApproval ?? false) && ad.PostStatus == "Pending";
                var approveButton = needsApproval
                    ? ad.ApprovedForPosting
                        ? """<span class="status available small-text">Approved</span>"""
                        : $"""<form method="post" action="/ad-library/approve/{ad.Id}">{searchField}<button class="button small" type="submit">Approve for Auto-Post</button></form>"""
                    : "";

                var charCount = PostLimits.CharacterCountLabel(ad.Platform, ad.PostText);
                var focusClass = focus == $"ad-{ad.Id}" ? " post-card-focused" : "";
                var coverUrl = PostBranding.AbsoluteImageUrl(appBaseUrl, coverPath);
                cards.Append($"""
                    <article class="post-card{focusClass}" id="ad-{ad.Id}">
                        <div class="post-card-cover">{cover}</div>
                        <div class="post-card-header">
                            <div>
                                <strong>{H.Encode(ad.BookTitle)}</strong>
                                <small>{ad.GeneratedAt:ddd MMM d, HH:mm} UTC</small>
                            </div>
                            {statusBadge}
                        </div>
                        <p class="platform-tag">{H.Encode(ad.Platform)}{charCount}</p>
                        <p>{H.Encode(ad.PostText)}</p>
                        <textarea id="{copyId}" class="copy-source" readonly>{H.Encode(ad.PostText)}</textarea>
                        <div class="post-card-actions">
                            <button class="button secondary small copy-button" type="button" data-cover-url="{H.Encode(coverUrl)}" onclick="copyPostText('{copyId}', this)">Copy post + cover</button>
                            {regenButton}
                            {approveButton}
                        </div>
                    </article>
                    """);
            }

            weekSections.Append($"""
                <section class="panel">
                    <h2>{H.Encode(week.Key.WeekLabel)}</h2>
                    <p class="muted small-text">{week.Count()} post(s) generated</p>
                    <div class="post-grid">{cards}</div>
                </section>
                """);
        }

        var monthLabel = now.ToString("MMMM yyyy");
        var totalThisMonth = monthAds.Count;
        var scheduledPerWeek = store.Schedules.Sum(s => s.PostsPerWeek);

        var appUrlJs = JsonSerializer.Serialize(appUrl);

        var script = $$"""
            <script>
            var BPAI_APP_URL = {{appUrlJs}};

            function copyPostText(textareaId, button) {
                var textarea = document.getElementById(textareaId);
                var text = textarea.value;
                var coverUrl = button.getAttribute('data-cover-url') || '';
                var html = buildPostHtml(text, coverUrl);

                function showCopied(label) {
                    var original = button.textContent;
                    button.textContent = label || 'Copied!';
                    button.classList.add('copied');
                    setTimeout(function () {
                        button.textContent = original;
                        button.classList.remove('copied');
                    }, 2000);
                }

                if (!coverUrl) {
                    copyTextOnly(text, textarea, showCopied);
                    return;
                }

                copyPostWithCover(text, html, coverUrl)
                    .then(function () { showCopied('Copied with cover!'); })
                    .catch(function () {
                        if (copyHtmlFallback(html)) {
                            showCopied('Copied!');
                            return;
                        }
                        copyTextOnly(text, textarea, showCopied);
                    });
            }

            function buildPostHtml(text, coverUrl) {
                var escaped = text
                    .replace(/&/g, '&amp;')
                    .replace(/</g, '&lt;')
                    .replace(/>/g, '&gt;')
                    .replace(/"/g, '&quot;');
                var linked = escaped.replace(/(https?:\/\/[^\s<]+)/g, function (url) {
                    return '<a href="' + url + '">' + url + '</a>';
                });
                var imageHtml = coverUrl
                    ? '<img src="' + coverUrl + '" alt="Book cover" style="max-width:320px;width:100%;height:auto;border:0;display:block;margin:0 0 12px">'
                    : '';
                return '<div>' + imageHtml + linked.replace(/\n/g, '<br>') + '</div>';
            }

            async function copyPostWithCover(text, html, coverUrl) {
                if (!navigator.clipboard) throw new Error('no clipboard');
                var coverResponse = await fetch(coverUrl);
                if (!coverResponse.ok) throw new Error('cover fetch failed');
                var coverBlob = await coverResponse.blob();
                var coverType = coverBlob.type || 'image/png';

                if (window.ClipboardItem) {
                    var item = new ClipboardItem({
                        'text/plain': new Blob([text], { type: 'text/plain' }),
                        'text/html': new Blob([html], { type: 'text/html' }),
                        [coverType]: coverBlob
                    });
                    await navigator.clipboard.write([item]);
                    return;
                }

                throw new Error('ClipboardItem unsupported');
            }

            function copyTextOnly(text, textarea, showCopied) {
                if (navigator.clipboard && navigator.clipboard.writeText) {
                    navigator.clipboard.writeText(text).then(function () { showCopied('Text copied'); }).catch(function () {
                        fallbackCopy(textarea, showCopied);
                    });
                } else {
                    fallbackCopy(textarea, showCopied);
                }
            }

            function copyHtmlFallback(html) {
                var container = document.createElement('div');
                container.contentEditable = 'true';
                container.innerHTML = html;
                container.style.position = 'fixed';
                container.style.left = '-9999px';
                document.body.appendChild(container);
                var range = document.createRange();
                range.selectNodeContents(container);
                var selection = window.getSelection();
                selection.removeAllRanges();
                selection.addRange(range);
                var ok = false;
                try {
                    ok = document.execCommand('copy');
                } catch (e) {
                    ok = false;
                }
                selection.removeAllRanges();
                document.body.removeChild(container);
                return ok;
            }

            function fallbackCopy(textarea, onSuccess) {
                textarea.style.position = 'fixed';
                textarea.style.opacity = '0';
                textarea.removeAttribute('readonly');
                textarea.select();
                try {
                    document.execCommand('copy');
                    onSuccess('Text copied');
                } catch (e) {
                    alert('Could not copy automatically. Please select and copy the text manually.');
                } finally {
                    textarea.setAttribute('readonly', 'readonly');
                    textarea.style.position = '';
                    textarea.style.opacity = '';
                }
            }

            (function () {
                var hash = window.location.hash;
                if (!hash) return;
                var el = document.querySelector(hash);
                if (el) {
                    el.scrollIntoView({ block: 'center', behavior: 'instant' });
                    el.classList.add('post-card-focused');
                }
            })();
            </script>
            """;

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Ad Library</p>
                    <h1>AI-generated posts for {H.Encode(monthLabel)}.</h1>
                    <p class="muted">{totalThisMonth} post(s) this month &middot; Schedule: {scheduledPerWeek} posts/week across {store.Schedules.Count(s => s.PostsPerWeek > 0)} platform(s)</p>
                    <p class="muted small-text"><strong>Copy post + cover</strong> pastes your caption with a book page link (<code>/book/…</code>) that shows your cover, counts clicks in Analytics, and links readers to your store. On social, paste the caption — the link preview should show your book cover. Upload the cover image from the card if the platform asks for a photo.</p>
                </div>
                <form method="post" action="/ad-library/generate-week">
                    <button class="button" type="submit">Generate This Week's Posts</button>
                </form>
            </section>

            {notice}

            <section class="panel">
                <form method="get" action="/ad-library" class="search-bar">
                    <input name="search" type="search" placeholder="Search by book, platform, or post text..." value="{H.Encode(search)}">
                    <button class="button" type="submit">Search</button>
                    {(string.IsNullOrWhiteSpace(search) ? "" : """<a class="button secondary" href="/ad-library">Clear</a>""")}
                </form>
            </section>

            {weekSections}

            {script}
            """;
    }
}
