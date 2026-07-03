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
        $"{EffectiveScheme(request)}://{request.Host}";

    /// <summary>
    /// Facebook OAuth callback base URL. Must be HTTPS in production (Meta rejects http redirect_uri).
    /// </summary>
    public static string FacebookOAuthBase(HttpRequest request) =>
        $"{EffectiveScheme(request, forceHttps: true)}://{request.Host}".TrimEnd('/');

    public static string FacebookCallbackUrl(HttpRequest request, AppSettings? settings = null)
    {
        if (settings is not null && !string.IsNullOrWhiteSpace(settings.PublicBaseUrl))
            return FacebookService.CallbackUrl(settings.PublicBaseUrl.TrimEnd('/'));
        return $"{FacebookOAuthBase(request)}{FacebookService.CallbackPath}";
    }

    static string EffectiveScheme(HttpRequest request, bool forceHttps = false)
    {
        if (request.IsHttps) return "https";
        if (string.Equals(request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase))
            return "https";
        if (forceHttps && IsProductionHost(request.Host.Host))
            return "https";
        return request.Scheme;
    }

    static bool IsProductionHost(string host) =>
        host.Contains("railway.app", StringComparison.OrdinalIgnoreCase)
        || host.Contains("bookpromoterai.us", StringComparison.OrdinalIgnoreCase);

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
