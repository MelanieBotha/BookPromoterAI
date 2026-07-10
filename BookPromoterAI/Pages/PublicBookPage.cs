using System.Text;

namespace BookPromoterAI;

static class PublicBookPage
{
    public static string Render(
        Book book,
        string appBaseUrl,
        string assetBaseUrl,
        IReadOnlyList<AuthorFollowLinks.Link>? followLinks = null)
    {
        var appUrl = appBaseUrl.TrimEnd('/');
        var cover = string.IsNullOrWhiteSpace(book.CoverImageUrl)
            ? """<div class="cover-placeholder large public-book-cover">No cover</div>"""
            : $"""<img class="book-cover large public-book-cover" src="{H.Encode(PostBranding.AbsoluteImageUrl(assetBaseUrl, book.CoverImageUrl))}" alt="{H.Encode(book.Title)} cover">""";

        var buyLinks = new StringBuilder();
        foreach (var link in book.Links.Where(l => !string.IsNullOrWhiteSpace(l.Url)))
        {
            buyLinks.Append($"""
                <a class="button" href="{H.Encode(link.Url)}" target="_blank" rel="noopener noreferrer">Buy on {H.Encode(link.StoreName)}</a>
                """);
        }
        if (buyLinks.Length == 0)
            buyLinks.Append("""<p class="muted">Purchase links coming soon.</p>""");

        var genre = string.IsNullOrWhiteSpace(book.Genre) ? "" : $"""<p class="platform-tag">{H.Encode(book.Genre)}</p>""";
        var followSection = RenderFollowSection(book.AuthorName, followLinks);

        return $"""
            <section class="public-book-page">
                <div class="public-book-hero panel">
                    {cover}
                    <div class="public-book-copy">
                        <p class="eyebrow">Featured book</p>
                        <h1>{H.Encode(book.Title)}</h1>
                        <p class="muted">by {H.Encode(book.AuthorName)}</p>
                        {genre}
                        <p class="public-book-description">{H.Encode(book.Description)}</p>
                        <div class="landing-cta-row public-book-buy">{buyLinks}</div>
                        {followSection}
                    </div>
                </div>

                <section class="panel public-book-author-cta">
                    <p class="eyebrow">For authors</p>
                    <h2>Promote your books like this — with AI-generated posts and click tracking.</h2>
                    <p class="muted">BookPromoter AI helps authors manage their catalog, create social posts, and grow readership.</p>
                    <div class="landing-cta-row">
                        <a class="button" href="/start">Create free account</a>
                        <a class="button secondary" href="/trial">Get 30-day access code</a>
                    </div>
                </section>
            </section>
            """;
    }

    static string RenderFollowSection(string authorName, IReadOnlyList<AuthorFollowLinks.Link>? followLinks)
    {
        if (followLinks is null || followLinks.Count == 0) return "";

        var buttons = new StringBuilder();
        foreach (var link in followLinks)
        {
            buttons.Append($"""
                <a class="button secondary" href="{H.Encode(link.Url)}" target="_blank" rel="noopener noreferrer">{H.Encode(link.Label)}</a>
                """);
        }

        var who = string.IsNullOrWhiteSpace(authorName) ? "the author" : authorName.Trim();
        return $"""
            <div class="public-book-follow">
                <p class="eyebrow">Follow me on</p>
                <p class="muted small-text">Stay in touch with {H.Encode(who)} on social and community channels.</p>
                <div class="landing-cta-row public-book-follow-links">{buttons}</div>
            </div>
            """;
    }
}
