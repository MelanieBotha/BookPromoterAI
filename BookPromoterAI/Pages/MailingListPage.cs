using System.Text;
namespace BookPromoterAI;

static class MailingListPage
{
    public static string Render(AppStoreDb store, string notice, string baseUrl, string draftSubject = "", string draftBody = "", int draftBookId = 0, MailingListCampaign? viewedCampaign = null)
    {
        var userCode = store.CurrentUserCode ?? "";
        var signupUrl = string.IsNullOrWhiteSpace(userCode) ? "" : $"{baseUrl}/readers/signup/{Uri.EscapeDataString(userCode)}";

        var rows = new StringBuilder();
        foreach (var sub in store.MailingListSubscribers)
        {
            var displayName = string.IsNullOrWhiteSpace(sub.Name) ? sub.Email : $"{sub.Name} ({sub.Email})";
            rows.Append($"""
                <article class="book-row">
                    <div>
                        <strong>{H.Encode(displayName)}</strong>
                        <p class="muted small-text">Joined {sub.SubscribedAt:MMM d, yyyy} &middot; via {H.Encode(sub.Source)}</p>
                    </div>
                    <form method="post" action="/mailing-list/delete/{sub.Id}">
                        <button class="danger-button small" type="submit">Remove</button>
                    </form>
                </article>
                """);
        }
        if (store.MailingListSubscribers.Count == 0)
            rows.Append("""<p class="muted">No subscribers yet. Add people manually or share your signup link.</p>""");

        var historyRows = new StringBuilder();
        foreach (var campaign in store.MailingListCampaigns)
        {
            var preview = campaign.Body.Length > 80 ? campaign.Body[..80] + "..." : campaign.Body;
            var failNote = campaign.FailedCount > 0 ? $", {campaign.FailedCount} failed" : "";
            var viewActive = viewedCampaign?.Id == campaign.Id ? "button" : "button secondary";
            historyRows.Append($"""
                <div class="promo-row mailing-history-row">
                    <span><strong>{H.Encode(campaign.Subject)}</strong></span>
                    <span>{H.Encode(preview)}</span>
                    <span>{campaign.SentAt:MMM d, yyyy} &middot; {campaign.RecipientCount} sent{failNote}</span>
                    <span><a class="{viewActive} small" href="/mailing-list?view={campaign.Id}#campaign-view">View</a></span>
                </div>
                """);
        }
        if (store.MailingListCampaigns.Count == 0)
            historyRows.Append("""<p class="muted">No emails sent yet.</p>""");

        var signupSection = string.IsNullOrWhiteSpace(signupUrl)
            ? """<p class="muted">Log in to see your public signup link.</p>"""
            : $"""
                <p>Share this link so readers can join your list:</p>
                <p><a href="{H.Encode(signupUrl)}" target="_blank"><strong>{H.Encode(signupUrl)}</strong></a></p>
                <p class="muted small-text">Post it on social media, in your book back matter, or on your website.</p>
                """;

        var hasDraft = !string.IsNullOrWhiteSpace(draftSubject) || !string.IsNullOrWhiteSpace(draftBody);
        var bookField = draftBookId > 0 ? $"""<input type="hidden" name="bookId" value="{draftBookId}">""" : "";

        var viewedSection = viewedCampaign is null ? "" : $"""
            <section class="panel" id="campaign-view">
                <div class="post-card-header">
                    <h2 style="margin:0">Sent Email</h2>
                    <a class="button secondary small" href="/mailing-list">Close</a>
                </div>
                <p class="muted small-text">Sent {viewedCampaign.SentAt:MMMM d, yyyy 'at' HH:mm} UTC &middot; {viewedCampaign.RecipientCount} recipient(s){(viewedCampaign.FailedCount > 0 ? $" &middot; {viewedCampaign.FailedCount} failed" : "")}</p>
                <p><strong>Subject:</strong> {H.Encode(viewedCampaign.Subject)}</p>
                <div class="email-body-view">{H.Encode(viewedCampaign.Body)}</div>
            </section>
            """ + """
            <script>document.getElementById('campaign-view')?.scrollIntoView({ block: 'start' });</script>
            """;

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Mailing List</p>
                    <h1>Build a reader list and email your subscribers.</h1>
                    <p class="muted">Auto-generate reader emails from your books — just like the Ad Library.</p>
                </div>
                <form method="post" action="/mailing-list/generate" class="inline-form">
                    <button class="button" type="submit">Auto-Generate Email</button>
                </form>
            </section>

            {notice}

            <section class="panel">
                <h2>Public Signup Link</h2>
                {signupSection}
            </section>

            <section class="split">
                <form method="post" action="/mailing-list/subscribers" class="panel form">
                    <h2>Add Subscriber</h2>
                    <p class="muted">Manually add someone who asked to join your list.</p>
                    <label>Name (optional) <input name="name" placeholder="Jane Reader"></label>
                    <label>Email <input name="email" type="email" required placeholder="reader@example.com"></label>
                    <button class="button" type="submit">Add to List</button>
                </form>
                <section class="panel">
                    <h2>Subscribers ({store.MailingListSubscribers.Count})</h2>
                    {rows}
                </section>
            </section>

            <section class="panel" id="compose-email">
                <h2>Send Email to List</h2>
                <p class="muted">{(store.MailingListSubscribers.Count == 0 ? "<strong>Add subscribers first.</strong> " : $"Ready to reach {store.MailingListSubscribers.Count} subscriber(s). ")}Use Auto-Generate to draft a promotion from your books, edit if needed, then send.</p>
                <form method="post" action="/mailing-list/send" class="form" onsubmit="return confirm('Send this email to all {store.MailingListSubscribers.Count} subscriber(s)?');">
                    {bookField}
                    <label>Subject <input name="subject" required placeholder="New book announcement" value="{H.Encode(draftSubject)}"></label>
                    <label>Message
                        <textarea name="body" required placeholder="Hi readers,&#10;&#10;I wanted to share...">{H.Encode(draftBody)}</textarea>
                    </label>
                    <button class="button" type="submit" {(store.MailingListSubscribers.Count == 0 ? "disabled" : "")}>Send to All Subscribers</button>
                </form>
                {(hasDraft ? $"""<form method="post" action="/mailing-list/regenerate" class="inline-form" style="margin-top:12px">{bookField}<button class="button secondary" type="submit">Regenerate Draft</button></form>""" : "")}
            </section>

            {viewedSection}

            <section class="panel">
                <h2>Send History</h2>
                <div class="promo-table">
                    <div class="promo-header mailing-history-header">
                        <strong>Subject</strong>
                        <strong>Preview</strong>
                        <strong>Sent</strong>
                        <strong></strong>
                    </div>
                    {historyRows}
                </div>
            </section>
            {(hasDraft ? """<script>document.getElementById('compose-email')?.scrollIntoView({ block: 'start' });</script>""" : "")}
            """;
    }

    public static string SignupPage(string userCode, string authorEmail, string notice)
    {
        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Reader Signup</p>
                    <h1>Join the mailing list</h1>
                    <p class="muted">Get book updates and news from this author.</p>
                </div>
            </section>

            {notice}

            <section class="panel form" style="max-width:480px">
                <form method="post" action="/readers/signup/{Uri.EscapeDataString(userCode)}">
                    <label>Your name (optional) <input name="name" placeholder="Your name"></label>
                    <label>Email address <input name="email" type="email" required placeholder="you@example.com"></label>
                    <button class="button" type="submit">Subscribe</button>
                </form>
                {(string.IsNullOrWhiteSpace(authorEmail) ? "" : """<p class="muted small-text">List managed via BookPromoter AI.</p>""")}
            </section>
            """;
    }
}
