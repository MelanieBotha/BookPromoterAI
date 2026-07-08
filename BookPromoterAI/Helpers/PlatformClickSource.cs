namespace BookPromoterAI;

static class PlatformClickSource
{
    public static string? SlugForPlatform(string? platform)
    {
        if (string.IsNullOrWhiteSpace(platform)) return null;
        var name = platform.Trim();
        if (name.Equals("General", StringComparison.OrdinalIgnoreCase)) return null;

        if (PostLimits.IsX(name)) return "x";
        if (PostLimits.IsBluesky(name)) return "bluesky";

        return name.ToLowerInvariant() switch
        {
            "facebook" => "facebook",
            "reddit" => "reddit",
            "linkedin" => "linkedin",
            "inkitt" => "inkitt",
            "wordpress" => "wordpress",
            "medium" => "medium",
            "flickr" => "flickr",
            "pinterest" => "pinterest",
            "tiktok" => "tiktok",
            "tumblr" => "tumblr",
            "mastodon" => "mastodon",
            "discord" => "discord",
            "telegram" => "telegram",
            _ => new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant()
        };
    }

    public static string Normalize(string? slugOrName)
    {
        if (string.IsNullOrWhiteSpace(slugOrName)) return "Direct";

        var value = slugOrName.Trim().ToLowerInvariant();
        return value switch
        {
            "facebook" or "fb" => "Facebook",
            "x" or "twitter" => "X",
            "reddit" => "Reddit",
            "linkedin" => "LinkedIn",
            "bluesky" => "Bluesky",
            "inkitt" => "Inkitt",
            "pinterest" => "Pinterest",
            "tiktok" => "TikTok",
            "tumblr" => "Tumblr",
            "wordpress" => "WordPress",
            "medium" => "Medium",
            "flickr" => "Flickr",
            "mastodon" => "Mastodon",
            "discord" => "Discord",
            "telegram" => "Telegram",
            "direct" => "Direct",
            _ => char.ToUpper(slugOrName.Trim()[0]) + slugOrName.Trim()[1..].ToLowerInvariant()
        };
    }
}
