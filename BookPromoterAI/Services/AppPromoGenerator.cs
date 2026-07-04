namespace BookPromoterAI;

static class AppPromoGenerator
{
    static readonly string[] Hooks =
    [
        "Authors: promote your books with AI-generated social posts, click tracking, and a weekly Ad Library.",
        "Stop writing posts from scratch — BookPromoter AI creates platform-ready captions for your books.",
        "One dashboard for books, social posts, scheduling, and reader mailing lists. Built for indie authors.",
        "Your next book deserves better marketing. BookPromoter AI helps you post consistently without the grind.",
        "Schedule book promos across Facebook, Reddit, X, and more — with covers attached automatically.",
        "Turn your backlist into a steady stream of social posts. BookPromoter AI does the heavy lifting.",
        "Indie authors: AI captions, book covers in every post, and click tracking in one place.",
        "Marketing your books shouldn't eat your writing time. Let BookPromoter AI handle the posts."
    ];

    static readonly string[] Tags =
    [
        "#Authors #BookMarketing #IndieAuthor",
        "#IndieAuthor #BookPromo #WritingCommunity",
        "#Authors #Books #BookMarketing",
        "#IndieAuthors #AmWriting #BookPromo"
    ];

    static readonly string[] Platforms = ["Facebook", "Reddit", "X", "LinkedIn", "Bluesky"];

    public static IReadOnlyList<string> SupportedPlatforms => Platforms;

    public static Dictionary<string, string> GeneratePromoPosts(string appBaseUrl, int seed = 0)
    {
        var baseSeed = seed == 0 ? Random.Shared.Next() : seed;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < Platforms.Length; i++)
            result[Platforms[i]] = GeneratePromoPost(Platforms[i], appBaseUrl, baseSeed + i * 7919);
        return result;
    }

    public static string GeneratePromoPost(string platform, string appBaseUrl, int? seed = null)
    {
        var s = Math.Abs(seed ?? Random.Shared.Next());
        var url = appBaseUrl.TrimEnd('/');
        var startUrl = $"{url}/start";
        var trialUrl = $"{url}/trial";
        var hook = Hooks[s % Hooks.Length];
        var tags = Tags[(s / Hooks.Length) % Tags.Length];
        var useTrialCta = s % 3 != 0;

        return GeneratePost(platform, hook, startUrl, trialUrl, tags, useTrialCta);
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

    public static (string Subject, string Body) GeneratePromoEmail(string appBaseUrl, int seed = 0)
    {
        var s = seed == 0 ? Random.Shared.Next() : seed;
        var url = appBaseUrl.TrimEnd('/');
        var hook = Hooks[Math.Abs(s) % Hooks.Length];
        var subject = Math.Abs(s) % 2 == 0
            ? "Promote your books smarter with BookPromoter AI"
            : "BookPromoter AI — tips for promoting your books";
        var body = $"""
            Hi,

            {hook}

            Start here: {url}/start
            Free access code: {url}/trial

            — The BookPromoter AI Team
            """;
        return (subject, body.Trim());
    }

    static string GeneratePost(string platform, string hook, string startUrl, string trialUrl, string tags, bool useTrialCta)
    {
        if (PostLimits.IsX(platform))
        {
            var cta = useTrialCta ? trialUrl : startUrl;
            return PostLimits.Enforce($"{hook} {cta} {tags}", platform);
        }

        if (PostLimits.IsBluesky(platform))
        {
            var cta = useTrialCta ? $"Free access code: {trialUrl}" : $"Get started: {startUrl}";
            return PostLimits.Enforce($"{hook}\n{cta}\n#Books #Authors", platform);
        }

        if (platform.Equals("Reddit", StringComparison.OrdinalIgnoreCase))
        {
            var cta = useTrialCta
                ? $"Start free with an access code:\n{trialUrl}"
                : $"Create your account:\n{startUrl}";
            return PostLimits.Enforce($"{hook}\n\n{cta}\n\n#selfpublish #writing #books", platform);
        }

        var bodyCta = useTrialCta
            ? $"Free access code: {trialUrl}\nCreate your account: {startUrl}"
            : $"Create your account: {startUrl}\nFree access code: {trialUrl}";
        return PostLimits.Enforce($"{hook}\n\n{bodyCta}\n\n— BookPromoter AI", platform);
    }

    public static List<string> ParseLines(string? text) =>
        (text ?? "")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
}
