namespace BookPromoterAI;

static class PostBranding
{
    public const string LogoPath = "/images/BookPromoterAI.logo.png";

    /// <summary>Primary Amazon/store URL saved on the book — used for redirects from /go/ links.</summary>
    public static string? PrimaryPurchaseUrl(Book book) =>
        book.Links
            .Select(l => l.Url?.Trim())
            .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url) && UrlSafety.IsSafeRedirect(url));

    /// <summary>Link embedded in generated posts — book landing page tracks clicks and shows cover before store links.</summary>
    public static string PurchaseUrlForPost(Book book, string appBaseUrl) =>
        PrimaryPurchaseUrl(book) is null
            ? ""
            : BookShareUrl(appBaseUrl, book.TrackingCode);

    public static string BuildBookShareMeta(Book book, string pageUrl, string assetBaseUrl)
    {
        var coverUrl = AbsoluteImageUrl(assetBaseUrl, book.CoverImageUrl);
        var description = string.IsNullOrWhiteSpace(book.Description)
            ? $"Discover {book.Title} by {book.AuthorName}"
            : book.Description;
        if (description.Length > 200)
            description = description[..197] + "...";

        var imageMeta = string.IsNullOrWhiteSpace(coverUrl)
            ? ""
            : $"""
                <meta property="og:image" content="{WebEncode(coverUrl)}">
                <meta property="og:image:secure_url" content="{WebEncode(coverUrl)}">
                <meta property="og:image:alt" content="{WebEncode($"{book.Title} cover")}">
                <link rel="image_src" href="{WebEncode(coverUrl)}">
                <meta name="twitter:image" content="{WebEncode(coverUrl)}">
                """;

        return $"""
            <meta property="og:type" content="website">
            <meta property="og:site_name" content="{WebEncode(book.Title)}">
            <meta property="og:title" content="{WebEncode(book.Title)}">
            <meta property="og:description" content="{WebEncode(description)}">
            <meta property="og:url" content="{WebEncode(pageUrl)}">
            {imageMeta}
            <meta name="twitter:card" content="summary_large_image">
            <meta name="twitter:title" content="{WebEncode(book.Title)}">
            <meta name="twitter:description" content="{WebEncode(description)}">
            """;
    }

    public static string RenderCrawlerPreviewHtml(Book book, string pageUrl, string assetBaseUrl)
    {
        var ogMeta = BuildBookShareMeta(book, pageUrl, assetBaseUrl);
        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <title>{WebEncode(book.Title)}</title>
                <link rel="canonical" href="{WebEncode(pageUrl)}">
                {ogMeta}
            </head>
            <body></body>
            </html>
            """;
    }

    static string WebEncode(string value) => System.Net.WebUtility.HtmlEncode(value);

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
