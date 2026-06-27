using System.Text;
namespace BookPromoterAI;

static class MyAccountPage
{
    public static string Render(AppStoreDb store, string notice, SocialAccount? editingAccount = null)
    {
        var plan = store.CurrentPlan;
        var planName = plan?.Name ?? store.AccessType;
        var userCode = store.CurrentUserCode ?? "N/A";

        // ── Account Details ──────────────────────────────────────────
        var accountSection = $"""
            <section class="panel">
                <h2>Account Details</h2>
                <p>Email: <strong>{H.Encode(store.LoggedInEmail ?? "")}</strong></p>
                <p>Account Code: <strong>{H.Encode(userCode)}</strong></p>
                <p>Current plan: <strong>{H.Encode(planName)}</strong></p>
                <p>
                    <a href="/billing">Manage Billing</a> &middot;
                    <a href="/billing">Change Plan</a> &middot;
                    <a href="/logout">Log Out</a>
                </p>
            </section>
            """;

        // ── Social Accounts + Posting Schedule (integrated) ───────────
        // Each connected account also shows its posting frequency and
        // approval setting inline, since a social account and its
        // schedule entry are really the same thing from the user's
        // point of view — no need to fill in the platform name twice.
        var accountRows = new StringBuilder();
        var removeAccountForms = new StringBuilder();

        foreach (var account in store.SocialAccounts)
        {
            var connectionStatus = account.ConnectedViaOAuth
                ? """<small class="status available">Connected via OAuth (simulated)</small>"""
                : """<small class="status used">Manually added</small>""";

            var schedule = store.Schedules.FirstOrDefault(s => s.Platform.Equals(account.Platform, StringComparison.OrdinalIgnoreCase));
            var postsPerWeek = schedule?.PostsPerWeek ?? 0;
            var requiresApproval = schedule?.RequiresApproval ?? true;
            var checkedText = requiresApproval ? "checked" : "";
            var autoPostChecked = (schedule?.AutoPostEnabled ?? false) ? "checked" : "";

            var removeFormId = $"remove-account-{account.Id}";

            accountRows.Append($"""
                <article class="book-row account-schedule-row">
                    <div>
                        <strong>{H.Encode(account.Platform)}</strong>
                        <p>{H.Encode(account.DisplayName)} - @{H.Encode(account.Handle)}</p>
                        {connectionStatus}
                    </div>
                    <input type="hidden" name="platform" value="{H.Encode(account.Platform)}">
                    <label>Posts/week
                        <input name="postsPerWeek" type="number" min="0" max="14" value="{postsPerWeek}">
                    </label>
                    <label class="checkbox">
                        <input name="requiresApproval" value="{H.Encode(account.Platform)}" type="checkbox" {checkedText}>
                        Approval required
                    </label>
                    <label class="checkbox">
                        <input name="autoPostEnabled" value="{H.Encode(account.Platform)}" type="checkbox" {autoPostChecked}>
                        Auto-post
                    </label>
                    <div class="row-actions">
                        <a class="button small" href="/social-accounts/edit/{account.Id}">Edit</a>
                        <button class="danger-button small" type="submit" form="{removeFormId}">Remove</button>
                    </div>
                </article>
                """);

            removeAccountForms.Append($"""
                <form id="{removeFormId}" method="post" action="/social-accounts/delete/{account.Id}" style="display:none"></form>
                """);
        }

        if (store.SocialAccounts.Count == 0)
            accountRows.Append("""<p class="muted">No social accounts connected yet. Connect one using the buttons below.</p>""");

        var limitNotice = "";
        var limitMessage = store.CheckSocialAccountLimit();
        if (limitMessage is not null)
            limitNotice = $"""<div class="notice error">{H.Encode(limitMessage)}</div>""";

        var plan2 = store.CurrentPlan;
        var totalWeeklyPosts = store.Schedules.Sum(s => s.PostsPerWeek);
        var limitText = plan2?.MaxWeeklyPosts is int cap
            ? $"""<p class="muted small-text">Your {H.Encode(plan2.Name)} plan allows up to <strong>{cap} posts/week</strong> (about {H.Encode(plan2.AiPostsPerMonthText)} AI posts/month). Currently scheduling <strong>{totalWeeklyPosts}</strong>/week.</p>"""
            : """<p class="muted small-text">Your plan includes unlimited AI posts per month.</p>""";

        // ── OAuth connect buttons ─────────────────────────────────────
        var defaultPlatforms = new[] { "Facebook", "X", "Instagram", "LinkedIn", "Pinterest", "TikTok" };
        var platformColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Facebook"] = "#1877F2", ["X"] = "#000000", ["Instagram"] = "#E4405F",
            ["LinkedIn"] = "#0A66C2", ["Pinterest"] = "#E60023", ["TikTok"] = "#000000"
        };

        var connectButtons = new StringBuilder();
        foreach (var p in defaultPlatforms)
        {
            var color = platformColors.TryGetValue(p, out var c) ? c : "#0f766e";
            connectButtons.Append($"""
                <a class="button" href="/social-accounts/connect/{Uri.EscapeDataString(p)}" style="background:{color}">
                    Connect {H.Encode(p)}
                </a>
                """);
        }

        var socialSection = $"""
            <section class="panel">
                <h2>Connected Accounts &amp; Posting Schedule</h2>
                <p class="muted">Connect or add each platform once. Set how many times a week it should post, whether posts need your approval first, and whether to auto-post automatically at the scheduled time &mdash; all in one place.</p>
                <p class="muted small-text">Checking "Auto-post" means BookPromoter AI will publish generated posts to that platform automatically according to its weekly schedule, without you needing to manually copy/paste each one. If "Approval required" is also checked, auto-posting will only send posts you've approved in the Ad Library first.</p>
                {limitText}
                {limitNotice}
                <form method="post" action="/schedule" class="schedule-list">
                    {accountRows}
                    {(store.SocialAccounts.Count > 0 ? """<button class="button" type="submit">Save Posting Schedule</button>""" : "")}
                </form>
                <div class="connect-buttons">
                    {connectButtons}
                </div>
                <p class="muted small-text">Or <a href="#add-manual">add an account manually</a> below.</p>
            </section>
            """;

        // ── Posting Activity Log ───────────────────────────────────────
        var logRows = new StringBuilder();
        foreach (var entry in store.PostingLog.Take(20))
        {
            var statusClass = entry.Success ? "available" : "used";
            var statusText = entry.Success ? "Posted" : "Failed";
            logRows.Append($"""
                <div class="promo-row">
                    <span>{H.Encode(entry.Platform)} &middot; {H.Encode(entry.BookTitle)}</span>
                    <span>{entry.AttemptedAt:MMM d, HH:mm} UTC &middot; {H.Encode(entry.Message)}</span>
                    <span class="status {statusClass}">{statusText}</span>
                </div>
                """);
        }
        if (store.PostingLog.Count == 0)
            logRows.Append("""<p class="muted">No auto-posting activity yet. Enable "Auto-post" above for a connected platform to get started.</p>""");

        var postingLogSection = $"""
            <section class="panel">
                <h2>Posting Activity Log</h2>
                <p class="muted small-text">Shows the most recent auto-posting attempts across all platforms.</p>
                <div class="promo-table">
                    {logRows}
                </div>
            </section>
            """;

        // ── Add / Edit social account form ────────────────────────────
        // Uses the full platform list (same one previously on the
        // Schedule page) grouped by category, plus a custom option.
        var alreadyAdded = store.SocialAccounts.Select(a => a.Platform).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentPlatform = editingAccount?.Platform ?? "";

        var optionsByGroup = SchedulePage.AllPlatforms
            .Where(p => !alreadyAdded.Contains(p.Value) || p.Value.Equals(currentPlatform, StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.Group);

        var isCustom = !string.IsNullOrWhiteSpace(currentPlatform) &&
                       !SchedulePage.AllPlatforms.Any(p => p.Value.Equals(currentPlatform, StringComparison.OrdinalIgnoreCase));

        var options = new StringBuilder();
        options.Append("""<option value="">Choose a platform...</option>""");
        foreach (var group in optionsByGroup)
        {
            options.Append($"""<optgroup label="{H.Encode(group.Key)}">""");
            foreach (var (value, _) in group)
            {
                var sel = value.Equals(currentPlatform, StringComparison.OrdinalIgnoreCase) ? "selected" : "";
                options.Append($"""<option value="{H.Encode(value)}" {sel}>{H.Encode(value)}</option>""");
            }
            options.Append("</optgroup>");
        }
        options.Append($"""<option value="__custom__" {(isCustom ? "selected" : "")}>Other (type your own)...</option>""");

        var customDisplay = isCustom ? "block" : "none";
        var customValue = isCustom ? currentPlatform : "";
        var formTitle = editingAccount is null ? "Add Social Account Manually" : $"Edit Account: {H.Encode(editingAccount.Platform)}";
        var formAction = editingAccount is null ? "/social-accounts" : $"/social-accounts/edit/{editingAccount.Id}";
        var submitLabel = editingAccount is null ? "Add Account" : "Update Account";
        var cancelLink = editingAccount is null ? "" : """<a class="button secondary" href="/my-account">Cancel</a>""";

        var manualForm = $"""
            <section class="panel" id="add-manual">
                <h2>{formTitle}</h2>
                <p class="muted">Note: manually added accounts are not truly connected. Use the connect buttons above for a real OAuth flow. Adding a platform here also adds it to your posting schedule automatically.</p>
                <form method="post" action="{formAction}" class="form">
                    <label>Platform
                        <select name="platform" onchange="toggleCustomPlatform(this)">{options}</select>
                    </label>
                    <label class="custom-platform" style="display:{customDisplay}">Custom platform name
                        <input name="customPlatform" value="{H.Encode(customValue)}" placeholder="e.g. Threads, Bluesky">
                    </label>
                    <label>Display Name <input name="displayName" value="{H.Encode(editingAccount?.DisplayName ?? "")}" placeholder="Author Page"></label>
                    <label>Handle <input name="handle" value="{H.Encode(editingAccount?.Handle ?? "")}" placeholder="yourauthorname" required></label>
                    <div class="form-actions">
                        <button class="button" type="submit">{submitLabel}</button>
                        {cancelLink}
                    </div>
                </form>
            </section>
            """;

        // ── Delete Account ────────────────────────────────────────────
        var deleteSection = """
            <section class="panel owner-settings">
                <h2>Delete Account</h2>
                <p class="muted">This removes your login from BookPromoter AI. This cannot be undone.</p>
                <form method="post" action="/my-account/delete" onsubmit="return confirm('Are you sure you want to delete your account? This cannot be undone.');">
                    <button class="danger-button" type="submit">Delete My Account</button>
                </form>
            </section>
            """;

        var script = """
            <script>
            function toggleCustomPlatform(select) {
                var custom = document.querySelector('.custom-platform');
                custom.style.display = select.value === '__custom__' ? 'block' : 'none';
            }
            </script>
            """;

        return $"""
            {removeAccountForms}

            <section class="hero">
                <div>
                    <p class="eyebrow">My Account</p>
                    <h1>Manage your profile, social accounts, and posting schedule.</h1>
                </div>
            </section>

            {notice}

            {accountSection}
            {socialSection}
            {postingLogSection}
            {manualForm}
            {deleteSection}
            {script}
            """;
    }
}
