namespace BookPromoterAI;

static class PostSchedule
{
    public const int DefaultPostHourLocal = 10;
    public const int DefaultPostMinuteLocal = 0;
    const int StaggerHoursPerSlot = 4;

    /// <summary>Spread pending posts across separate days in the ISO week (e.g. 3/week → Mon, Wed, Fri).</summary>
    public static void AssignWeeklyPostSlots(
        IList<DbGeneratedAd> ads,
        int postsPerWeek,
        DateTime nowUtc,
        int weekYear,
        int weekNumber,
        bool onlyAdsMissingFutureSlot = false)
    {
        if (postsPerWeek <= 0) return;

        var pending = ads
            .Where(a => a.PostStatus == "Pending")
            .Where(a => !onlyAdsMissingFutureSlot || a.ScheduledPostAt is null || a.ScheduledPostAt <= nowUtc)
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
        var todayOffset = TodayOffsetInWeek(weekYear, weekNumber, nowUtc);

        while (result.Count < slotCount)
        {
            var monday = System.Globalization.ISOWeek.ToDateTime(weekYear, weekNumber, DayOfWeek.Monday)
                .AddDays(weekOffset * 7);
            var slotYear = System.Globalization.ISOWeek.GetYear(monday);
            var slotWeek = System.Globalization.ISOWeek.GetWeekOfYear(monday);

            var offsetsThisWeek = weekOffset == 0
                ? dayOffsets.Where(o => o >= todayOffset).ToArray()
                : dayOffsets;

            if (offsetsThisWeek.Length == 0)
            {
                weekOffset++;
                if (weekOffset > 52) break;
                continue;
            }

            var dayStagger = new Dictionary<int, int>();
            foreach (var dayOffset in offsetsThisWeek)
            {
                if (result.Count >= slotCount) break;

                var stagger = dayStagger.GetValueOrDefault(dayOffset, 0);
                dayStagger[dayOffset] = stagger + 1;
                var hour = Math.Min(DefaultPostHourLocal + stagger * StaggerHoursPerSlot, 20);

                var slot = SlotAtDayOffsetUtc(slotYear, slotWeek, dayOffset, hour, DefaultPostMinuteLocal);
                if (weekOffset == 0 && slot <= nowUtc)
                    continue;

                result.Add(slot);
            }

            weekOffset++;
            if (weekOffset > 52) break;
        }

        if (result.Count < slotCount)
        {
            var fallback = nowUtc.AddHours(1);
            for (var i = result.Count; i < slotCount; i++)
                result.Add(fallback.AddDays(i - result.Count));
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

    /// <summary>
    /// Next brand auto-post time. Uses even spacing across the week so missed
    /// weekday slots still catch up (unlike Ad Library day-of-week assignment).
    /// </summary>
    public static DateTime? NextBrandAutoPostUtc(SocialSchedule schedule, DateTime nowUtc)
    {
        if (schedule.PostsPerWeek <= 0 || schedule.PostsSentThisWeek >= schedule.PostsPerWeek)
            return null;

        if (schedule.LastPostedAt is not DateTime last)
            return nowUtc;

        var hoursBetween = (24.0 * 7) / schedule.PostsPerWeek;
        var next = last.AddHours(hoursBetween);
        return next > nowUtc ? next : nowUtc;
    }

    public static DateTime DisplayTime(GeneratedAd ad) =>
        ad.PostStatus == "Posted" && ad.PostedAt is not null
            ? ad.PostedAt.Value
            : ad.ScheduledPostAt ?? ad.GeneratedAt;

    public static string FormatAdTimeSubtitle(GeneratedAd ad)
    {
        var when = DisplayTime(ad);
        var formatted = AppTimeZone.FormatWithZone(when, "ddd MMM d, HH:mm");
        var prefix = ad.PostStatus switch
        {
            "Posted" => "Posted",
            "Failed" => "Failed",
            _ => "Scheduled"
        };
        var delivery = PostDeliveryKinds.Label(ad.PostedVia);
        return string.IsNullOrEmpty(delivery)
            ? $"{prefix} {formatted}"
            : $"{prefix} {formatted} · {delivery}";
    }

    public static string? FormatAdAutoPostHint(GeneratedAd ad, SocialSchedule? schedule)
    {
        if (PostLimits.IsInkitt(ad.Platform))
        {
            if (ad.PostStatus != "Pending") return null;
            return "Copy this post and paste it on your Inkitt author wall (Open my Inkitt wall below).";
        }

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

    static int TodayOffsetInWeek(int weekYear, int weekNumber, DateTime nowUtc)
    {
        var monday = System.Globalization.ISOWeek.ToDateTime(weekYear, weekNumber, DayOfWeek.Monday);
        var mondayLocal = AppTimeZone.ToLocal(monday).Date;
        var todayLocal = AppTimeZone.ToLocal(nowUtc).Date;
        return Math.Clamp((int)(todayLocal - mondayLocal).TotalDays, 0, 6);
    }
}
