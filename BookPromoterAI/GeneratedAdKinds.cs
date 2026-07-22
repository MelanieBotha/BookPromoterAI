namespace BookPromoterAI;

static class GeneratedAdKinds
{
    public const string Author = "Author";
    public const string Brand = "Brand";

    public static bool IsBrand(string? kind) =>
        string.Equals(kind, Brand, StringComparison.OrdinalIgnoreCase);

    public static bool IsAuthor(string? kind) =>
        string.IsNullOrWhiteSpace(kind) ||
        string.Equals(kind, Author, StringComparison.OrdinalIgnoreCase);
}
