using System.Globalization;

namespace BookPromoterAI;

static class PostLimits
{
    /// <summary>Bluesky: app.bsky.feed.post maxGraphemes = 300 (AT Protocol lexicon).</summary>
    public const int BlueskyMaxGraphemes = 300;

    /// <summary>X (Twitter) standard post limit.</summary>
    public const int XMaxGraphemes = 280;

    /// <summary>LinkedIn feed post limit.</summary>
    public const int LinkedInMaxGraphemes = 3000;

    /// <summary>Reddit self-post body limit.</summary>
    public const int RedditMaxGraphemes = 40000;

    static readonly Dictionary<string, int> Limits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bluesky"] = BlueskyMaxGraphemes,
        ["X"] = XMaxGraphemes,
        ["X (Twitter)"] = XMaxGraphemes,
        ["Twitter"] = XMaxGraphemes,
        ["LinkedIn"] = LinkedInMaxGraphemes,
        ["Reddit"] = RedditMaxGraphemes,
        ["Mastodon"] = MastodonMaxGraphemes,
    };

    /// <summary>Mastodon default post limit.</summary>
    public const int MastodonMaxGraphemes = 500;

    public static int? GetMaxGraphemes(string platform)
    {
        if (string.IsNullOrWhiteSpace(platform)) return null;
        if (Limits.TryGetValue(platform.Trim(), out var exact)) return exact;
        foreach (var (key, limit) in Limits)
        {
            if (platform.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                return limit;
        }
        return null;
    }

    public static int GraphemeLength(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return new StringInfo(text).LengthInTextElements;
    }

    public static string Enforce(string text, string platform)
    {
        var max = GetMaxGraphemes(platform);
        if (max is null || GraphemeLength(text) <= max) return text;

        if (IsX(platform) || IsBluesky(platform))
            return EnforcePreservingTrailingUrl(text, max.Value);

        return TruncateToGraphemes(text, max.Value);
    }

    /// <summary>Keeps the last-line URL intact so X/Bluesky link previews still work after trimming.</summary>
    static string EnforcePreservingTrailingUrl(string text, int maxGraphemes)
    {
        var trimmed = text.TrimEnd();
        var lastBreak = trimmed.LastIndexOf('\n');
        if (lastBreak < 0) return TruncateToGraphemes(trimmed, maxGraphemes);

        var body = trimmed[..lastBreak].TrimEnd();
        var lastLine = trimmed[(lastBreak + 1)..].Trim();
        if (!lastLine.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !lastLine.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return TruncateToGraphemes(trimmed, maxGraphemes);

        var separator = "\n\n";
        var reserved = GraphemeLength(lastLine) + GraphemeLength(separator);
        var bodyBudget = maxGraphemes - reserved;
        if (bodyBudget < 1)
            return GraphemeLength(lastLine) <= maxGraphemes ? lastLine : TruncateToGraphemes(lastLine, maxGraphemes, "");

        var shortenedBody = GraphemeLength(body) <= bodyBudget
            ? body
            : TruncateToGraphemes(body, bodyBudget, "…");

        return $"{shortenedBody}{separator}{lastLine}";
    }

    public static bool IsWithinLimit(string text, string platform)
    {
        var max = GetMaxGraphemes(platform);
        return max is null || GraphemeLength(text) <= max;
    }

    public static string CharacterCountLabel(string platform, string text)
    {
        var max = GetMaxGraphemes(platform);
        if (max is null) return "";
        var len = GraphemeLength(text);
        var over = len > max;
        var cls = over ? " char-count-over" : "";
        return $""" <span class="char-count{cls}">{len}/{max}</span>""";
    }

    static string TruncateToGraphemes(string text, int maxGraphemes, string suffix = "…")
    {
        if (maxGraphemes <= 0) return "";
        var info = new StringInfo(text);
        if (info.LengthInTextElements <= maxGraphemes) return text;

        var suffixLen = GraphemeLength(suffix);
        var budget = Math.Max(1, maxGraphemes - suffixLen);
        var truncated = info.SubstringByTextElements(0, Math.Min(budget, info.LengthInTextElements)).TrimEnd();

        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > truncated.Length * 0.55)
            truncated = truncated[..lastSpace].TrimEnd();

        return string.IsNullOrEmpty(suffix) ? truncated : truncated + suffix;
    }

    public static bool IsBluesky(string platform) =>
        platform.Equals("Bluesky", StringComparison.OrdinalIgnoreCase);

    public static bool IsX(string platform) =>
        platform.Equals("X", StringComparison.OrdinalIgnoreCase) ||
        platform.Equals("Twitter", StringComparison.OrdinalIgnoreCase) ||
        platform.StartsWith("X (", StringComparison.OrdinalIgnoreCase);

    public static bool IsLinkedIn(string platform) =>
        platform.Equals("LinkedIn", StringComparison.OrdinalIgnoreCase);

    public static bool IsFacebook(string platform) =>
        platform.Equals("Facebook", StringComparison.OrdinalIgnoreCase);

    public static bool IsInstagram(string platform) => false;

    public static bool IsReddit(string platform) =>
        platform.Equals("Reddit", StringComparison.OrdinalIgnoreCase);

    public static bool IsTikTok(string platform) =>
        platform.Equals("TikTok", StringComparison.OrdinalIgnoreCase);

    public static bool IsMastodon(string platform) =>
        platform.Equals("Mastodon", StringComparison.OrdinalIgnoreCase);

    public static bool IsDiscord(string platform) =>
        platform.Equals("Discord", StringComparison.OrdinalIgnoreCase);

    public static bool IsTelegram(string platform) =>
        platform.Equals("Telegram", StringComparison.OrdinalIgnoreCase);

    public static bool IsTumblr(string platform) =>
        platform.Equals("Tumblr", StringComparison.OrdinalIgnoreCase);

    public static bool IsWordPress(string platform) =>
        platform.Equals("WordPress", StringComparison.OrdinalIgnoreCase);

    public static bool IsMedium(string platform) =>
        platform.Equals("Medium", StringComparison.OrdinalIgnoreCase);

    public static bool IsFlickr(string platform) =>
        platform.Equals("Flickr", StringComparison.OrdinalIgnoreCase);

    public static bool RequiresLiveConnection(string platform) =>
        IsBluesky(platform) || IsX(platform) || IsLinkedIn(platform) || IsFacebook(platform) ||
        IsReddit(platform) || IsMastodon(platform) || IsDiscord(platform) || IsTelegram(platform) ||
        IsTumblr(platform) || IsWordPress(platform) || IsMedium(platform) || IsFlickr(platform);

    public static string LiveReconnectHint(string platform)
    {
        if (IsBluesky(platform))
            return "Bluesky is not connected for live posting. In My Account, remove your Bluesky account and reconnect with an app password.";
        if (IsX(platform))
            return "X is not connected for live posting. In My Account, remove your X account and reconnect with Sign in with X.";
        if (IsLinkedIn(platform))
            return "LinkedIn is not connected for live posting. In My Account, remove your LinkedIn account and reconnect with Sign in with LinkedIn.";
        if (IsFacebook(platform))
            return "Facebook is not connected for live posting. In My Account, remove your Facebook account and reconnect with Sign in with Facebook.";
        if (IsReddit(platform))
            return "Reddit is not connected for live posting. In My Account, remove your Reddit account and reconnect with Sign in with Reddit.";
        if (IsMastodon(platform))
            return "Mastodon is not connected for live posting. In My Account, remove your Mastodon account and reconnect.";
        if (IsDiscord(platform))
            return "Discord is not connected for live posting. In My Account, reconnect with your channel webhook URL.";
        if (IsTelegram(platform))
            return "Telegram is not connected for live posting. In My Account, reconnect with your bot token and chat ID.";
        if (IsTumblr(platform))
            return "Tumblr is not connected for live posting. In My Account, remove your Tumblr account and reconnect.";
        if (IsWordPress(platform))
            return "WordPress is not connected for live posting. In My Account, remove your WordPress account and reconnect with an application password.";
        if (IsMedium(platform))
            return "Medium is not connected for live posting. In My Account, remove your Medium account and reconnect with an integration token.";
        if (IsFlickr(platform))
            return "Flickr is not connected for live posting. In My Account, remove your Flickr account and reconnect with Sign in with Flickr.";
        return $"Connect {platform} for live posting in My Account.";
    }

    public static string LivePostNowHint(string platform)
    {
        if (IsBluesky(platform))
            return "Reconnect Bluesky with an app password in My Account to use Post now.";
        if (IsX(platform))
            return "Reconnect X with Sign in with X in My Account to use Post now.";
        if (IsLinkedIn(platform))
            return "Reconnect LinkedIn with Sign in with LinkedIn in My Account to use Post now.";
        if (IsFacebook(platform))
            return "Reconnect Facebook with Sign in with Facebook in My Account to use Post now.";
        if (IsReddit(platform))
            return "Reconnect Reddit with Sign in with Reddit in My Account to use Post now.";
        if (IsMastodon(platform))
            return "Reconnect Mastodon in My Account to use Post now.";
        if (IsDiscord(platform))
            return "Reconnect Discord with your webhook URL in My Account to use Post now.";
        if (IsTelegram(platform))
            return "Reconnect Telegram with your bot token in My Account to use Post now.";
        if (IsTumblr(platform))
            return "Reconnect Tumblr with Sign in with Tumblr in My Account to use Post now.";
        if (IsWordPress(platform))
            return "Reconnect WordPress with your application password in My Account to use Post now.";
        if (IsMedium(platform))
            return "Reconnect Medium with your integration token in My Account to use Post now.";
        if (IsFlickr(platform))
            return "Reconnect Flickr with Sign in with Flickr in My Account to use Post now.";
        return $"Reconnect {platform} in My Account to use Post now.";
    }

    public static bool PlatformsMatch(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return true;
        if (IsX(a) && IsX(b)) return true;
        if (IsLinkedIn(a) && IsLinkedIn(b)) return true;
        if (IsFacebook(a) && IsFacebook(b)) return true;
        if (IsMastodon(a) && IsMastodon(b)) return true;
        if (IsDiscord(a) && IsDiscord(b)) return true;
        if (IsTelegram(a) && IsTelegram(b)) return true;
        if (IsTumblr(a) && IsTumblr(b)) return true;
        if (IsWordPress(a) && IsWordPress(b)) return true;
        if (IsMedium(a) && IsMedium(b)) return true;
        if (IsFlickr(a) && IsFlickr(b)) return true;
        return IsReddit(a) && IsReddit(b);
    }
}
