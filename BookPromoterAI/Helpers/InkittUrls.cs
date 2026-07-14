namespace BookPromoterAI;

/// <summary>Inkitt author profile / wall URLs (no public posting API).</summary>
static class InkittUrls
{
    public const string PlatformName = "Inkitt";

    static readonly HashSet<string> NonProfileSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "stories", "api", "writers", "analytics", "search", "groups", "login", "signup"
    };

    public static string? ProfileWallUrl(string? handleOrUrl)
    {
        var username = ExtractUsername(handleOrUrl);
        return username is null ? null : $"https://www.inkitt.com/{Uri.EscapeDataString(username)}";
    }

    public static string? ExtractUsername(string? handleOrUrl)
    {
        if (string.IsNullOrWhiteSpace(handleOrUrl)) return null;
        var s = handleOrUrl.Trim().TrimStart('@');

        if (s.Contains("inkitt.com", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(
                    s.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? s : "https://" + s.TrimStart('/'),
                    UriKind.Absolute,
                    out var uri))
                return null;

            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) return null;
            var first = segments[0];
            if (NonProfileSegments.Contains(first)) return null;
            return SanitizeUsername(first);
        }

        if (s.Contains('/') || s.Contains(' ') || s.Contains('.')) return null;
        return SanitizeUsername(s);
    }

    static string? SanitizeUsername(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    public static string? SuggestUsernameFromBooks(IEnumerable<Book> books)
    {
        foreach (var book in books)
        {
            foreach (var link in book.Links)
            {
                if (!link.StoreName.Equals(PlatformName, StringComparison.OrdinalIgnoreCase)) continue;
                var username = ExtractUsername(link.Url);
                if (username is not null) return username;
            }
        }
        return null;
    }
}
