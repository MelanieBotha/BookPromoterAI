namespace BookPromoterAI;

/// <summary>Display times in the app owner's local zone (San Jose / Pacific by default).</summary>
static class AppTimeZone
{
    public const string DefaultId = "America/Los_Angeles";

    static TimeZoneInfo _zone = Resolve(DefaultId);

    public static void Configure(string? timeZoneId) =>
        _zone = Resolve(string.IsNullOrWhiteSpace(timeZoneId) ? DefaultId : timeZoneId.Trim());

    public static DateTime ToLocal(DateTime utc)
    {
        var normalized = utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Local => utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc)
        };
        return TimeZoneInfo.ConvertTimeFromUtc(normalized, _zone);
    }

    public static DateTime ToUtcFromLocal(DateTime localWallClock)
    {
        var unspecified = localWallClock.Kind switch
        {
            DateTimeKind.Utc => localWallClock,
            DateTimeKind.Local => DateTime.SpecifyKind(localWallClock, DateTimeKind.Unspecified),
            _ => localWallClock
        };
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, _zone);
    }

    public static string Format(DateTime utc, string format) =>
        ToLocal(utc).ToString(format);

    public static string FormatWithZone(DateTime utc, string format) =>
        $"{Format(utc, format)} {Abbreviation(utc)}";

    public static string Abbreviation(DateTime utc)
    {
        var local = ToLocal(utc);
        if (IsPacificZone(_zone))
            return _zone.IsDaylightSavingTime(local) ? "PDT" : "PST";

        var name = _zone.IsDaylightSavingTime(local) ? _zone.DaylightName : _zone.StandardName;
        return string.IsNullOrWhiteSpace(name) ? "local" : name;
    }

    static bool IsPacificZone(TimeZoneInfo tz) =>
        tz.Id.Contains("Los_Angeles", StringComparison.OrdinalIgnoreCase) ||
        tz.Id.Contains("Pacific", StringComparison.OrdinalIgnoreCase);

    static TimeZoneInfo Resolve(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            if (string.Equals(id, DefaultId, StringComparison.OrdinalIgnoreCase))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"); }
                catch (TimeZoneNotFoundException) { }
            }

            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
