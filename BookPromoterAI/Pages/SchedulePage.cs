using System.Text;
namespace BookPromoterAI;

static class SchedulePage
{
    public static readonly (string Value, string Group)[] AllPlatforms =
    [
        ("Facebook",        "Major Platforms"),
        ("Instagram",       "Major Platforms"),
        ("X (Twitter)",     "Major Platforms"),
        ("TikTok",          "Major Platforms"),
        ("YouTube",         "Major Platforms"),
        ("LinkedIn",        "Major Platforms"),
        ("Pinterest",       "Major Platforms"),
        ("Snapchat",        "Major Platforms"),
        ("Threads",         "Emerging Platforms"),
        ("Bluesky",         "Emerging Platforms"),
        ("Mastodon",        "Emerging Platforms"),
        ("BeReal",          "Emerging Platforms"),
        ("Lemon8",          "Emerging Platforms"),
        ("Nostr",           "Emerging Platforms"),
        ("Telegram",        "Messaging & Community"),
        ("WhatsApp",        "Messaging & Community"),
        ("Discord",         "Messaging & Community"),
        ("Reddit",          "Messaging & Community"),
        ("Quora",           "Messaging & Community"),
        ("Clubhouse",       "Messaging & Community"),
        ("Goodreads",       "Books & Reading"),
        ("BookTok",         "Books & Reading"),
        ("Bookstagram",     "Books & Reading"),
        ("Wattpad",         "Books & Reading"),
        ("Royal Road",      "Books & Reading"),
        ("Scribble Hub",    "Books & Reading"),
        ("Substack",        "Content & Blogging"),
        ("Medium",          "Content & Blogging"),
        ("Tumblr",          "Content & Blogging"),
        ("WordPress",       "Content & Blogging"),
        ("Patreon",         "Content & Blogging"),
        ("Ko-fi",           "Content & Blogging"),
        ("Twitch",          "Other"),
        ("Rumble",          "Other"),
        ("Kick",            "Other"),
        ("Vimeo",           "Other"),
        ("Flickr",          "Other"),
        ("MeWe",            "Other"),
        ("VK",              "Other"),
        ("Weibo",           "Other"),
        ("Line",            "Other"),
    ];

    public static string Render(AppStoreDb store, string notice)
    {
        var noticeHtml = "";
        if (!string.IsNullOrWhiteSpace(notice))
        {
            var parts = notice.Split(':', 2);
            var cls = parts[0] == "success" ? "success" : "error";
            var msg = parts.Length > 1 ? parts[1] : notice;
            noticeHtml = $"""<div class="notice {cls}">{H.Encode(msg)}</div>""";
        }

        // Each schedule row is inside the main save form.
        // The Remove button uses its OWN form (id="remove-{platform}")
        // declared OUTSIDE the save form, and a button with form="remove-{id}"
        // to attach to it — this avoids illegal nested forms which break
        // the hidden platform fields that the save route depends on.
        var rows = new StringBuilder();
        var removeForms = new StringBuilder();

        foreach (var schedule in store.Schedules)
        {
            var checkedText = schedule.RequiresApproval ? "checked" : "";
            var removeFormId = $"remove-{H.Encode(schedule.Platform).Replace(" ", "-")}";

            rows.Append($"""
                <div class="schedule-row">
                    <input type="hidden" name="platform" value="{H.Encode(schedule.Platform)}">
                    <strong>{H.Encode(schedule.Platform)}</strong>
                    <label>Posts per week
                        <input name="postsPerWeek" type="number" min="0" max="14" value="{schedule.PostsPerWeek}">
                    </label>
                    <label class="checkbox">
                        <input name="requiresApproval" value="{H.Encode(schedule.Platform)}" type="checkbox" {checkedText}>
                        Approval required
                    </label>
                    <button class="danger-button small" type="submit" form="{removeFormId}">Remove</button>
                </div>
                """);

            removeForms.Append($"""
                <form id="{removeFormId}" method="post" action="/schedule/remove-platform/{Uri.EscapeDataString(schedule.Platform)}" style="display:none"></form>
                """);
        }

        var plan = store.CurrentPlan;
        var totalWeeklyPosts = store.Schedules.Sum(s => s.PostsPerWeek);
        var limitText = plan?.MaxWeeklyPosts is int cap
            ? $"""<p class="muted small-text">Your {H.Encode(plan.Name)} plan allows up to <strong>{cap} posts/week</strong> (about {H.Encode(plan.AiPostsPerMonthText)} AI posts/month). Currently scheduling <strong>{totalWeeklyPosts}</strong>/week.</p>"""
            : """<p class="muted small-text">Your plan includes unlimited AI posts per month.</p>""";

        var alreadyAdded = store.Schedules.Select(s => s.Platform).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var optionsByGroup = AllPlatforms
            .Where(p => !alreadyAdded.Contains(p.Value))
            .GroupBy(p => p.Group);

        var dropdownOptions = new StringBuilder();
        dropdownOptions.Append("""<option value="">Choose a platform...</option>""");
        foreach (var group in optionsByGroup)
        {
            dropdownOptions.Append($"""<optgroup label="{H.Encode(group.Key)}">""");
            foreach (var (value, _) in group)
                dropdownOptions.Append($"""<option value="{H.Encode(value)}">{H.Encode(value)}</option>""");
            dropdownOptions.Append("</optgroup>");
        }
        dropdownOptions.Append("""<option value="__custom__">Other (type your own)...</option>""");

        var script = """
            <script>
            function onPlatformSelect(select) {
                var customLabel = document.getElementById('custom-platform-label');
                var hidden = document.getElementById('platform-hidden');
                var customInput = document.getElementById('custom-platform-input');
                if (select.value === '__custom__') {
                    customLabel.style.display = 'block';
                    hidden.value = '';
                    hidden.name = '';
                    customInput.required = true;
                    customInput.name = 'newPlatform';
                } else {
                    customLabel.style.display = 'none';
                    hidden.value = select.value;
                    hidden.name = 'newPlatform';
                    customInput.required = false;
                    customInput.name = '';
                }
            }
            </script>
            """;

        return $"""
            {removeForms}

            <section class="panel">
                <h1>Posting Schedule Manager</h1>
                <p class="muted">Choose how many times each platform should post every week.</p>
                {limitText}
                {noticeHtml}
                <form method="post" action="/schedule" class="schedule-list">
                    {rows}
                    <button class="button" type="submit">Save Schedule</button>
                </form>
            </section>

            <section class="panel">
                <h2>Add a Platform</h2>
                <p class="muted">Select from the list or type your own.</p>
                <form method="post" action="/schedule/add-platform" class="inline-form">
                    <label>Platform
                        <select id="platform-select" onchange="onPlatformSelect(this)">
                            {dropdownOptions}
                        </select>
                    </label>
                    <label id="custom-platform-label" style="display:none">Custom name
                        <input id="custom-platform-input" placeholder="Enter platform name">
                    </label>
                    <input type="hidden" id="platform-hidden" name="newPlatform">
                    <button class="button" type="submit">Add to Schedule</button>
                </form>
            </section>

            {script}
            """;
    }
}
