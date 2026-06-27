using System.Text;
namespace BookPromoterAI;

static class DashboardPage
{
    public static string Render(AppStoreDb store, PostGenerator generator, HttpRequest request, AppSettings settings)
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
                        <span>{book.MonthlyClicks}</span>
                        <small>{H.Encode(book.Title)}</small>
                        <p class="stat-sub">clicks this month</p>
                    </div>
                    """);
            }
        }

        var totalClicks = store.Books.Sum(b => b.MonthlyClicks);
        var topBook = store.Books.OrderByDescending(b => b.MonthlyClicks).FirstOrDefault()?.Title ?? "None yet";

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

            <section class="stats">
                <div><span>{store.Books.Count}</span><small>Books</small></div>
                <div><span>{totalPostsPerWeek}</span><small>Posts per week</small></div>
                <div><span>{totalClicks}</span><small>Total monthly clicks</small></div>
                <div><span>{H.Encode(topBook)}</span><small>Top book</small></div>
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
        var purchaseUrl = PostBranding.PrimaryPurchaseUrl(book) ?? "";
        var schedule = store.Schedules.FirstOrDefault(s => s.PostsPerWeek > 0);
        var platform = schedule?.Platform ?? "General";
        var text = generator.Generate(book, platform, purchaseUrl, book.PostVariantSeed, baseUrl);
        var cover = string.IsNullOrWhiteSpace(book.CoverImageUrl)
            ? """<div class="cover-placeholder large">No cover</div>"""
            : $"""<img class="book-cover large" src="{H.Encode(book.CoverImageUrl)}" alt="{H.Encode(book.Title)} cover">""";

        return $"""
            <article class="post-card">
                <div class="post-card-cover">{cover}</div>
                <div class="post-card-header">
                    <div>
                        <strong>{H.Encode(book.Title)}</strong>
                        <small>{book.MonthlyClicks} clicks this month</small>
                    </div>
                </div>
                <p class="platform-tag">{H.Encode(platform)}</p>
                <p>{H.Encode(text)}</p>
                <form method="post" action="/books/{book.Id}/regenerate-post">
                    <button class="button secondary small" type="submit">Generate New Post</button>
                </form>
            </article>
            """;
    }
}
