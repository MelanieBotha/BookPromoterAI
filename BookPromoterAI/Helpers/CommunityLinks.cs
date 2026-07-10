namespace BookPromoterAI;

/// <summary>Reader-community URLs and platforms that only grow when promoted outside the post itself.</summary>
static class CommunityLinks
{
    public static readonly string[] ExternalAudiencePlatforms =
    [
        "Discord",
        "Telegram",
        "Mastodon",
        "WordPress",
        "Medium",
        "Flickr",
        "TikTok",
        "Mailing list"
    ];

    public static bool NeedsExternalAudience(string? platform)
    {
        if (string.IsNullOrWhiteSpace(platform)) return false;
        if (platform.Contains("mailing", StringComparison.OrdinalIgnoreCase)) return true;
        return PostLimits.IsDiscord(platform)
            || PostLimits.IsTelegram(platform)
            || PostLimits.IsMastodon(platform)
            || PostLimits.IsWordPress(platform)
            || PostLimits.IsMedium(platform)
            || PostLimits.IsFlickr(platform)
            || PostLimits.IsTikTok(platform);
    }

    public static bool IsBroadcastChannel(string? platform) =>
        !string.IsNullOrWhiteSpace(platform) &&
        (PostLimits.IsDiscord(platform) || PostLimits.IsTelegram(platform));

    public static string AppendPromotion(string body, string platform, CommunityProfile? community)
    {
        if (community is null || !community.HasAny) return body;

        var lines = BuildPromotionLines(platform, community);
        if (lines.Count == 0) return body;

        var suffix = "\n\n—\n" + string.Join("\n", lines);
        var combined = body.TrimEnd() + suffix;
        return PostLimits.Enforce(combined, platform);
    }

    public static IReadOnlyList<string> BuildPromotionLines(string platform, CommunityProfile community)
    {
        var lines = new List<string>();
        var onDiscord = PostLimits.IsDiscord(platform);
        var onTelegram = PostLimits.IsTelegram(platform);

        if (!onDiscord && community.HasDiscord)
            lines.Add($"Join readers on Discord: {community.DiscordUrl}");

        if (!onTelegram && community.HasTelegram)
            lines.Add($"Telegram updates: {community.TelegramUrl}");

        if (community.HasMailingList)
            lines.Add($"New releases by email: {community.MailingListUrl}");

        if (!PostLimits.IsWordPress(platform) && community.HasBlog)
            lines.Add($"Read more on the blog: {community.BlogUrl}");

        if (!PostLimits.IsTikTok(platform) && community.HasTikTok)
            lines.Add($"BookTok / videos: {community.TikTokUrl}");

        if (!PostLimits.IsMastodon(platform) && community.HasMastodon)
            lines.Add($"Mastodon: {community.MastodonUrl}");

        return lines;
    }

    public static string RenderFooterLinks(CommunityProfile profile)
    {
        if (!profile.HasAny) return "";

        var links = new List<string>();
        if (profile.HasDiscord)
            links.Add($"""<a href="{H.Encode(profile.DiscordUrl!)}" target="_blank" rel="noopener">Discord</a>""");
        if (profile.HasTelegram)
            links.Add($"""<a href="{H.Encode(profile.TelegramUrl!)}" target="_blank" rel="noopener">Telegram</a>""");
        if (profile.HasMastodon)
            links.Add($"""<a href="{H.Encode(profile.MastodonUrl!)}" target="_blank" rel="noopener">Mastodon</a>""");
        if (profile.HasMailingList)
            links.Add($"""<a href="{H.Encode(profile.MailingListUrl!)}" target="_blank" rel="noopener">Reader emails</a>""");

        return links.Count == 0 ? "" : string.Join(" · ", links);
    }

    public static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            trimmed = "https://" + trimmed.TrimStart('/');
        return UrlSafety.IsSafeRedirect(trimmed) ? trimmed : null;
    }
}

record CommunityProfile(
    string? DiscordUrl,
    string? TelegramUrl,
    string? MailingListUrl,
    string? BlogUrl,
    string? TikTokUrl,
    string? MastodonUrl)
{
    public bool HasDiscord => !string.IsNullOrWhiteSpace(DiscordUrl);
    public bool HasTelegram => !string.IsNullOrWhiteSpace(TelegramUrl);
    public bool HasMailingList => !string.IsNullOrWhiteSpace(MailingListUrl);
    public bool HasBlog => !string.IsNullOrWhiteSpace(BlogUrl);
    public bool HasTikTok => !string.IsNullOrWhiteSpace(TikTokUrl);
    public bool HasMastodon => !string.IsNullOrWhiteSpace(MastodonUrl);

    public bool HasAny =>
        HasDiscord || HasTelegram || HasMailingList || HasBlog || HasTikTok || HasMastodon;
}
