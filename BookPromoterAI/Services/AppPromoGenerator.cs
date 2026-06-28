namespace BookPromoterAI;

static class AppPromoGenerator
{
    static readonly string[] Hooks =
    [
        "Authors: promote your books with AI-generated social posts, click tracking, and a weekly Ad Library.",
        "Stop writing posts from scratch — BookPromoter AI creates platform-ready captions for your books.",
        "One dashboard for books, social posts, scheduling, and reader mailing lists. Built for indie authors.",
        "Your next book deserves better marketing. BookPromoter AI helps you post consistently without the grind."
    ];

    static readonly string[] Platforms = ["Facebook", "Instagram", "X", "LinkedIn", "Bluesky"];

    public static IReadOnlyList<string> SupportedPlatforms => Platforms;

    public static Dictionary<string, string> GeneratePromoPosts(string appBaseUrl, int seed = 0)
    {
        var url = appBaseUrl.TrimEnd('/');
        var startUrl = $"{url}/start";
        var trialUrl = $"{url}/trial";
        var hook = Hooks[Math.Abs(seed) % Hooks.Length];

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var platform in Platforms)
            result[platform] = GeneratePost(platform, hook, startUrl, trialUrl);
        return result;
    }

    public static string GenerateUpdatePost(string platform, ProductUpdate update, string appBaseUrl)
    {
        var url = appBaseUrl.TrimEnd('/');
        var dashboardUrl = $"{url}/dashboard";
        var headline = string.IsNullOrWhiteSpace(update.Title)
            ? $"BookPromoter AI v{update.Version} is live"
            : update.Title;

        var highlights = new List<string>();
        highlights.AddRange(ParseLines(update.UpdatedItems).Take(2));
        highlights.AddRange(ParseLines(update.CreatedItems).Take(2));
        highlights.AddRange(ParseLines(update.AddedItems).Take(2));
        var bullet = highlights.Count > 0 ? highlights[0] : "New improvements for authors";

        if (PostLimits.IsX(platform))
            return PostLimits.Enforce($"🚀 {headline} — {bullet} {dashboardUrl} #Authors #BookMarketing", platform);

        if (PostLimits.IsBluesky(platform))
            return PostLimits.Enforce($"{headline}\n{bullet}\n{dashboardUrl}\n#Books #Authors", platform);

        return PostLimits.Enforce(
            $"{headline}\n\n{bullet}\n\nSee what's new: {dashboardUrl}\n\n— BookPromoter AI",
            platform);
    }

    public static Dictionary<string, string> GenerateUpdatePosts(ProductUpdate update, string appBaseUrl)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var platform in Platforms)
            result[platform] = GenerateUpdatePost(platform, update, appBaseUrl);
        return result;
    }

    static string GeneratePost(string platform, string hook, string startUrl, string trialUrl)
    {
        if (PostLimits.IsX(platform))
            return PostLimits.Enforce($"{hook} {startUrl} #Authors #BookMarketing #IndieAuthor", platform);

        if (PostLimits.IsBluesky(platform))
            return PostLimits.Enforce($"{hook}\n{startUrl}\n#Books #Authors", platform);

        if (platform.Equals("Instagram", StringComparison.OrdinalIgnoreCase))
            return PostLimits.Enforce(
                $"{hook}\n\nStart free with an access code:\n{trialUrl}\n\n#Bookstagram #Authors #IndieAuthor #BookMarketing",
                platform);

        return PostLimits.Enforce(
            $"{hook}\n\nCreate your account: {startUrl}\nFree access code: {trialUrl}\n\n— BookPromoter AI",
            platform);
    }

    public static List<string> ParseLines(string? text) =>
        (text ?? "")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
}
