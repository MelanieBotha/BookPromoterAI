namespace BookPromoterAI;

/// <summary>Splits book excerpts into timed sentences/word chunks for narrated video subtitles.</summary>
static class ReadAloudScript
{
    public const int MaxWords = TikTokVideoLimits.MaxExcerptWords;
    public const int WordsPerCaption = 5;

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

    /// <summary>TikTok-style short captions: ~5 words per beat, timed across the speech duration.</summary>
    public static ReadAloudPlan BuildWordChunks(string excerpt, double totalDurationMs, int wordsPerCaption = WordsPerCaption)
    {
        var text = excerpt.Trim();
        var words = text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
            return Build(text, totalDurationMs);

        var chunks = new List<string>();
        for (var i = 0; i < words.Length; i += Math.Max(1, wordsPerCaption))
            chunks.Add(string.Join(' ', words.Skip(i).Take(wordsPerCaption)));

        var totalChars = Math.Max(1, chunks.Sum(c => c.Length));
        var cursor = 0.0;
        var beats = new List<ReadAloudBeat>();
        foreach (var chunk in chunks)
        {
            var share = chunk.Length / (double)totalChars;
            var duration = Math.Max(400, totalDurationMs * share);
            beats.Add(new ReadAloudBeat
            {
                Text = chunk,
                StartMs = cursor,
                EndMs = cursor + duration
            });
            cursor += duration;
        }

        if (beats.Count > 0)
        {
            if (cursor < totalDurationMs)
                beats[^1].EndMs = totalDurationMs;
            else if (beats[^1].EndMs > totalDurationMs)
                beats[^1].EndMs = totalDurationMs;
        }

        return new ReadAloudPlan
        {
            Excerpt = text,
            DurationMs = totalDurationMs,
            Beats = beats
        };
    }

    public static ReadAloudPlan BuildFromWordTimings(IReadOnlyList<SpeechWordTiming> words, int wordsPerCaption = WordsPerCaption)
    {
        if (words.Count == 0)
            return new ReadAloudPlan { Excerpt = "", DurationMs = 0, Beats = [] };

        var beats = new List<ReadAloudBeat>();
        for (var i = 0; i < words.Count; i += Math.Max(1, wordsPerCaption))
        {
            var chunk = words.Skip(i).Take(wordsPerCaption).ToList();
            beats.Add(new ReadAloudBeat
            {
                Text = string.Join(' ', chunk.Select(w => w.Word)),
                StartMs = chunk[0].StartMs,
                EndMs = Math.Max(chunk[0].StartMs + 400, chunk[^1].EndMs)
            });
        }

        return new ReadAloudPlan
        {
            Excerpt = string.Join(' ', words.Select(w => w.Word)),
            DurationMs = beats.Count > 0 ? beats[^1].EndMs : 0,
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
