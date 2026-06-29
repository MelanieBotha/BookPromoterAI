namespace BookPromoterAI;

static class AdWeek
{
    public static (int Number, int Year, string Label) For(DateTime utc)
    {
        var number = System.Globalization.ISOWeek.GetWeekOfYear(utc);
        var year = System.Globalization.ISOWeek.GetYear(utc);
        var start = System.Globalization.ISOWeek.ToDateTime(year, number, DayOfWeek.Monday);
        var end = start.AddDays(6);
        var label = end.Year == start.Year
            ? $"Week {number} — {start:MMM d} to {end:MMM d, yyyy}"
            : $"Week {number} — {start:MMM d, yyyy} to {end:MMM d, yyyy}";
        return (number, year, label);
    }

    public static bool IsCurrent(DateTime utc, int weekNumber, int weekYear) =>
        weekNumber == System.Globalization.ISOWeek.GetWeekOfYear(utc) &&
        weekYear == System.Globalization.ISOWeek.GetYear(utc);
}
