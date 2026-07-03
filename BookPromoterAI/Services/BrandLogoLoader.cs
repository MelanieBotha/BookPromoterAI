namespace BookPromoterAI;

/// <summary>Loads the BookPromoter AI logo for brand Bluesky posts.</summary>
static class BrandLogoLoader
{
    const string LogoFileName = "BookPromoterAI.logo.png";
    const int MaxBytes = 900_000;

    public static async Task<BlueskyImageAttachment?> TryLoadAsync(
        HttpClient http,
        string appBaseUrl,
        CancellationToken cancellationToken = default)
    {
        foreach (var path in CandidateLogoPaths())
        {
            if (!File.Exists(path)) continue;
            try
            {
                var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                if (bytes.Length == 0 || bytes.Length > MaxBytes) continue;
                return new BlueskyImageAttachment(bytes, "image/png", "BookPromoter AI", null, null);
            }
            catch
            {
                // try next path
            }
        }

        var baseUrl = string.IsNullOrWhiteSpace(appBaseUrl)
            ? "https://bookpromoterai.us"
            : appBaseUrl.TrimEnd('/');
        var url = PostBranding.AbsoluteLogoUrl(baseUrl);

        try
        {
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var mime = response.Content.Headers.ContentType?.MediaType ?? "image/png";
            if (!mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return null;

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length == 0 || bytes.Length > MaxBytes) return null;

            return new BlueskyImageAttachment(bytes, mime, "BookPromoter AI", null, null);
        }
        catch
        {
            return null;
        }
    }

    public static string PublicLogoUrl(string appBaseUrl) =>
        PostBranding.AbsoluteLogoUrl(string.IsNullOrWhiteSpace(appBaseUrl)
            ? "https://bookpromoterai.us"
            : appBaseUrl.TrimEnd('/'));

    static IEnumerable<string> CandidateLogoPaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "wwwroot", "images", LogoFileName);
        var cwd = Directory.GetCurrentDirectory();
        if (!string.IsNullOrWhiteSpace(cwd))
            yield return Path.Combine(cwd, "wwwroot", "images", LogoFileName);
    }
}

record BrandPostMedia(string AppBaseUrl);
