namespace BookPromoterAI;

static class PostBranding
{
    public const string LogoPath = "/images/BookPromoterAI.logo.png";

    /// <summary>Primary Amazon/store URL saved on the book — used in post text.</summary>
    public static string? PrimaryPurchaseUrl(Book book) =>
        book.Links
            .Select(l => l.Url?.Trim())
            .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url) && UrlSafety.IsSafeRedirect(url));

    public static string TrackingRedirectUrl(string appBaseUrl, string trackingCode) =>
        $"{appBaseUrl.TrimEnd('/')}/go/{trackingCode}";

    public static string BookShareUrl(string appBaseUrl, string trackingCode) =>
        $"{appBaseUrl.TrimEnd('/')}/book/{trackingCode}";

    public static string AbsoluteLogoUrl(string appBaseUrl) =>
        $"{appBaseUrl.TrimEnd('/')}{LogoPath}";

    public static string AbsoluteImageUrl(string appBaseUrl, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return "";
        if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return imageUrl;
        return $"{appBaseUrl.TrimEnd('/')}/{imageUrl.TrimStart('/')}";
    }

    public static string Footer(string platform, string appBaseUrl)
    {
        var startUrl = $"{appBaseUrl.TrimEnd('/')}/start";
        if (platform.Equals("X", StringComparison.OrdinalIgnoreCase) ||
            platform.StartsWith("X (", StringComparison.OrdinalIgnoreCase) ||
            PostLimits.IsBluesky(platform))
            return $"\n\nAuthors — promote your books: {startUrl}";
        return $"\n\n—\nAre you an author? Promote your books with BookPromoter AI.\n{startUrl}";
    }
}
