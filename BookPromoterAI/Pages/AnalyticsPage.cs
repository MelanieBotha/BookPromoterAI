using System.Text;
namespace BookPromoterAI;

static class AnalyticsPage
{
    // Last 6 months for charts/tables
    static List<(string Key, string Label)> RecentMonths()
    {
        var months = new List<(string, string)>();
        for (var i = 5; i >= 0; i--)
        {
            var d = DateTime.UtcNow.AddMonths(-i);
            months.Add((d.ToString("yyyy-MM"), d.ToString("MMM yyyy")));
        }
        return months;
    }

    public static string Render(AppStoreDb store)
    {
        var months = RecentMonths();
        var books = store.Books;
        var totalClicks = books.Sum(b => b.MonthlyClicks);
        var totalPosts = store.GeneratedAds.Count;
        var totalScheduled = store.Schedules.Sum(s => s.PostsPerWeek);
        var topBook = books.OrderByDescending(b => b.MonthlyClicks).FirstOrDefault();
        var lowestBook = books.Where(b => b.MonthlyClicks > 0).OrderBy(b => b.MonthlyClicks).FirstOrDefault();
        var hasAdvanced = store.HasAdvancedAnalytics;

        // ── Summary stat cards (all tiers) ───────────────────────────
        var summaryCards = $"""
            <div class="analytics-summary-grid">
                <div class="analytics-card">
                    <span class="analytics-num">{totalClicks}</span>
                    <span class="analytics-label">Total Clicks This Month</span>
                </div>
                <div class="analytics-card">
                    <span class="analytics-num">{books.Count}</span>
                    <span class="analytics-label">Books</span>
                </div>
                <div class="analytics-card">
                    <span class="analytics-num">{totalScheduled}</span>
                    <span class="analytics-label">Posts Scheduled / Week</span>
                </div>
                <div class="analytics-card">
                    <span class="analytics-num">{totalPosts}</span>
                    <span class="analytics-label">Total Posts Generated</span>
                </div>
            </div>
            """;

        // ── Top / Lowest performer boxes (all tiers) ──────────────────
        var topBox = topBook is not null
            ? $"""
                <div class="analytics-performer-card top">
                    <p class="analytics-performer-label">Top Performing Book</p>
                    <p class="analytics-performer-book">{H.Encode(topBook.Title)}</p>
                    <p class="analytics-performer-stat">{topBook.MonthlyClicks} clicks this month</p>
                </div>
                """
            : """<div class="analytics-performer-card top"><p class="muted">No books yet.</p></div>""";

        var lowestBox = lowestBook is not null
            ? $"""
                <div class="analytics-performer-card lowest">
                    <p class="analytics-performer-label">Lowest Performing Book</p>
                    <p class="analytics-performer-book">{H.Encode(lowestBook.Title)}</p>
                    <p class="analytics-performer-stat">{lowestBook.MonthlyClicks} clicks this month</p>
                </div>
                """
            : """<div class="analytics-performer-card lowest"><p class="muted">No click data yet.</p></div>""";

        // ── Bar chart — clicks per book per month (all tiers, basic) ──
        var barChart = BuildBarChart(books, months);

        // ── Upgrade prompt for lower tiers ────────────────────────────
        var upgradeNotice = !hasAdvanced
            ? $"""
                <div class="notice error">
                    You're on the <strong>{H.Encode(store.CurrentPlan?.Name ?? store.AccessType)}</strong> plan.
                    Upgrade to <strong>Publisher</strong> or <strong>Agency</strong> to unlock full analytics —
                    monthly tables per author, platform estimates, and posting activity.
                    <a href="/billing">Upgrade now</a>
                </div>
                """
            : "";

        // ── Full analytics (Publisher / Agency / Lifetime only) ───────
        var fullAnalytics = hasAdvanced
            ? BuildFullAnalytics(store, books, months)
            : BuildBlurredPreview(store, books, months);

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Analytics</p>
                    <h1>Track how your books and posts are performing.</h1>
                </div>
            </section>

            {upgradeNotice}

            {summaryCards}

            <div class="analytics-performers">
                {topBox}
                {lowestBox}
            </div>

            <section class="panel">
                <h2>Clicks Per Book &mdash; Last 6 Months</h2>
                <p class="muted small-text">Monthly link clicks tracked via your unique book tracking URLs.</p>
                {barChart}
            </section>

            {fullAnalytics}
            """;
    }

    // ── SVG stacked bar chart ─────────────────────────────────────────
    static string BuildBarChart(List<Book> books, List<(string Key, string Label)> months)
    {
        if (books.Count == 0)
            return """<p class="muted">No books yet.</p>""";

        // Use distinct colours per book
        var colours = new[] { "#0f766e", "#6366f1", "#f59e0b", "#ef4444", "#10b981", "#8b5cf6", "#f97316", "#06b6d4", "#84cc16", "#ec4899" };
        var chartWidth = 560;
        var chartHeight = 220;
        var barAreaWidth = chartWidth - 60;
        var barWidth = (int)(barAreaWidth / months.Count * 0.6);
        var gap = (int)(barAreaWidth / months.Count);

        // Find max total for any month across all books
        var monthlyTotals = months.Select(m => books.Sum(b => b.ClickHistory.TryGetValue(m.Key, out var v) ? v : 0)).ToList();
        var maxTotal = Math.Max(1, monthlyTotals.Max());

        var bars = new StringBuilder();
        var labels = new StringBuilder();
        var legend = new StringBuilder();

        for (var mi = 0; mi < months.Count; mi++)
        {
            var (key, label) = months[mi];
            var x = 50 + mi * gap + (gap - barWidth) / 2;
            var yBase = chartHeight - 30;
            var stackY = yBase;

            // Short month label
            var shortLabel = label[..3];
            labels.Append($"""<text x="{x + barWidth / 2}" y="{chartHeight - 10}" text-anchor="middle" font-size="11" fill="#667085">{H.Encode(shortLabel)}</text>""");

            for (var bi = 0; bi < books.Count; bi++)
            {
                var book = books[bi];
                var clicks = book.ClickHistory.TryGetValue(key, out var v) ? v : 0;
                if (clicks == 0) continue;
                var barH = (int)((double)clicks / maxTotal * (chartHeight - 50));
                if (barH < 1) barH = 1;
                stackY -= barH;
                var colour = colours[bi % colours.Length];
                bars.Append($"""<rect x="{x}" y="{stackY}" width="{barWidth}" height="{barH}" fill="{colour}" rx="2"><title>{H.Encode(book.Title)}: {clicks} clicks in {label}</title></rect>""");
            }
        }

        // Legend
        for (var bi = 0; bi < Math.Min(books.Count, colours.Length); bi++)
        {
            var colour = colours[bi % colours.Length];
            var shortTitle = books[bi].Title.Length > 20 ? books[bi].Title[..20] + "…" : books[bi].Title;
            legend.Append($"""
                <div class="chart-legend-item">
                    <span class="chart-legend-dot" style="background:{colour}"></span>
                    <span>{H.Encode(shortTitle)}</span>
                </div>
                """);
        }

        // Y-axis ticks
        var yAxis = new StringBuilder();
        for (var tick = 0; tick <= 4; tick++)
        {
            var val = (int)(maxTotal * tick / 4.0);
            var y = chartHeight - 30 - (int)((double)tick / 4 * (chartHeight - 50));
            yAxis.Append($"""
                <line x1="45" y1="{y}" x2="{chartWidth - 10}" y2="{y}" stroke="#d7dde8" stroke-width="1"/>
                <text x="40" y="{y + 4}" text-anchor="end" font-size="10" fill="#667085">{val}</text>
                """);
        }

        return $"""
            <div class="chart-wrap">
                <svg viewBox="0 0 {chartWidth} {chartHeight}" xmlns="http://www.w3.org/2000/svg" class="bar-svg">
                    {yAxis}
                    {bars}
                    {labels}
                </svg>
                <div class="chart-legend">{legend}</div>
            </div>
            """;
    }

    // ── Full analytics tables (Publisher / Agency) ────────────────────
    static string BuildFullAnalytics(AppStoreDb store, List<Book> books, List<(string Key, string Label)> months)
    {
        var result = new StringBuilder();

        // Group by author/client
        var groups = GroupBooksByAuthor(store, books);

        foreach (var (authorName, authorBooks) in groups)
        {
            result.Append($"""
                <section class="panel">
                    <h2>Clicks per Month &mdash; {H.Encode(authorName)}</h2>
                    {BuildMonthlyTable(authorBooks, months, "book-clicks-table")}
                </section>
                """);

            // Platform estimates per author
            var totalScheduled = store.Schedules.Sum(s => s.PostsPerWeek);
            if (totalScheduled > 0)
            {
                var authorClicks = authorBooks.Sum(b => b.MonthlyClicks);
                result.Append($"""
                    <section class="panel">
                        <h2>Estimated Clicks Per Platform &mdash; {H.Encode(authorName)}</h2>
                        <p class="muted small-text">Estimated by distributing total clicks proportionally across platforms based on your posting schedule.</p>
                        {BuildPlatformTable(store, authorClicks, months)}
                    </section>
                    """);
            }
        }

        // Posting activity summary
        var postedCount = store.GeneratedAds.Count(a => a.PostStatus == "Posted");
        var pendingCount = store.GeneratedAds.Count(a => a.PostStatus == "Pending");
        var failedCount = store.GeneratedAds.Count(a => a.PostStatus == "Failed");

        result.Append($"""
            <section class="panel">
                <h2>Posting Activity</h2>
                <div class="analytics-summary-grid">
                    <div class="analytics-card"><span class="analytics-num">{store.GeneratedAds.Count}</span><span class="analytics-label">Posts Generated</span></div>
                    <div class="analytics-card posted"><span class="analytics-num">{postedCount}</span><span class="analytics-label">Posted</span></div>
                    <div class="analytics-card pending"><span class="analytics-num">{pendingCount}</span><span class="analytics-label">Pending</span></div>
                    <div class="analytics-card failed"><span class="analytics-num">{failedCount}</span><span class="analytics-label">Failed</span></div>
                </div>
            </section>
            """);

        return result.ToString();
    }

    // ── Blurred preview for lower tiers ──────────────────────────────
    static string BuildBlurredPreview(AppStoreDb store, List<Book> books, List<(string Key, string Label)> months)
    {
        return $"""
            <section class="panel analytics-locked-preview">
                <h2>Full Analytics &mdash; Publisher &amp; Agency Plans</h2>
                <p class="muted">Upgrade to unlock monthly tables per author, estimated clicks per platform, posting activity summary, and more.</p>
                <div class="analytics-preview-blur">
                    <div class="analytics-table-placeholder">
                        <table class="analytics-month-table">
                            <thead><tr><th>Book</th><th>Jan</th><th>Feb</th><th>Mar</th><th>Apr</th><th>May</th><th>Jun</th><th>Total</th></tr></thead>
                            <tbody>
                                <tr><td>Your Book Title</td><td>45</td><td>52</td><td>38</td><td>61</td><td>49</td><td>55</td><td>300</td></tr>
                                <tr><td>Another Book</td><td>22</td><td>31</td><td>28</td><td>35</td><td>41</td><td>38</td><td>195</td></tr>
                                <tr><td>Third Title</td><td>12</td><td>18</td><td>15</td><td>22</td><td>19</td><td>24</td><td>110</td></tr>
                            </tbody>
                        </table>
                    </div>
                    <div class="analytics-blur-overlay">
                        <a class="button" href="/billing">Upgrade to Publisher or Agency</a>
                    </div>
                </div>
            </section>
            """;
    }

    // ── Monthly clicks table ──────────────────────────────────────────
    static string BuildMonthlyTable(List<Book> books, List<(string Key, string Label)> months, string cssClass)
    {
        if (books.Count == 0) return """<p class="muted">No books for this author yet.</p>""";

        var header = new StringBuilder("<tr><th>Book</th>");
        foreach (var (_, label) in months)
            header.Append($"<th>{H.Encode(label[..3])}</th>");
        header.Append("<th>Total</th></tr>");

        var rows = new StringBuilder();
        foreach (var book in books)
        {
            rows.Append($"<tr><td>{H.Encode(book.Title)}</td>");
            var rowTotal = 0;
            foreach (var (key, _) in months)
            {
                var v = book.ClickHistory.TryGetValue(key, out var clicks) ? clicks : 0;
                rowTotal += v;
                rows.Append($"<td>{(v > 0 ? v.ToString() : "")}</td>");
            }
            rows.Append($"<td><strong>{rowTotal}</strong></td></tr>");
        }

        // Totals row
        rows.Append("<tr class=\"totals-row\"><td><strong>Total</strong></td>");
        var grandTotal = 0;
        foreach (var (key, _) in months)
        {
            var colTotal = books.Sum(b => b.ClickHistory.TryGetValue(key, out var v) ? v : 0);
            grandTotal += colTotal;
            rows.Append($"<td><strong>{(colTotal > 0 ? colTotal.ToString() : "")}</strong></td>");
        }
        rows.Append($"<td><strong>{grandTotal}</strong></td></tr>");

        return $"""
            <div class="analytics-table-scroll">
                <table class="analytics-month-table {cssClass}">
                    <thead>{header}</thead>
                    <tbody>{rows}</tbody>
                </table>
            </div>
            """;
    }

    // ── Estimated platform clicks table ───────────────────────────────
    static string BuildPlatformTable(AppStoreDb store, int totalClicks, List<(string Key, string Label)> months)
    {
        var totalWeekly = store.Schedules.Sum(s => s.PostsPerWeek);
        if (totalWeekly == 0) return """<p class="muted">Add platforms to your schedule to see estimates.</p>""";

        var header = new StringBuilder("<tr><th>Platform</th>");
        foreach (var (_, label) in months)
            header.Append($"<th>{H.Encode(label[..3])}</th>");
        header.Append("<th>Est. Total</th></tr>");

        var rows = new StringBuilder();
        foreach (var schedule in store.Schedules.Where(s => s.PostsPerWeek > 0).OrderByDescending(s => s.PostsPerWeek))
        {
            var share = (double)schedule.PostsPerWeek / totalWeekly;
            var estimated = (int)(totalClicks * share);
            rows.Append($"<tr><td>{H.Encode(schedule.Platform)}</td>");
            foreach (var _ in months)
                rows.Append($"<td class=\"muted-cell\">{(int)(estimated / 6)}</td>");
            rows.Append($"<td><strong>{estimated}</strong></td></tr>");
        }

        return $"""
            <div class="analytics-table-scroll">
                <table class="analytics-month-table">
                    <thead>{header}</thead>
                    <tbody>{rows}</tbody>
                </table>
            </div>
            """;
    }

    static List<(string Name, List<Book> Books)> GroupBooksByAuthor(AppStoreDb store, List<Book> books)
    {
        var result = new List<(string, List<Book>)>();
        if (store.HasMultiClient && store.Clients.Count > 0)
        {
            foreach (var client in store.Clients)
            {
                var clientBooks = books.Where(b => b.ClientId == client.Id).ToList();
                if (clientBooks.Count > 0)
                    result.Add((client.Name, clientBooks));
            }
            var unmatched = books.Where(b => b.ClientId is null).ToList();
            if (unmatched.Count > 0) result.Add(("Unassigned", unmatched));
        }
        else
        {
            result.Add(("All Books", books));
        }
        return result;
    }
}
