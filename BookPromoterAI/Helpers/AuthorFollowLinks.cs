namespace BookPromoterAI;

/// <summary>Public profile / invite links shown on the reader-facing book page.</summary>
static class AuthorFollowLinks
{
    public record Link(string Label, string Url);

    public static IReadOnlyList<Link> Build(IEnumerable<SocialAccount> accounts, CommunityProfile? community)
    {
        var links = new List<Link>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string label, string? url)
        {
            var normalized = CommunityLinks.NormalizeUrl(url);
            if (normalized is null || !seen.Add(normalized)) return;
            links.Add(new Link(label, normalized));
        }

        foreach (var account in accounts.Where(a => a.IsConnected))
        {
            var url = ProfileUrl(account);
            if (url is not null)
                Add(PlatformLabel(account.Platform), url);
        }

        if (community is not null)
        {
            if (community.HasDiscord) Add("Discord", community.DiscordUrl);
            if (community.HasTelegram) Add("Telegram", community.TelegramUrl);
            if (community.HasMastodon) Add("Mastodon", community.MastodonUrl);
            if (community.HasBlog) Add("Blog", community.BlogUrl);
            if (community.HasTikTok) Add("TikTok", community.TikTokUrl);
            if (community.HasMailingList) Add("Email list", community.MailingListUrl);
        }

        return links;
    }

    public static string? ProfileUrl(SocialAccount account)
    {
        if (PostLimits.IsDiscord(account.Platform) || PostLimits.IsTelegram(account.Platform))
            return null;

        var handle = (account.Handle ?? "").Trim().TrimStart('@');

        if (PostLimits.IsX(account.Platform))
            return string.IsNullOrWhiteSpace(handle) ? null : $"https://x.com/{Uri.EscapeDataString(handle)}";

        if (PostLimits.IsBluesky(account.Platform))
            return string.IsNullOrWhiteSpace(handle) ? null : $"https://bsky.app/profile/{handle}";

        if (PostLimits.IsFacebook(account.Platform))
        {
            if (!string.IsNullOrWhiteSpace(handle) && !handle.All(char.IsDigit))
                return $"https://www.facebook.com/{Uri.EscapeDataString(handle)}";
            if (!string.IsNullOrWhiteSpace(account.ExternalAccountId))
                return $"https://www.facebook.com/{Uri.EscapeDataString(account.ExternalAccountId)}";
            return null;
        }

        if (PostLimits.IsLinkedIn(account.Platform))
        {
            if (string.IsNullOrWhiteSpace(handle) || handle.Contains(':')) return null;
            return $"https://www.linkedin.com/in/{Uri.EscapeDataString(handle)}";
        }

        if (PostLimits.IsTumblr(account.Platform))
            return string.IsNullOrWhiteSpace(handle) ? null : $"https://www.tumblr.com/{Uri.EscapeDataString(handle)}";

        if (PostLimits.IsTikTok(account.Platform))
            return string.IsNullOrWhiteSpace(handle) ? null : $"https://www.tiktok.com/@{Uri.EscapeDataString(handle)}";

        if (PostLimits.IsMastodon(account.Platform))
        {
            var acct = (account.Handle ?? "").Trim().TrimStart('@');
            var parts = acct.Split('@', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
                return $"https://{parts[1]}/@{Uri.EscapeDataString(parts[0])}";
            return null;
        }

        if (PostLimits.IsReddit(account.Platform))
            return string.IsNullOrWhiteSpace(handle) ? null : $"https://www.reddit.com/r/{Uri.EscapeDataString(handle)}";

        if (PostLimits.IsFlickr(account.Platform))
            return string.IsNullOrWhiteSpace(handle) ? null : $"https://www.flickr.com/people/{Uri.EscapeDataString(handle)}/";

        if (PostLimits.IsMedium(account.Platform))
            return string.IsNullOrWhiteSpace(handle) ? null : $"https://medium.com/@{Uri.EscapeDataString(handle)}";

        if (PostLimits.IsWordPress(account.Platform))
            return CommunityLinks.NormalizeUrl(account.ExternalAccountId) ?? CommunityLinks.NormalizeUrl(account.Handle);

        return null;
    }

    static string PlatformLabel(string platform)
    {
        if (PostLimits.IsX(platform)) return "X";
        if (PostLimits.IsBluesky(platform)) return "Bluesky";
        if (PostLimits.IsFacebook(platform)) return "Facebook";
        if (PostLimits.IsLinkedIn(platform)) return "LinkedIn";
        if (PostLimits.IsTumblr(platform)) return "Tumblr";
        if (PostLimits.IsTikTok(platform)) return "TikTok";
        if (PostLimits.IsMastodon(platform)) return "Mastodon";
        if (PostLimits.IsReddit(platform)) return "Reddit";
        if (PostLimits.IsFlickr(platform)) return "Flickr";
        if (PostLimits.IsMedium(platform)) return "Medium";
        if (PostLimits.IsWordPress(platform)) return "Blog";
        return string.IsNullOrWhiteSpace(platform) ? "Social" : platform.Trim();
    }
}
