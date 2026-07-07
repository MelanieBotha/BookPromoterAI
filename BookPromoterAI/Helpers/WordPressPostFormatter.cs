namespace BookPromoterAI;

static class WordPressPostFormatter
{
    public static string BuildTitle(string postText, string? bookTitle, bool isBrand)
    {
        if (!string.IsNullOrWhiteSpace(bookTitle))
            return Truncate(bookTitle.Trim(), 120);
        if (isBrand)
            return "BookPromoter AI";

        foreach (var rawLine in postText.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;
            if (line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                continue;
            return Truncate(line, 120);
        }

        return "Book promo";
    }

    public static string ToHtmlContent(string postText, string appBaseUrl, bool isBrand) =>
        TumblrPostFormatter.ToHtmlCaption(postText, appBaseUrl, includeAppCta: !isBrand);

    static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";
}
