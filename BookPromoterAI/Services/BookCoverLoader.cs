namespace BookPromoterAI;

/// <summary>Loads book cover bytes for attaching to Bluesky posts.</summary>
static class BookCoverLoader
{
    const int MaxBytes = 900_000;

    public static async Task<BlueskyImageAttachment?> TryLoadAsync(
        HttpClient http,
        string uploadsDir,
        string appBaseUrl,
        string bookTitle,
        string? coverImageUrl,
        string? trackingCode,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(coverImageUrl) &&
            coverImageUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var local = TryReadLocal(uploadsDir, coverImageUrl, bookTitle);
            if (local is not null) return local;
        }

        if (!string.IsNullOrWhiteSpace(trackingCode))
        {
            var fromShare = await TryDownloadAsync(
                http,
                PostBranding.BookCoverShareUrl(appBaseUrl, trackingCode),
                bookTitle,
                cancellationToken);
            if (fromShare is not null) return fromShare;
        }

        if (!string.IsNullOrWhiteSpace(coverImageUrl) &&
            (coverImageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             coverImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            return await TryDownloadAsync(http, coverImageUrl, bookTitle, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(coverImageUrl))
        {
            var absolute = PostBranding.AbsoluteImageUrl(appBaseUrl, coverImageUrl);
            return await TryDownloadAsync(http, absolute, bookTitle, cancellationToken);
        }

        return null;
    }

    static BlueskyImageAttachment? TryReadLocal(string uploadsDir, string coverImageUrl, string bookTitle)
    {
        var path = Path.Combine(uploadsDir, Path.GetFileName(coverImageUrl));
        if (!File.Exists(path)) return null;

        var info = CoverImageInfo.TryGetLocal(uploadsDir, coverImageUrl);
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0 || bytes.Length > MaxBytes) return null;

        var mime = info?.ContentType ?? GuessMime(path);
        if (!IsSupportedMime(mime)) return null;

        return new BlueskyImageAttachment(
            bytes,
            mime,
            $"{bookTitle} cover",
            info is { Width: > 0, Height: > 0 } ? info.Value.Width : null,
            info is { Width: > 0, Height: > 0 } ? info.Value.Height : null);
    }

    static async Task<BlueskyImageAttachment?> TryDownloadAsync(
        HttpClient http,
        string url,
        string bookTitle,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var mime = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            if (!IsSupportedMime(mime)) return null;

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length == 0 || bytes.Length > MaxBytes) return null;

            return new BlueskyImageAttachment(bytes, mime, $"{bookTitle} cover", null, null);
        }
        catch
        {
            return null;
        }
    }

    static bool IsSupportedMime(string mime) =>
        mime.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
        mime.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
        mime.Equals("image/webp", StringComparison.OrdinalIgnoreCase);

    static string GuessMime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "image/jpeg"
    };
}

record BlueskyImageAttachment(byte[] Data, string MimeType, string AltText, int? Width, int? Height);

record BookPostMedia(string? CoverImageUrl, string? TrackingCode, string BookTitle, string AppBaseUrl);
