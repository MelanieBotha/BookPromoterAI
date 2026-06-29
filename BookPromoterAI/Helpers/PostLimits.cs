using System.Globalization;

namespace BookPromoterAI;

static class PostLimits
{
    /// <summary>Bluesky: app.bsky.feed.post maxGraphemes = 300 (AT Protocol lexicon).</summary>
    public const int BlueskyMaxGraphemes = 300;

    /// <summary>X (Twitter) standard post limit.</summary>
    public const int XMaxGraphemes = 280;

    static readonly Dictionary<string, int> Limits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bluesky"] = BlueskyMaxGraphemes,
        ["X"] = XMaxGraphemes,
        ["X (Twitter)"] = XMaxGraphemes,
        ["Twitter"] = XMaxGraphemes,
    };

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
}
