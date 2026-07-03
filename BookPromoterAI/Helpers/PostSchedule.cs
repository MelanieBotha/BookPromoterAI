namespace BookPromoterAI;

static class PostSchedule
{
    /// <summary>Spread pending posts for one platform across the rest of the ISO week.</summary>
    public static void AssignWeeklyPostSlots(
        IList<DbGeneratedAd> ads,
        int postsPerWeek,
        DateTime nowUtc,
        int weekYear,
        int weekNumber)
    {
        var pending = ads
            .Where(a => a.PostStatus == "Pending")
            .OrderBy(a => a.ScheduledPostAt ?? a.GeneratedAt)
            .ThenBy(a => a.Id)
            .ToList();
        if (pending.Count == 0) return;

        var weekStart = System.Globalization.ISOWeek.ToDateTime(weekYear, weekNumber, DayOfWeek.Monday);
        var weekEnd = weekStart.AddDays(7);
        var slotCount = Math.Max(postsPerWeek, pending.Count);
        var hoursBetween = (24.0 * 7) / slotCount;
        if (slotCount <= 7)
            hoursBetween = Math.Max(hoursBetween, 24.0);
        var anchor = nowUtc > weekStart ? nowUtc : weekStart.AddHours(10);

        for (var i = 0; i < pending.Count; i++)
        {
            var slotTime = anchor.AddHours(i * hoursBetween);
            if (slotTime >= weekEnd)
                slotTime = weekEnd.AddMinutes(-30 - i * 3);

            pending[i].ScheduledPostAt = slotTime;
        }
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
            return $"Auto-post scheduled: ~{when:ddd MMM d, HH:mm} UTC";

        return "Auto-post checks every 5 minutes — due now.";
    }
}
