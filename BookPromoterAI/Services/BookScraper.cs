namespace BookPromoterAI;

// =====================================================================
// BOOK INFO SCRAPER
//
// Fetches a book's listing page (Amazon, Goodreads, Kindle, etc.) and
// extracts metadata using Open Graph tags and platform-specific HTML
// patterns. This runs server-side so there are no cross-origin issues.
//
// NOTE: Amazon and some other retailers actively block automated
// requests. If a fetch fails or returns incomplete data, the fields
// are left empty so the user can fill them in manually.
// =====================================================================
class BookScraper
{
    private readonly HttpClient _http;

    public BookScraper()
    {
        _http = new HttpClient();
        // Identify as a browser to avoid being immediately blocked
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<BookScrapeResult> ScrapeAsync(string url)
    {
        var result = new BookScrapeResult { SourceUrl = url };

        try
        {
            var html = await _http.GetStringAsync(url);
            result.Title = ExtractMeta(html, "og:title")
                        ?? ExtractMeta(html, "twitter:title")
                        ?? ExtractTag(html, "title");

            result.Description = ExtractMeta(html, "og:description")
                               ?? ExtractMeta(html, "twitter:description")
                               ?? ExtractMeta(html, "description");

            result.CoverImageUrl = ExtractMeta(html, "og:image")
                                ?? ExtractMeta(html, "twitter:image");

            // Try to extract author from common meta/schema patterns
            result.AuthorName = ExtractMeta(html, "book:author")
                             ?? ExtractSchemaAuthor(html)
                             ?? ExtractByItemprop(html, "author");

            // Try to extract genre/category
            result.Genre = ExtractMeta(html, "book:genre")
                        ?? ExtractSchemaGenre(html);

            // Clean up title — Amazon appends " - Kindle Edition" etc.
            if (!string.IsNullOrWhiteSpace(result.Title))
            {
                result.Title = CleanTitle(result.Title);
            }

            // Clean up description — trim to 200 words
            if (!string.IsNullOrWhiteSpace(result.Description))
            {
                var words = result.Description.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 200)
                    result.Description = string.Join(' ', words.Take(200));
            }

            result.Success = !string.IsNullOrWhiteSpace(result.Title);
            result.Message = result.Success
                ? "Book details fetched. Review and edit before saving."
                : "Could not extract book details from that URL. Please fill in the fields manually.";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Could not fetch that URL: {ex.Message} Please fill in the fields manually.";
        }

        return result;
    }

    // ── Extraction helpers ─────────────────────────────────────────────

    static string? ExtractMeta(string html, string property)
    {
        // Matches both name= and property= variants
        foreach (var attr in new[] { "property", "name" })
        {
            var patterns = new[]
            {
                $"""<meta {attr}="{property}" content="([^"]+)""",
                $"""<meta content="([^"]+)" {attr}="{property}"""
            };
            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(html, pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                    return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
            }
        }
        return null;
    }

    static string? ExtractTag(string html, string tag)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            html, $"<{tag}[^>]*>([^<]+)</{tag}>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success
            ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim())
            : null;
    }

    static string? ExtractByItemprop(string html, string prop)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            html, $"""itemprop="{prop}"[^>]*>([^<]+)<""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success
            ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim())
            : null;
    }

    static string? ExtractSchemaAuthor(string html)
    {
        // JSON-LD schema.org pattern: "author":{"name":"John Smith"}
        var match = System.Text.RegularExpressions.Regex.Match(
            html, """"author"\s*:\s*\{\s*"name"\s*:\s*"([^"]+)"""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    static string? ExtractSchemaGenre(string html)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            html, """"genre"\s*:\s*"([^"]+)"""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    static string CleanTitle(string title)
    {
        // Remove common suffixes appended by book retailers
        var suffixes = new[]
        {
            " - Kindle Edition", " (Kindle Edition)", ": Kindle Edition",
            " - Amazon.com", " | Amazon", " - Goodreads",
            " - Google Play", " - Apple Books", " - Kobo",
            " - Barnes & Noble", " eBook"
        };
        foreach (var suffix in suffixes)
            if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                title = title[..^suffix.Length].Trim();
        return title;
    }
}

class BookScrapeResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string? Title { get; set; }
    public string? AuthorName { get; set; }
    public string? Description { get; set; }
    public string? Genre { get; set; }
    public string? CoverImageUrl { get; set; }
}
