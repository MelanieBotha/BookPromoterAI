namespace BookPromoterAI;

static class UrlSafety
{
    public static bool IsSafeRedirect(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
        return uri.Scheme is "http" or "https";
    }
}
