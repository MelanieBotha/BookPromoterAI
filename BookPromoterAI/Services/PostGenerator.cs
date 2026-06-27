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
    private static readonly string[] ClosingCallToActions = ["Read now", "Grab your copy", "Start reading today", "Dive in here"];

    public string Generate(Book book, string platform, string purchaseUrl, int variantSeed = 0, string appBaseUrl = "")
    {
        var descriptionHook = ExtractDescriptionHook(book.Description, variantSeed);
        var genreHook = PickHook(book.Genre, variantSeed);
        var cta = ClosingCallToActions[Math.Abs(variantSeed) % ClosingCallToActions.Length];
        var useDescriptionFirst = descriptionHook is not null && variantSeed % 2 == 0;
        var hook = useDescriptionFirst
            ? $"{descriptionHook} {genreHook} \"{book.Title}\""
            : descriptionHook is not null
                ? $"{genreHook} \"{book.Title}\" - {descriptionHook}"
                : $"{genreHook} Try \"{book.Title}\".";

        var link = purchaseUrl.Trim();
        var hasLink = !string.IsNullOrWhiteSpace(link);
        var ctaLine = hasLink ? $"{cta}: {link}" : $"{cta} (add a store link in Books)";

        var body = platform switch
        {
            "X" => hasLink
                ? $"{hook} {link} #Books #{CleanTag(book.Genre)}"
                : $"{hook} #Books #{CleanTag(book.Genre)}",
            "Instagram" => hasLink
                ? $"{hook}\n\n{ctaLine}\n\n#Bookstagram #{CleanTag(book.GenreOrDefault())}"
                : $"{hook}\n\n{ctaLine}\n\n#Bookstagram #{CleanTag(book.GenreOrDefault())}",
            _ => hasLink
                ? $"{hook}\n\n{book.Description}\n\n{ctaLine}"
                : $"{hook}\n\n{book.Description}\n\n{ctaLine}"
        };

        if (!string.IsNullOrWhiteSpace(appBaseUrl))
            body += PostBranding.Footer(platform, appBaseUrl);

        return body;
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
