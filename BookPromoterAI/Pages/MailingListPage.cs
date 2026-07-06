using System.Text;
namespace BookPromoterAI;

static class MailingListPage
{
    public static string Render(AppStoreDb store, string notice, string baseUrl, string draftSubject = "", string draftBody = "", int draftBookId = 0, MailingListCampaign? viewedCampaign = null)
    {
        var userCode = store.CurrentUserCode ?? "";
        var signupUrl = string.IsNullOrWhiteSpace(userCode) ? "" : $"{baseUrl}/readers/signup/{Uri.EscapeDataString(userCode)}";
        var settings = store.MailingListSettings;
        var effectiveSubject = !string.IsNullOrWhiteSpace(draftSubject) ? draftSubject : settings.PendingSubject;
        var effectiveBody = !string.IsNullOrWhiteSpace(draftBody) ? draftBody : settings.PendingBody;
        var effectiveBookId = draftBookId > 0 ? draftBookId : settings.PendingBookId ?? 0;
        var mySubscriptions = store.GetMailingListSubscriptionsForLoggedInUser();
        var subscriptionRows = new StringBuilder();
        foreach (var sub in mySubscriptions)
        {
            var ownerLabel = !string.IsNullOrWhiteSpace(sub.ListOwnerDisplayName)
                ? sub.ListOwnerDisplayName
                : OwnerAccount.IsOwnerEmail(sub.ListOwnerEmail) && MailingListKinds.IsBrand(sub.ListKind)
                    ? "BookPromoter AI product updates"
                    : OwnerAccount.IsOwnerEmail(sub.ListOwnerEmail)
                        ? "BookPromoter AI reader list"
                        : "Author mailing list";
            subscriptionRows.Append($"""
                <article class="book-row">
                    <div>
                        <strong>{H.Encode(ownerLabel)}</strong>
                        <p class="muted small-text">Subscribed {sub.SubscribedAt:MMM d, yyyy} &middot; via {H.Encode(sub.Source)}</p>
                    </div>
                    <form method="post" action="/mailing-list/unsubscribe/{sub.Id}" onsubmit="return confirm('Unsubscribe from {H.Encode(ownerLabel)}?');">
                        <button class="button secondary small" type="submit">Unsubscribe</button>
                    </form>
                </article>
                """);
        }
        var subscriptionsSection = mySubscriptions.Count == 0
            ? ""
            : $"""
                <section class="panel">
                    <h2>Your Email Preferences</h2>
                    <p class="muted">Lists you're subscribed to with <strong>{H.Encode(store.LoggedInEmail ?? "")}</strong>. Unsubscribe anytime if you don't want updates.</p>
                    {subscriptionRows}
                </section>
                """;

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

        var hasDraft = !string.IsNullOrWhiteSpace(effectiveSubject) || !string.IsNullOrWhiteSpace(effectiveBody);
        var bookField = effectiveBookId > 0 ? $"""<input type="hidden" name="bookId" value="{effectiveBookId}">""" : "";
        var draftBook = effectiveBookId > 0 ? store.Books.FirstOrDefault(b => b.Id == effectiveBookId) : null;
        var coverNote = draftBook is not null && !string.IsNullOrWhiteSpace(draftBook.CoverImageUrl)
            ? """<p class="muted small-text">The novel cover will appear at the top of the sent email.</p>"""
            : "";
        var featuringNote = draftBook is not null
            ? $"""<p class="muted small-text">This week's featured book: <strong>{H.Encode(draftBook.Title)}</strong>{(store.Books.Count > 1 ? " — rotates to the next novel each week" : "")}</p>"""
            : store.Books.Count > 0
                ? """<p class="muted small-text">One book is featured per week and auto-emailed to readers. New books trigger an immediate new-release announcement.</p>"""
                : "";
        var refreshDraftButton = "Refresh Draft";

        var autoSendChecked = settings.AutoSendEnabled ? "checked" : "";
        var requiresApprovalChecked = settings.RequiresApproval ? "checked" : "";
        var autoHint = AppStoreDb.FormatNextMailingHint(settings) is string hint
            ? $"""<p class="muted small-text">{H.Encode(hint)}</p>"""
            : "";
        var sendGridNote = store.IsSendGridConfigured
            ? ""
            : """<p class="notice error">SendGrid is not configured — auto-send will log emails in dev mode but won't deliver until SendGrid is set up.</p>""";
        var pendingApproval = settings.RequiresApproval
            && !settings.PendingApproved
            && !string.IsNullOrWhiteSpace(settings.PendingSubject);
        var approveSection = pendingApproval
            ? """
                <p class="notice">A draft is ready. Approve it to allow auto-send, or edit the message below and send manually.</p>
                <form method="post" action="/mailing-list/approve" class="inline-form" style="margin-top:8px">
                    <button class="button secondary" type="submit">Approve draft for auto-send</button>
                </form>
                """
            : settings.PendingApproved && settings.AutoSendEnabled && !string.IsNullOrWhiteSpace(settings.PendingSubject)
                ? """<p class="notice success">Draft approved — auto-send will use this message on the next scheduled slot.</p>"""
                : "";

        var viewedSection = viewedCampaign is null ? "" : $"""
            <section class="panel" id="campaign-view">
                <div class="post-card-header">
                    <h2 style="margin:0">Sent Email</h2>
                    <a class="button secondary small" href="/mailing-list">Close</a>
                </div>
                <p class="muted small-text">Sent {AppTimeZone.FormatWithZone(viewedCampaign.SentAt, "MMMM d, yyyy 'at' HH:mm")} &middot; {viewedCampaign.RecipientCount} recipient(s){(viewedCampaign.FailedCount > 0 ? $" &middot; {viewedCampaign.FailedCount} failed" : "")}</p>
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
                    <p class="muted">One featured novel per week, auto-sent to your readers. Add a new book and readers get an immediate new-release announcement.{(store.IsOwner ? " <strong>Registered user emails</strong> (product updates) are on the <a href=\"/owner-promos\">Owner</a> page." : "")}</p>
                </div>
                <form method="post" action="/mailing-list/generate" class="inline-form">
                    <button class="button" type="submit">Preview This Week's Email</button>
                </form>
            </section>

            {notice}

            {subscriptionsSection}

            <section class="panel">
                <h2>Weekly auto-send</h2>
                <p class="muted">Each week, one of your books is featured and emailed to <strong>your readers</strong>. Books rotate automatically. When you add a new book, readers get a <strong>new-release</strong> email right away.</p>
                {sendGridNote}
                <form method="post" action="/mailing-list/schedule" class="form">
                    <label class="checkbox">
                        <input name="autoSendEnabled" type="checkbox" {autoSendChecked}>
                        Auto-send one featured book per week
                    </label>
                    <label class="checkbox">
                        <input name="requiresApproval" type="checkbox" {requiresApprovalChecked}>
                        Approval required before weekly send
                    </label>
                    <input type="hidden" name="emailsPerWeek" value="1">
                    <button class="button" type="submit">Save</button>
                </form>
                {autoHint}
                {approveSection}
            </section>

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
                <p class="muted">{(store.MailingListSubscribers.Count == 0 ? "<strong>Add subscribers first.</strong> " : $"Ready to reach {store.MailingListSubscribers.Count} subscriber(s). ")}Use Auto-Generate to draft a promotion from your books, edit if needed, then send. Manual sends are separate from weekly auto-send.</p>
                <form method="post" action="/mailing-list/send" class="form" onsubmit="return confirm('Send this email to all {store.MailingListSubscribers.Count} subscriber(s)?');">
                    {bookField}
                    <label>Subject <input name="subject" required placeholder="New book announcement" value="{H.Encode(effectiveSubject)}"></label>
                    <label>Message
                        <textarea name="body" required placeholder="Hi readers,&#10;&#10;I wanted to share...">{H.Encode(effectiveBody)}</textarea>
                    </label>
                    {featuringNote}
                    {coverNote}
                    <button class="button" type="submit" {(store.MailingListSubscribers.Count == 0 ? "disabled" : "")}>Send to All Subscribers</button>
                </form>
                {(hasDraft ? $"""<form method="post" action="/mailing-list/regenerate" class="inline-form" style="margin-top:12px">{bookField}<button class="button secondary" type="submit">{refreshDraftButton}</button></form>""" : "")}
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

    public static string SignupPage(string userCode, string authorName, string notice)
    {
        var heading = string.IsNullOrWhiteSpace(authorName) || authorName == AuthorDisplayName.Fallback
            ? "Join the mailing list"
            : $"Join {H.Encode(authorName)}'s mailing list";
        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Reader Signup</p>
                    <h1>{heading}</h1>
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
                <p class="muted small-text">You can unsubscribe anytime from the link in any email, or from Mailing List after you log in.</p>
                <p class="muted small-text">List managed via BookPromoter AI.</p>
            </section>
            """;
    }

    public static string UnsubscribePage(string token, string notice, AppStoreDb store, bool unsubscribed = false)
    {
        var actionSection = unsubscribed
            ? """<p><a class="button secondary" href="/">Return to home</a></p>"""
            : $"""
                <form method="post" action="/readers/unsubscribe/{Uri.EscapeDataString(token)}" class="form" style="max-width:480px">
                    <p class="muted">Click below to stop receiving emails from this list. You can always re-subscribe later using the author's signup link.</p>
                    <button class="button secondary" type="submit">Unsubscribe</button>
                </form>
                <p class="muted small-text"><a href="/">Keep receiving updates</a></p>
                """;

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Email preferences</p>
                    <h1>{(unsubscribed ? "You're unsubscribed" : "Unsubscribe from this list")}</h1>
                    <p class="muted">{(unsubscribed ? "You won't receive further emails from this mailing list." : "Choose whether to keep receiving updates from this author.")}</p>
                </div>
            </section>

            {notice}

            <section class="panel">
                {actionSection}
            </section>
            """;
    }
}
