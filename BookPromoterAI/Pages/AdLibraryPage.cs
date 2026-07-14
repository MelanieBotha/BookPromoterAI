using System.Text;
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

        var allAds = filtered.ToList();
        var now = DateTime.UtcNow;
        var (currentWeek, currentYear, currentWeekLabel) = AdWeek.For(now);

        var byWeek = allAds
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
                            ? "No posts yet. Set posts/week on My Account — this week's batch generates automatically in the background."
                            : $"No posts matched \"{H.Encode(search)}\".")}
                    </p>
                </section>
                """);
        }

        foreach (var week in byWeek)
        {
            var cards = new StringBuilder();
            foreach (var ad in week.OrderBy(a => PostSchedule.DisplayTime(a)))
            {
                cards.Append(RenderPostCard(store, ad, appBaseUrl, search, focus));
            }

            var isCurrentWeek = week.Key.WeekYear == currentYear && week.Key.WeekNumber == currentWeek;
            var weekLabel = string.IsNullOrWhiteSpace(week.Key.WeekLabel)
                ? $"Week {week.Key.WeekNumber}, {week.Key.WeekYear}"
                : week.Key.WeekLabel;
            var openAttr = isCurrentWeek ? " open" : "";

            weekSections.Append($"""
                <details class="ad-week-collapsible"{openAttr}>
                    <summary class="ad-week-heading">
                        <span>{H.Encode(weekLabel)}{(isCurrentWeek ? " <span class=\"status available small-text\">This week</span>" : "")}</span>
                        <span class="ad-week-count">{week.Count()} post(s)</span>
                    </summary>
                    <div class="ad-week-body">
                        <div class="post-grid">{cards}</div>
                    </div>
                </details>
                """);
        }

        var totalAds = allAds.Count;
        var thisWeekCount = allAds.Count(a => a.WeekYear == currentYear && a.WeekNumber == currentWeek);
        var scheduledPerWeek = store.ConnectedAuthorSchedules().Sum(s => s.PostsPerWeek);

        var script = """
            <script>
            function copyPostText(textareaId, button) {
                var textarea = document.getElementById(textareaId);
                var text = textarea.value;

                function showCopied() {
                    var original = button.textContent;
                    button.textContent = 'Copied!';
                    button.classList.add('copied');
                    setTimeout(function () {
                        button.textContent = original;
                        button.classList.remove('copied');
                    }, 2000);
                }

                if (navigator.clipboard && navigator.clipboard.writeText) {
                    navigator.clipboard.writeText(text).then(showCopied).catch(function () {
                        fallbackCopy(textarea, showCopied);
                    });
                } else {
                    fallbackCopy(textarea, showCopied);
                }
            }

            function fallbackCopy(textarea, onSuccess) {
                textarea.style.position = 'fixed';
                textarea.style.opacity = '0';
                textarea.removeAttribute('readonly');
                textarea.select();
                try {
                    document.execCommand('copy');
                    onSuccess();
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
                if (!el) return;
                var week = el.closest('details.ad-week-collapsible');
                if (week) week.open = true;
                el.scrollIntoView({ block: 'center', behavior: 'instant' });
                el.classList.add('post-card-focused');
            })();
            </script>
            """;

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Ad Library</p>
                    <h1>AI-generated posts by week.</h1>
                    <p class="muted">{totalAds} post(s) total &middot; {thisWeekCount} this week ({H.Encode(currentWeekLabel)}) &middot; Schedule: {scheduledPerWeek} posts/week across {store.ConnectedAuthorSchedules().Count(s => s.PostsPerWeek > 0)} connected platform(s)</p>
                    <p class="muted small-text"><strong>Copy post</strong> copies the caption only. Your book link is on the <strong>last line</strong> — Facebook and X use that URL for the preview image (portrait covers are letterboxed so the full cover shows). On X without auto-post, paste the caption and publish; you can also right-click the cover above and attach it for a larger image. This week's posts <strong>auto-generate</strong> from your schedule (you do not need to click Generate). Use <strong>Generate This Week's Posts</strong> only to replace unapproved drafts with fresh captions; approved and already-posted ads are left unchanged. Auto-post runs every <strong>5 minutes</strong> and spaces posts evenly across the week — use <strong>Post now</strong> to publish immediately.</p>
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

    static string RenderPostCard(AppStoreDb store, GeneratedAd ad, string appBaseUrl, string search, string focus)
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

        var copyId = $"post-text-{ad.Id}";
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

        var hasAccount = store.AuthorSocialAccounts.Any(a =>
            PostLimits.PlatformsMatch(a.Platform, ad.Platform) && a.IsConnected);
        var authorAccount = store.AuthorSocialAccounts.FirstOrDefault(a =>
            PostLimits.PlatformsMatch(a.Platform, ad.Platform) && a.IsConnected);
        var platformNotLive = authorAccount is not null
            && PostLimits.RequiresLiveConnection(ad.Platform)
            && !authorAccount.IsLiveConnection;
        var canPostNow = hasAccount
            && !platformNotLive
            && ad.PostStatus is "Pending" or "Failed"
            && (!needsApproval || ad.ApprovedForPosting);
        var reconnectHint = PostLimits.LivePostNowHint(ad.Platform);
        var postNowButton = canPostNow
            ? $"""<form method="post" action="/ad-library/post-now/{ad.Id}">{searchField}<button class="button small" type="submit">Post now</button></form>"""
            : platformNotLive && ad.PostStatus is "Pending" or "Failed"
                ? $"""<p class="muted small-text">{H.Encode(reconnectHint)}</p>"""
                : "";

        var postErrorNote = ad.PostStatus == "Failed" && !string.IsNullOrWhiteSpace(ad.PostError)
            ? $"""<p class="notice error small-text">{H.Encode(ad.PostError)}</p>"""
            : "";

        var autoPostHint = PostSchedule.FormatAdAutoPostHint(ad, schedule) is string hint
            ? $"""<p class="muted small-text">{H.Encode(hint)}</p>"""
            : "";

        var timeSubtitle = PostSchedule.FormatAdTimeSubtitle(ad);
        var charCount = PostLimits.CharacterCountLabel(ad.Platform, ad.PostText);
        var focusClass = focus == $"ad-{ad.Id}" ? " post-card-focused" : "";

        return $"""
            <article class="post-card{focusClass}" id="ad-{ad.Id}">
                <div class="post-card-cover">{cover}</div>
                <div class="post-card-header">
                    <div>
                        <strong>{H.Encode(ad.BookTitle)}</strong>
                        <small>{H.Encode(timeSubtitle)}</small>
                    </div>
                    {statusBadge}
                </div>
                <p class="platform-tag">{H.Encode(ad.Platform)}{charCount}</p>
                <p>{H.Encode(ad.PostText)}</p>
                {postErrorNote}
                {autoPostHint}
                <textarea id="{copyId}" class="copy-source" readonly>{H.Encode(ad.PostText)}</textarea>
                <div class="post-card-actions">
                    <button class="button secondary small copy-button" type="button" onclick="copyPostText('{copyId}', this)">Copy post</button>
                    {regenButton}
                    {postNowButton}
                    {approveButton}
                </div>
            </article>
            """;
    }
}
