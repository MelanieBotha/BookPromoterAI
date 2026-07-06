namespace BookPromoterAI;

class PostGenerator
{
    private static readonly Dictionary<string, string[]> GenreHooks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["romance"] = ["She never expected a love story like this.", "Some connections are impossible to ignore.", "Their story will steal your heart."],
        ["horror"] = ["The fear begins on page one.", "Some things are better left undisturbed.", "By the final page, you won't want the lights off."],
        ["thriller"] = ["Some secrets refuse to stay buried.", "One wrong move changes everything.", "Trust no one. Especially not them."],
        ["fantasy"] = ["A new world is waiting.", "Magic always comes with a price.", "Step into a world unlike any other."]
    };

    private static readonly string[] GenericHooks = ["Looking for your next read?", "Your next favorite book is one click away.", "Readers can't stop talking about this one."];

    public string Generate(Book book, string platform, string purchaseUrl, int variantSeed = 0, string appBaseUrl = "")
    {
        var descriptionHook = ExtractDescriptionHook(book.Description, variantSeed);
        var genreHook = PickHook(book.Genre, variantSeed);
        var useDescriptionFirst = descriptionHook is not null && variantSeed % 2 == 0;
        var hook = PostLimits.IsX(platform)
            ? BuildShortHook(book, variantSeed)
            : BuildHook(book, variantSeed, descriptionHook, genreHook, useDescriptionFirst);

        var link = purchaseUrl.Trim();
        var hasLink = !string.IsNullOrWhiteSpace(link);
        if (hasLink && !link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            link = "https://" + link.TrimStart('/');
        var linkLine = hasLink ? link : "(add a store link in Books)";

        var body = platform switch
        {
            _ when PostLimits.IsX(platform) => hasLink
                ? $"{hook}\n\n#Books #{CleanTag(book.Genre)}\n\n{link}"
                : $"{hook}\n\n#Books #{CleanTag(book.Genre)}",
            _ when PostLimits.IsBluesky(platform) => hasLink
                ? $"{hook}\n\n#Books #{CleanTag(book.GenreOrDefault())}\n\n{link}"
                : $"{hook}\n\n#Books #{CleanTag(book.GenreOrDefault())}",
            "Reddit" => hasLink
                ? $"{hook}\n\n{book.Description}\n\n{link}"
                : $"{hook}\n\n{book.Description}\n\n{linkLine}",
            _ => hasLink
                ? $"{hook}\n\n{book.Description}\n\n{link}"
                : $"{hook}\n\n{book.Description}\n\n{linkLine}"
        };

        return PostLimits.Enforce(body, platform);
    }

    /// <summary>Short hook + BookTok hashtags for vertical video captions.</summary>
    public string GenerateTikTokCaption(Book book, string purchaseUrl, int variantSeed = 0)
    {
        var hook = BuildShortHook(book, variantSeed);
        var tags = $"#BookTok #{CleanTag(book.GenreOrDefault())} #Books";
        var link = purchaseUrl.Trim();
        if (!string.IsNullOrWhiteSpace(link))
        {
            if (!link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                link = "https://" + link.TrimStart('/');
            return $"{hook}\n\n{tags}\n\n{link}";
        }
        return $"{hook}\n\n{tags}";
    }

    static string BuildHook(Book book, int variantSeed, string? descriptionHook, string genreHook, bool useDescriptionFirst) =>
        useDescriptionFirst
            ? $"{descriptionHook} {genreHook} \"{book.Title}\""
            : descriptionHook is not null
                ? $"{genreHook} \"{book.Title}\" - {descriptionHook}"
                : $"{genreHook} Try \"{book.Title}\".";

    static string BuildShortHook(Book book, int variantSeed)
    {
        var genreHook = PickHook(book.Genre, variantSeed);
        var title = book.Title.Length > 50 ? book.Title[..47].TrimEnd() + "…" : book.Title;
        return $"{genreHook} \"{title}\"";
    }

    private static string? ExtractDescriptionHook(string description, int variantSeed)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var sentences = description.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(s => s.Length > 0).ToList();
        if (sentences.Count == 0) return null;
        var sentence = sentences[Math.Abs(variantSeed) % sentences.Count];
        return sentence.EndsWith('.') || sentence.EndsWith('!') || sentence.EndsWith('?') ? sentence : sentence + ".";
    }

    private static string PickHook(string genre, int variantSeed)
    {
        if (GenreHooks.TryGetValue(genre, out var hooks)) return hooks[Math.Abs(variantSeed) % hooks.Length];
        return GenericHooks[Math.Abs(variantSeed) % GenericHooks.Length];
    }

    private static string CleanTag(string value) => new string(value.Where(char.IsLetterOrDigit).ToArray());
}
