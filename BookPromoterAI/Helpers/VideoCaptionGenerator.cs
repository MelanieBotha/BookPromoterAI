namespace BookPromoterAI;

/// <summary>Builds ~60-second narrations from book metadata for weekly auto-videos.</summary>
static class VideoCaptionGenerator
{
    public static string BuildSixtySecondNarration(Book book, PostGenerator generator, int variantSeed = 0)
    {
        var parts = new List<string>();
        var hook = generator.GenerateTikTokCaption(book, book.Links.FirstOrDefault()?.Url ?? "", variantSeed)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(l => !l.StartsWith('#') && !l.StartsWith("http", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(hook))
            parts.Add(hook.Replace('\n', ' ').Trim());

        foreach (var sentence in SplitSentences(book.Description))
            parts.Add(sentence);

        if (parts.Count <= 1 && !string.IsNullOrWhiteSpace(book.ReadAloudExcerpt))
            parts.Add(book.ReadAloudExcerpt.Trim());

        parts.Add($"If you love {book.GenreOrDefault().ToLowerInvariant()} books, check out {book.Title} by {book.AuthorName}.");

        var combined = string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return ReadAloudScript.LimitWords(combined, TikTokVideoLimits.MaxExcerptWords);
    }

    static IEnumerable<string> SplitSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        foreach (var part in text.Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Length < 4) continue;
            yield return part.EndsWith('.') || part.EndsWith('!') || part.EndsWith('?') ? part : part + ".";
        }
    }
}
