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
    public static string PurchaseUrlForPost(Book book, string appBaseUrl, string? platform = null) =>
        PrimaryPurchaseUrl(book) is null
            ? ""
            : BookShareUrl(appBaseUrl, book.TrackingCode, platform);

    public static string BuildBookShareMeta(Book book, string pageUrl, string assetBaseUrl, (int Width, int Height)? imageSize = null)
    {
        var hasCover = !string.IsNullOrWhiteSpace(book.CoverImageUrl);
        var coverUrl = hasCover ? EnsureHttps(BookCoverShareUrl(assetBaseUrl, book.TrackingCode)) : "";
        var description = string.IsNullOrWhiteSpace(book.Description)
            ? $"Discover {book.Title} by {book.AuthorName}"
            : book.Description;
        if (description.Length > 200)
            description = description[..197] + "...";

        var cardType = hasCover ? "summary_large_image" : "summary";
        var imageType = GuessImageType(book.CoverImageUrl);
        var sizeMeta = imageSize is { Width: > 0, Height: > 0 } size
            ? $"""
                <meta property="og:image:width" content="{size.Width}">
                <meta property="og:image:height" content="{size.Height}">
                """
            : "";

        var imageMeta = string.IsNullOrWhiteSpace(coverUrl)
            ? ""
            : $"""
                <meta property="og:image" content="{MetaContent(coverUrl)}">
                <meta property="og:image:secure_url" content="{MetaContent(coverUrl)}">
                <meta property="og:image:type" content="{MetaContent(imageType)}">
                <meta property="og:image:alt" content="{MetaContent($"{book.Title} cover")}">
                {sizeMeta}
                <link rel="image_src" href="{MetaContent(coverUrl)}">
                <meta name="twitter:image" content="{MetaContent(coverUrl)}">
                <meta name="twitter:image:alt" content="{MetaContent($"{book.Title} cover")}">
                """;

        return $"""
            <meta name="twitter:card" content="{cardType}">
            <meta name="twitter:url" content="{MetaContent(pageUrl)}">
            <meta name="twitter:title" content="{MetaContent(book.Title)}">
            <meta name="twitter:description" content="{MetaContent(description)}">
            {imageMeta}
            <meta property="og:type" content="website">
            <meta property="og:site_name" content="{MetaContent(book.Title)}">
            <meta property="og:title" content="{MetaContent(book.Title)}">
            <meta property="og:description" content="{MetaContent(description)}">
            <meta property="og:url" content="{MetaContent(pageUrl)}">
            """;
    }

    public static string RenderCrawlerPreviewHtml(Book book, string pageUrl, string assetBaseUrl, (int Width, int Height)? imageSize = null)
    {
        var ogMeta = BuildBookShareMeta(book, pageUrl, assetBaseUrl, imageSize);
        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <title>{MetaContent(book.Title)}</title>
                <link rel="canonical" href="{MetaContent(pageUrl)}">
                {ogMeta}
            </head>
            <body></body>
            </html>
            """;
    }

    static string MetaContent(string value) => System.Net.WebUtility.HtmlEncode(value);

    static string EnsureHttps(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? "https://" + url[7..]
            : url;

    static string GuessImageType(string? coverUrl)
    {
        if (string.IsNullOrWhiteSpace(coverUrl)) return "image/jpeg";
        return Path.GetExtension(coverUrl).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
    }

    public static string TrackingRedirectUrl(string appBaseUrl, string trackingCode) =>
        $"{appBaseUrl.TrimEnd('/')}/go/{trackingCode}";

    public static string BookShareUrl(string appBaseUrl, string trackingCode, string? platform = null)
    {
        var url = $"{appBaseUrl.TrimEnd('/')}/book/{trackingCode}";
        var slug = PlatformClickSource.SlugForPlatform(platform);
        return string.IsNullOrWhiteSpace(slug) ? url : $"{url}?from={Uri.EscapeDataString(slug)}";
    }

    /// <summary>Stable cover URL for social crawlers (Twitter, Facebook) on the book share path.</summary>
    public static string BookCoverShareUrl(string appBaseUrl, string trackingCode) =>
        $"{appBaseUrl.TrimEnd('/')}/book/{trackingCode}/cover";

    public static string AbsoluteLogoUrl(string appBaseUrl) =>
        $"{appBaseUrl.TrimEnd('/')}{LogoPath}";

    public static string LogoUrlForSite(string? appBaseUrl) =>
        string.IsNullOrWhiteSpace(appBaseUrl) ? LogoPath : AbsoluteLogoUrl(appBaseUrl.TrimEnd('/'));

    public static string AbsoluteImageUrl(string appBaseUrl, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return "";
        if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return imageUrl;
        return $"{appBaseUrl.TrimEnd('/')}/{imageUrl.TrimStart('/')}";
    }

    /// <summary>Public cover + landing URLs for author mailing list HTML emails.</summary>
    public static (string CoverUrl, string LinkUrl) MailingListCoverUrls(Book book, string appBaseUrl)
    {
        var linkUrl = PurchaseUrlForPost(book, appBaseUrl);
        if (string.IsNullOrWhiteSpace(book.CoverImageUrl))
            return ("", linkUrl);

        var coverUrl = !string.IsNullOrWhiteSpace(book.TrackingCode)
            ? EnsureHttps(BookCoverShareUrl(appBaseUrl, book.TrackingCode))
            : EnsureHttps(AbsoluteImageUrl(appBaseUrl, book.CoverImageUrl));
        return (coverUrl, linkUrl);
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
