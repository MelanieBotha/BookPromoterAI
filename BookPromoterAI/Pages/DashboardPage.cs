using System.Text;
namespace BookPromoterAI;

static class DashboardPage
{
    public static string Render(AppStoreDb store, PostGenerator generator, HttpRequest request, AppSettings settings, string notice = "")
    {
        var totalPostsPerWeek = store.Schedules.Sum(s => s.PostsPerWeek);

        // One stat card per book showing its individual click count
        var bookStats = new StringBuilder();
        if (store.Books.Count == 0)
        {
            bookStats.Append("""<div class="stat-empty"><span>No books yet</span><small>Add your first book to see stats</small></div>""");
        }
        else
        {
            foreach (var book in store.Books)
            {
                bookStats.Append($"""
                    <div>
                        <span>{ClickAnalytics.ClicksThisMonth(book)}</span>
                        <small>{H.Encode(book.Title)}</small>
                        <p class="stat-sub">clicks this month</p>
                    </div>
                    """);
            }
        }

        var totalClicksThisMonth = ClickAnalytics.TotalClicksThisMonth(store.Books);
        var topBookTitle = ClickAnalytics.TopBookThisMonth(store.Books)?.Title ?? "None yet";

        var bookCards = new StringBuilder();
        if (store.Books.Count == 0)
        {
            bookCards.Append("""
                <section class="panel">
                    <article class="post-card">
                        <strong>No books yet</strong>
                        <p>Add a book before generating social media posts.</p>
                    </article>
                </section>
                """);
        }
        else if (store.HasMultiClient && store.Clients.Count > 0)
        {
            // Multi-client: one panel per author/client
            foreach (var client in store.Clients)
            {
                var clientBooks = store.Books.Where(b => b.ClientId == client.Id).ToList();
                if (clientBooks.Count == 0) continue;

                var cards = new StringBuilder();
                foreach (var book in clientBooks)
                    cards.Append(BookCard(store, generator, request, settings, book));

                bookCards.Append($"""
                    <section class="panel">
                        <h2>{H.Encode(client.Name)}</h2>
                        <p class="muted small-text">{H.Encode(client.ContactEmail)}</p>
                        <div class="post-grid">{cards}</div>
                    </section>
                    """);
            }

            // Unmatched books
            var unmatched = store.Books.Where(b => b.ClientId is null).ToList();
            if (unmatched.Count > 0)
            {
                var cards = new StringBuilder();
                foreach (var book in unmatched)
                    cards.Append(BookCard(store, generator, request, settings, book));

                bookCards.Append($"""
                    <section class="panel">
                        <h2>Unassigned Books</h2>
                        <p class="muted small-text">These books don't match any client name. Edit the book's author name to match a client, or add a matching client on the Clients page.</p>
                        <div class="post-grid">{cards}</div>
                    </section>
                    """);
            }
        }
        else
        {
            // Flat grid for non-multi-client plans
            var cards = new StringBuilder();
            foreach (var book in store.Books)
                cards.Append(BookCard(store, generator, request, settings, book));

            bookCards.Append($"""
                <section class="panel">
                    <h2>All Books &amp; Generated Posts</h2>
                    <div class="post-grid">{cards}</div>
                </section>
                """);
        }

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Author marketing dashboard</p>
                    <h1>Your books, posts, schedules, and clicks in one place.</h1>
                </div>
                <div class="hero-actions">
                    <a class="button" href="/books">Add a Book</a>
                    <a class="button secondary" href="/logout">Log Out</a>
                </div>
            </section>

            {notice}

            <section class="stats">
                <div><span>{store.Books.Count}</span><small>Books</small></div>
                <div><span>{totalPostsPerWeek}</span><small>Posts per week</small></div>
                <div><span>{totalClicksThisMonth}</span><small>Total clicks this month</small></div>
                <div><span>{H.Encode(topBookTitle)}</span><small>Top book this month</small></div>
            </section>

            <section class="panel">
                <h2>Clicks Per Book</h2>
                <p class="muted small-text">Monthly clicks tracked per book via your unique tracking links.</p>
                <div class="book-stats-grid">
                    {bookStats}
                </div>
            </section>

            {bookCards}
            """;
    }

    static string BookCard(AppStoreDb store, PostGenerator generator, HttpRequest request, AppSettings settings, Book book)
    {
        var baseUrl = PublicUrl.Base(request, settings);
        var now = DateTime.UtcNow;
        var (currentWeek, currentYear, _) = AdWeek.For(now);
        var activeSchedules = store.Schedules.Where(s => s.PostsPerWeek > 0).ToList();
        var platform = activeSchedules.FirstOrDefault()?.Platform ?? "General";
        var purchaseUrl = PostBranding.PurchaseUrlForPost(book, baseUrl, platform);
        var text = generator.Generate(book, platform, purchaseUrl, book.PostVariantSeed, baseUrl);

        var bookAds = store.GeneratedAds
            .Where(a => a.BookId == book.Id)
            .OrderByDescending(a => a.WeekYear == currentYear && a.WeekNumber == currentWeek ? 1 : 0)
            .ThenByDescending(a => a.GeneratedAt)
            .ToList();

        var displayAd = bookAds.FirstOrDefault(a => a.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase))
            ?? bookAds.FirstOrDefault();
        var displayText = displayAd is not null && !string.IsNullOrWhiteSpace(displayAd.PostText)
            ? displayAd.PostText
            : text;
        if (displayAd is not null)
            platform = displayAd.Platform;

        var cover = string.IsNullOrWhiteSpace(book.CoverImageUrl)
            ? """<div class="cover-placeholder large">No cover</div>"""
            : $"""<img class="book-cover large" src="{H.Encode(book.CoverImageUrl)}" alt="{H.Encode(book.Title)} cover">""";

        return $"""
            <article class="post-card">
                <div class="post-card-cover">{cover}</div>
                <div class="post-card-header">
                    <div>
                        <strong>{H.Encode(book.Title)}</strong>
                        <small>{ClickAnalytics.ClicksThisMonth(book)} clicks this month</small>
                    </div>
                    {HeaderStatusBadge(displayAd)}
                </div>
                {PlatformStatusLines(activeSchedules, bookAds, platform, displayAd)}
                <p>{H.Encode(displayText)}</p>
                <form method="post" action="/books/{book.Id}/regenerate-post">
                    <button class="button secondary small" type="submit">Generate New Post</button>
                </form>
            </article>
            """;
    }

    static string HeaderStatusBadge(GeneratedAd? ad) =>
        ad?.PostStatus == "Posted"
            ? """<span class="status available">Posted</span>"""
            : ad?.PostStatus == "Failed"
                ? """<span class="status used">Failed</span>"""
                : "";

    static string PlatformStatusLines(
        List<SocialSchedule> activeSchedules,
        List<GeneratedAd> bookAds,
        string fallbackPlatform,
        GeneratedAd? displayAd)
    {
        if (activeSchedules.Count == 0)
        {
            var line = RenderPlatformStatusLine(fallbackPlatform, displayAd);
            return $"""<p class="platform-tag">{line}</p>""";
        }

        var lines = new StringBuilder();
        foreach (var schedule in activeSchedules)
        {
            var ad = bookAds.FirstOrDefault(a => a.Platform.Equals(schedule.Platform, StringComparison.OrdinalIgnoreCase));
            lines.Append($"""<p class="platform-tag">{RenderPlatformStatusLine(schedule.Platform, ad)}</p>""");
        }
        return lines.ToString();
    }

    static string RenderPlatformStatusLine(string platform, GeneratedAd? ad) =>
        ad?.PostStatus switch
        {
            "Posted" => $"""<span class="status available">Posted</span> <span class="muted">· {H.Encode(platform)}{(ad.PostedAt is DateTime posted ? $" · {AppTimeZone.FormatWithZone(posted, "ddd MMM d, HH:mm")}" : "")}</span>""",
            "Failed" => $"""<span class="status used">Failed</span> <span class="muted">· {H.Encode(platform)}</span>""",
            _ when ad is not null => $"""{H.Encode(platform)} <span class="muted">· Pending</span>""",
            _ => H.Encode(platform)
        };
}
