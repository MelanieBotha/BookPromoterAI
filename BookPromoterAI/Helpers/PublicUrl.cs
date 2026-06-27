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
}
