namespace BookPromoterAI;

static class SocialCrawler
{
    static readonly string[] KnownAgents =
    [
        "facebookexternalhit",
        "Facebot",
        "Twitterbot",
        "Twitterbot/1.0",
        "LinkedInBot",
        "Slackbot",
        "WhatsApp",
        "TelegramBot",
        "Discordbot",
        "Pinterest"
    ];

    public static bool IsCrawler(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return false;
        foreach (var agent in KnownAgents)
        {
            if (userAgent.Contains(agent, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
