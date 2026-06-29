namespace BookPromoterAI;

static class ClickAnalytics
{
    public static string CurrentMonthKey() => DateTime.UtcNow.ToString("yyyy-MM");

    public static int ClicksForMonth(Book book, string monthKey) =>
        book.ClickHistory.TryGetValue(monthKey, out var clicks) ? clicks : 0;

    public static int ClicksThisMonth(Book book) => ClicksForMonth(book, CurrentMonthKey());

    public static int TotalClicksForMonth(IEnumerable<Book> books, string monthKey) =>
        books.Sum(b => ClicksForMonth(b, monthKey));

    public static int TotalClicksThisMonth(IEnumerable<Book> books) =>
        TotalClicksForMonth(books, CurrentMonthKey());

    public static int TotalClicksAllTime(IEnumerable<Book> books) =>
        books.Sum(b => b.ClickHistory.Values.Sum());

    public static Book? TopBookThisMonth(IEnumerable<Book> books) =>
        books.Where(b => ClicksThisMonth(b) > 0)
            .OrderByDescending(ClicksThisMonth)
            .ThenBy(b => b.Title, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    public static Book? LowestBookThisMonth(IEnumerable<Book> books) =>
        books.Where(b => ClicksThisMonth(b) > 0)
            .OrderBy(ClicksThisMonth)
            .ThenBy(b => b.Title, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
}
