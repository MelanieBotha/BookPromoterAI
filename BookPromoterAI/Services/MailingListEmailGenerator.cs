namespace BookPromoterAI;

class MailingListEmailGenerator
{
    private static readonly string[] SubjectTemplates =
    [
        "New from {author}: \"{title}\"",
        "A book update for you — {title}",
        "Don't miss \"{title}\" by {author}",
        "Reader news: {title} is waiting for you"
    ];

    public (string Subject, string Body) Generate(Book book, string trackingUrl, int variantSeed = 0)
    {
        var seed = Math.Abs(variantSeed);
        var subject = SubjectTemplates[seed % SubjectTemplates.Length]
            .Replace("{author}", book.AuthorName, StringComparison.Ordinal)
            .Replace("{title}", book.Title, StringComparison.Ordinal);

        var hook = PickHook(book, seed);
        var body = $"""
            Hi there,

            {hook}

            {book.Title} by {book.AuthorName}
            {TrimDescription(book.Description)}

            Read more or get your copy here:
            {trackingUrl}

            Happy reading!
            """;

        return (subject, body.Trim());
    }

    static string PickHook(Book book, int seed)
    {
        if (!string.IsNullOrWhiteSpace(book.Description))
        {
            var first = book.Description.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
                return first.EndsWith('.') || first.EndsWith('!') || first.EndsWith('?') ? first : first + ".";
        }

        return seed % 2 == 0
            ? $"I wanted to share my latest {book.GenreOrDefault().ToLowerInvariant()} book with you."
            : $"If you enjoy {book.GenreOrDefault().ToLowerInvariant()} stories, I think you'll love this one.";
    }

    static string TrimDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return "";
        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 60) return description;
        return string.Join(' ', words.Take(60)) + "...";
    }
}
