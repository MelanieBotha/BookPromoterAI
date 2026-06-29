namespace BookPromoterAI;

static class AuthorDisplayName
{
    public const string Fallback = "The Author";

    public static string FromBooks(IEnumerable<Book> books) =>
        books.Select(b => b.AuthorName?.Trim())
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
        ?? Fallback;

    public static string FromDbBooks(IEnumerable<DbBook> books) =>
        books.Select(b => b.AuthorName?.Trim())
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
        ?? Fallback;
}
