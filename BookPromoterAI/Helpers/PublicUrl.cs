namespace BookPromoterAI;

static class PublicUrl
{
    /// <summary>Branded site URL for links in posts, emails, and footers (e.g. https://bookpromoterai.us).</summary>
    public static string Base(HttpRequest request, AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.PublicBaseUrl))
            return settings.PublicBaseUrl.TrimEnd('/');

        return Local(request);
    }

    /// <summary>Current server URL — use for uploaded images and other local assets.</summary>
    public static string Local(HttpRequest request) =>
        $"{request.Scheme}://{request.Host}";

    /// <summary>
    /// Facebook OAuth callback base URL. Must match the host the user is browsing
    /// (each host needs its own entry in Meta Valid OAuth Redirect URIs).
    /// </summary>
    public static string FacebookOAuthBase(HttpRequest request) =>
        Local(request).TrimEnd('/');

    public static string FacebookCallbackUrl(HttpRequest request) =>
        $"{FacebookOAuthBase(request)}{FacebookService.CallbackPath}";

    public static IEnumerable<string> FacebookCallbackUrlsForMeta(AppSettings settings)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var baseUrl in new[]
                 {
                     settings.PublicBaseUrl.TrimEnd('/'),
                     "https://bookpromoterai.us",
                     "https://bookpromoterai-production.up.railway.app"
                 })
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) continue;
            var callback = FacebookService.CallbackUrl(baseUrl);
            if (seen.Add(callback)) yield return callback;
        }
    }
}
