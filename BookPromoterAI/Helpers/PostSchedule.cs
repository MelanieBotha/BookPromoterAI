namespace BookPromoterAI;

static class PostSchedule
{
    public const int DefaultPostHourLocal = 10;
    public const int DefaultPostMinuteLocal = 0;

    /// <summary>Spread pending posts across separate days in the ISO week (e.g. 3/week → Mon, Wed, Fri).</summary>
    public static void AssignWeeklyPostSlots(
        IList<DbGeneratedAd> ads,
        int postsPerWeek,
        DateTime nowUtc,
        int weekYear,
        int weekNumber)
    {
        if (postsPerWeek <= 0) return;

        var pending = ads
            .Where(a => a.PostStatus == "Pending")
            .OrderBy(a => a.ScheduledPostAt ?? a.GeneratedAt)
            .ThenBy(a => a.Id)
            .ToList();
        if (pending.Count == 0) return;

        var slots = BuildSlotTimes(postsPerWeek, pending.Count, weekYear, weekNumber, nowUtc);
        for (var i = 0; i < pending.Count && i < slots.Count; i++)
            pending[i].ScheduledPostAt = slots[i];
    }

    /// <summary>Evenly spaced day offsets within a 7-day week (3 → Mon, Wed, Fri).</summary>
    public static int[] DayOffsetsForCount(int count)
    {
        if (count <= 0) return [];
        if (count == 1) return [3];
        var offsets = new int[count];
        for (var i = 0; i < count; i++)
            offsets[i] = (i * 7) / count;
        return offsets;
    }

    public static List<DateTime> BuildSlotTimes(
        int postsPerWeek,
        int slotCount,
        int weekYear,
        int weekNumber,
        DateTime nowUtc)
    {
        if (postsPerWeek <= 0 || slotCount <= 0) return [];

        var dayOffsets = DayOffsetsForCount(postsPerWeek);
        var result = new List<DateTime>(slotCount);
        var weekOffset = 0;

        while (result.Count < slotCount)
        {
            var monday = System.Globalization.ISOWeek.ToDateTime(weekYear, weekNumber, DayOfWeek.Monday)
                .AddDays(weekOffset * 7);
            var slotYear = System.Globalization.ISOWeek.GetYear(monday);
            var slotWeek = System.Globalization.ISOWeek.GetWeekOfYear(monday);

            foreach (var dayOffset in dayOffsets)
            {
                var slot = SlotAtDayOffsetUtc(slotYear, slotWeek, dayOffset);
                if (weekOffset == 0 && slot <= nowUtc)
                    continue;

                result.Add(slot);
                if (result.Count >= slotCount) break;
            }

            weekOffset++;
            if (weekOffset > 52) break;
        }

        if (result.Count < slotCount)
        {
            var fallback = nowUtc.AddHours(1);
            while (result.Count < slotCount)
                result.Add(fallback.AddDays(result.Count));
        }

        return result;
    }

    public static DateTime SlotAtDayOffsetUtc(int weekYear, int weekNumber, int dayOffset, int hourLocal = DefaultPostHourLocal, int minuteLocal = DefaultPostMinuteLocal)
    {
        var monday = System.Globalization.ISOWeek.ToDateTime(weekYear, weekNumber, DayOfWeek.Monday);
        var localDate = monday.Date.AddDays(dayOffset);
        var localWallClock = new DateTime(localDate.Year, localDate.Month, localDate.Day, hourLocal, minuteLocal, 0, DateTimeKind.Unspecified);
        return AppTimeZone.ToUtcFromLocal(localWallClock);
    }

    /// <summary>Next brand auto-post slot based on posts already sent this week.</summary>
    public static DateTime? NextBrandAutoPostUtc(SocialSchedule schedule, DateTime nowUtc)
    {
        if (schedule.PostsPerWeek <= 0 || schedule.PostsSentThisWeek >= schedule.PostsPerWeek)
            return null;

        var weekYear = System.Globalization.ISOWeek.GetYear(nowUtc);
        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(nowUtc);
        var dayOffsets = DayOffsetsForCount(schedule.PostsPerWeek);
        var slotIndex = Math.Clamp(schedule.PostsSentThisWeek, 0, dayOffsets.Length - 1);

        for (var weekOffset = 0; weekOffset <= 1; weekOffset++)
        {
            var monday = System.Globalization.ISOWeek.ToDateTime(weekYear, weekNumber, DayOfWeek.Monday)
                .AddDays(weekOffset * 7);
            var slotYear = System.Globalization.ISOWeek.GetYear(monday);
            var slotWeek = System.Globalization.ISOWeek.GetWeekOfYear(monday);

            for (var i = slotIndex; i < dayOffsets.Length; i++)
            {
                var slot = SlotAtDayOffsetUtc(slotYear, slotWeek, dayOffsets[i]);
                if (slot > nowUtc)
                    return slot;
            }

            slotIndex = 0;
        }

        return nowUtc;
    }

    public static DateTime DisplayTime(GeneratedAd ad) =>
        ad.ScheduledPostAt ?? ad.GeneratedAt;

    public static string? FormatAdAutoPostHint(GeneratedAd ad, SocialSchedule? schedule)
    {
        if (schedule is null || !schedule.AutoPostEnabled || schedule.PostsPerWeek <= 0)
            return null;
        if (ad.PostStatus != "Pending")
            return null;
        if (schedule.RequiresApproval && !ad.ApprovedForPosting)
            return "Approve this post to include it in auto-posting.";

        if (schedule.PostsSentThisWeek >= schedule.PostsPerWeek && ad.PostedAt is null)
            return "Weekly auto-post limit reached for this platform.";

        var when = ad.ScheduledPostAt ?? ad.GeneratedAt;
        if (when > DateTime.UtcNow)
            return $"Auto-post scheduled: ~{AppTimeZone.FormatWithZone(when, "ddd MMM d, HH:mm")}";

        return "Auto-post checks every 5 minutes — due now.";
    }
}
