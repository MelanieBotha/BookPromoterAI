namespace BookPromoterAI;

/// <summary>Parses TikTok captions into timed on-screen beats (Grok / BookTok style).</summary>
static class TikTokCaptionScript
{
    public static VideoScript Build(string caption, string title, string author)
    {
        var lines = caption.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hook = lines.FirstOrDefault(l => !l.StartsWith('#') && !IsUrl(l)) ?? caption.Trim();
        var hashtags = string.Join("  ", lines.Where(l => l.Contains('#', StringComparison.Ordinal)));
        var link = lines.FirstOrDefault(IsUrl) ?? "";
        var chunks = SplitIntoChunks(hook, 3, 6);
        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(hook))
            chunks.Add(hook);

        return new VideoScript
        {
            Hook = hook,
            Title = title,
            Author = author,
            Chunks = chunks,
            Hashtags = hashtags,
            Link = link,
            Cta = string.IsNullOrWhiteSpace(link) ? "Link in bio 📚" : "Tap link in caption"
        };
    }

    static bool IsUrl(string line) =>
        line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    static List<string> SplitIntoChunks(string text, int minWords, int maxWords)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var i = 0;
        while (i < words.Length)
        {
            var take = Math.Min(maxWords, words.Length - i);
            if (take < minWords && chunks.Count > 0)
            {
                chunks[^1] += " " + string.Join(' ', words.Skip(i));
                break;
            }
            if (take < minWords && chunks.Count == 0)
                take = words.Length - i;
            chunks.Add(string.Join(' ', words.Skip(i).Take(take)));
            i += take;
        }
        return chunks;
    }
}

class VideoScript
{
    public string Hook { get; init; } = "";
    public string Title { get; init; } = "";
    public string Author { get; init; } = "";
    public List<string> Chunks { get; init; } = [];
    public string Hashtags { get; init; } = "";
    public string Link { get; init; } = "";
    public string Cta { get; init; } = "";
}
