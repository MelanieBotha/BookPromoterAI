using System.Text;
namespace BookPromoterAI;

static class TeamPage
{
    public static string Render(AppStoreDb store, string notice)
    {
        var rows = new StringBuilder();
        foreach (var member in store.TeamMembers)
        {
            var statusBadge = member.Accepted
                ? """<span class="status available">Accepted</span>"""
                : $"""<span class="status used">Pending &mdash; Code: <strong>{H.Encode(member.InviteCode)}</strong></span>""";

            rows.Append($"""
                <article class="book-row">
                    <div>
                        <strong>{H.Encode(member.Email)}</strong>
                        <p>{H.Encode(member.Role)} &middot; Invited {member.InvitedAt:MMM d, yyyy}</p>
                        {statusBadge}
                    </div>
                    <form method="post" action="/team/remove/{Uri.EscapeDataString(member.Email)}">
                        <button class="danger-button small" type="submit">Remove</button>
                    </form>
                </article>
                """);
        }
        if (store.TeamMembers.Count == 0)
            rows.Append("""<p class="muted">No team members invited yet.</p>""");

        var lockedNotice = !store.HasTeamAccess
            ? $"""<div class="notice error">Team invitations require the Publisher or Agency plan. You're currently on <strong>{H.Encode(store.CurrentPlan?.Name ?? store.AccessType)}</strong>. <a href="/billing">Upgrade your plan</a> to invite real team members.</div>"""
            : "";

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Team Access</p>
                    <h1>Invite collaborators to help manage your books and posts.</h1>
                </div>
            </section>

            {notice}
            {lockedNotice}

            <section class="split">
                <form method="post" action="/team/invite" class="panel form">
                    <h1>Invite Team Member</h1>
                    <label>Email <input name="email" type="email" required></label>
                    <label>Role
                        <select name="role">
                            <option>Editor</option>
                            <option>Viewer</option>
                            <option>Admin</option>
                        </select>
                    </label>
                    <button class="button" type="submit">Send Invite</button>
                </form>
                <section class="panel">
                    <h1>Team Members</h1>
                    {rows}
                </section>
            </section>
            """;
    }
}
