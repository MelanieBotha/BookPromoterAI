namespace BookPromoterAI;

/// <summary>Splits book excerpts into timed sentences for narrated video subtitles.</summary>
static class ReadAloudScript
{
    public const int MaxWords = 150;

    public static ReadAloudPlan Build(string excerpt, double totalDurationMs)
    {
        var text = excerpt.Trim();
        var sentences = SplitSentences(text);
        if (sentences.Count == 0)
            sentences = [text];

        var totalChars = Math.Max(1, sentences.Sum(s => s.Length));
        var cursor = 0.0;
        var beats = new List<ReadAloudBeat>();
        foreach (var sentence in sentences)
        {
            var share = sentence.Length / (double)totalChars;
            var duration = totalDurationMs * share;
            beats.Add(new ReadAloudBeat
            {
                Text = sentence,
                StartMs = cursor,
                EndMs = cursor + duration
            });
            cursor += duration;
        }

        if (beats.Count > 0)
            beats[^1].EndMs = totalDurationMs;

        return new ReadAloudPlan
        {
            Excerpt = text,
            DurationMs = totalDurationMs,
            Beats = beats
        };
    }

    public static string LimitWords(string text, int maxWords = MaxWords) =>
        H.LimitWords(text, maxWords);

    static List<string> SplitSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var parts = text.Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Where(p => p.Length > 0).Select(p => p.EndsWith('.') || p.EndsWith('!') || p.EndsWith('?') ? p : p + ".").ToList();
    }
}

class ReadAloudPlan
{
    public string Excerpt { get; init; } = "";
    public double DurationMs { get; init; }
    public List<ReadAloudBeat> Beats { get; init; } = [];
}

class ReadAloudBeat
{
    public string Text { get; init; } = "";
    public double StartMs { get; set; }
    public double EndMs { get; set; }
}
