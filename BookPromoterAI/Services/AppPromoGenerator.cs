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
        "Marketing your books shouldn't eat your writing time. Let BookPromoter AI handle the posts.",
        "Writers: keep creating while BookPromoter AI schedules promos with your cover art and store links.",
        "Launch week or backlist month — BookPromoter AI rotates fresh captions so you never repeat yourself.",
        "From catalog to clicks: manage books, auto-post to social, and track what drives readers to buy.",
        "BookPromoter AI is built for indie authors who want consistent promos without living on social media.",
        "Add your books once. Get platform-ready posts, mailing lists, and click stats in one author dashboard.",
        "Tired of blank-caption panic? BookPromoter AI generates hooks, hashtags, and links for every platform.",
        "Grow readership with scheduled book promos — covers attached, store links tracked, less manual work.",
        "Authors on BookPromoter AI post more often because the captions and images are already done for them."
    ];

    static readonly string[] Tags =
    [
        "#Authors #BookMarketing #IndieAuthor",
        "#IndieAuthor #BookPromo #WritingCommunity",
        "#Authors #Books #BookMarketing",
        "#IndieAuthors #AmWriting #BookPromo",
        "#SelfPublished #Authors #BookTok",
        "#IndieAuthor #WritersLife #Books",
        "#AuthorLife #BookMarketing #AmWriting",
        "#IndieAuthors #BookPromo #ReadingCommunity"
    ];

    /// <summary>Stable seed that changes each ISO week — auto-posts use a fresh caption every week.</summary>
    public static int WeeklyPromoSeed(DateTime utcNow, string platform, int postIndexInWeek = 0)
    {
        var week = System.Globalization.ISOWeek.GetWeekOfYear(utcNow);
        var year = System.Globalization.ISOWeek.GetYear(utcNow);
        unchecked
        {
            var hash = year * 1009 + week * 9176 + postIndexInWeek * 104729;
            foreach (var c in platform)
                hash = hash * 31 + c;
            return Math.Abs(hash);
        }
    }

    public static Dictionary<string, string> GeneratePromoPosts(
        IEnumerable<string> platforms,
        string appBaseUrl,
        int weekBaseSeed)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var platform in platforms)
        {
            if (string.IsNullOrWhiteSpace(platform)) continue;
            result[platform] = GeneratePromoPost(platform, appBaseUrl, weekBaseSeed + index * 7919);
            index++;
        }
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
        var swapLinkOrder = s % 2 == 0;

        return GeneratePost(platform, hook, startUrl, trialUrl, tags, useTrialCta, swapLinkOrder);
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

    public static Dictionary<string, string> GenerateUpdatePosts(
        ProductUpdate update,
        string appBaseUrl,
        IEnumerable<string> platforms)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var platform in platforms)
        {
            if (string.IsNullOrWhiteSpace(platform)) continue;
            result[platform] = GenerateUpdatePost(platform, update, appBaseUrl);
        }
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

    static string GeneratePost(
        string platform,
        string hook,
        string startUrl,
        string trialUrl,
        string tags,
        bool useTrialCta,
        bool swapLinkOrder = false)
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

        if (PostLimits.IsLinkedIn(platform))
        {
            var bodyCta = BuildBodyCta(startUrl, trialUrl, useTrialCta, swapLinkOrder);
            return PostLimits.Enforce($"{hook}\n\n{bodyCta}\n\n— BookPromoter AI", platform);
        }

        var defaultCta = BuildBodyCta(startUrl, trialUrl, useTrialCta, swapLinkOrder);
        return PostLimits.Enforce($"{hook}\n\n{defaultCta}\n\n— BookPromoter AI", platform);
    }

    static string BuildBodyCta(string startUrl, string trialUrl, bool useTrialCta, bool swapLinkOrder)
    {
        var trialLine = $"Free access code: {trialUrl}";
        var startLine = $"Create your account: {startUrl}";
        if (useTrialCta && swapLinkOrder) return $"{startLine}\n{trialLine}";
        if (useTrialCta) return $"{trialLine}\n{startLine}";
        if (swapLinkOrder) return $"{trialLine}\n{startLine}";
        return $"{startLine}\n{trialLine}";
    }

    public static List<string> ParseLines(string? text) =>
        (text ?? "")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
}
