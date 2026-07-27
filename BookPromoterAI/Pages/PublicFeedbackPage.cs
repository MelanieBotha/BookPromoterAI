using System.Text;
namespace BookPromoterAI;

static class PublicFeedbackPage
{
    public static string Render(
        AppStoreDb store,
        string tab,
        string notice = "",
        ForumThread? openThread = null,
        List<ForumPost>? openPosts = null)
    {
        var activeTab = string.Equals(tab, "forum", StringComparison.OrdinalIgnoreCase) ? "forum" : "feedback";
        var feedbackTabClass = activeTab == "feedback" ? "public-tab active" : "public-tab";
        var forumTabClass = activeTab == "forum" ? "public-tab active" : "public-tab";

        var feedbackPane = activeTab == "feedback" ? RenderFeedbackTab(store) : "";
        var forumPane = activeTab == "forum"
            ? (openThread is not null
                ? RenderThreadDetail(store, openThread, openPosts ?? [])
                : RenderForumList(store))
            : "";

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Public</p>
                    <h1>App feedback &amp; forum</h1>
                    <p class="muted">See what authors are saying about BookPromoter AI, and join the conversation — no purchase required. Browse freely; log in to post in the forum.</p>
                </div>
            </section>

            {notice}

            <nav class="public-tabs" aria-label="Feedback sections">
                <a class="{feedbackTabClass}" href="/app-feedback?tab=feedback">Feedback</a>
                <a class="{forumTabClass}" href="/app-feedback?tab=forum">Forum</a>
            </nav>

            {feedbackPane}
            {forumPane}
            {PageStyles}
            """;
    }

    const string PageStyles = """
        <style>
        .public-tabs{display:flex;gap:8px;margin:0 0 20px;flex-wrap:wrap}
        .public-tab{display:inline-block;padding:10px 18px;border:1px solid var(--line);border-radius:8px;text-decoration:none;color:var(--ink);font-weight:600;background:var(--paper)}
        .public-tab.active{background:var(--accent);color:#fff;border-color:var(--accent)}
        .public-feedback-card,.forum-thread-card,.forum-post-card{border:1px solid var(--line);border-radius:10px;padding:14px 16px;margin-bottom:12px;background:var(--paper)}
        .forum-thread-card a{color:var(--ink);text-decoration:none;font-weight:700}
        .forum-thread-card a:hover{color:var(--accent)}
        .forum-meta,.feedback-meta{color:var(--muted);font-size:13px;margin-top:6px}
        .owner-badge{display:inline-block;margin-left:6px;padding:2px 8px;border-radius:999px;background:#0f766e;color:#fff;font-size:11px;font-weight:700}
        .reviewed-badge{display:inline-block;margin-left:6px;padding:2px 8px;border-radius:999px;background:#166534;color:#fff;font-size:11px;font-weight:700}
        </style>
        """;

    static string RenderFeedbackTab(AppStoreDb store)
    {
        var entries = store.PublicGeneralFeedbackEntries;
        var cards = new StringBuilder();
        if (entries.Count == 0)
        {
            cards.Append("""<p class="muted">No general feedback shared yet. Be the first — signed-in authors can send feedback from the app, or use the form below.</p>""");
        }
        else
        {
            foreach (var entry in entries)
            {
                var reviewed = entry.Investigated
                    ? """<span class="reviewed-badge">Reviewed</span>"""
                    : "";
                cards.Append($"""
                    <article class="public-feedback-card">
                        <p>{H.Encode(entry.Message)}</p>
                        <p class="feedback-meta">{H.Encode(AppStoreDb.MaskEmailForPublic(entry.Email))} &middot; {AppTimeZone.FormatWithZone(entry.SubmittedAt, "MMM d, yyyy")}{reviewed}</p>
                    </article>
                    """);
            }
        }

        var submitForm = store.IsLoggedIn
            ? $"""
                <form method="post" action="/app-feedback/submit" class="panel form" style="margin-top:20px">
                    <h2>Share general feedback</h2>
                    <p class="muted small-text">Posted here for everyone to read (your email is masked). Bugs and private details? Use <a href="/feedback">in-app Feedback</a> instead.</p>
                    <input type="hidden" name="category" value="General Feedback">
                    <label>Your email
                        <input name="email" type="email" value="{H.Encode(store.LoggedInEmail ?? "")}" required>
                    </label>
                    <label>Message
                        <textarea name="message" rows="4" required placeholder="What do you like, or what should we improve?"></textarea>
                    </label>
                    <button class="button" type="submit">Post feedback</button>
                </form>
                """
            : """
                <section class="panel" style="margin-top:20px">
                    <h2>Share feedback</h2>
                    <p class="muted"><a href="/start">Create a free account</a> or <a href="/start">log in</a> to post general feedback here. You can also browse the forum without posting.</p>
                </section>
                """;

        return $"""
            <section class="panel">
                <h2>General feedback</h2>
                <p class="muted small-text">Public comments from the General Feedback category. Emails are masked for privacy.</p>
                {cards}
            </section>
            {submitForm}
            """;
    }

    static string RenderForumList(AppStoreDb store)
    {
        var threads = store.ListForumThreads();
        var list = new StringBuilder();
        if (threads.Count == 0)
            list.Append("""<p class="muted">No forum threads yet. Start one — the owner reads and replies here too.</p>""");

        foreach (var t in threads)
        {
            var last = t.LastPostAt ?? t.UpdatedAt;
            list.Append($"""
                <article class="forum-thread-card">
                    <a href="/app-feedback/thread/{t.Id}">{H.Encode(t.Title)}</a>
                    <p class="forum-meta">Started by {H.Encode(t.AuthorDisplayName)} &middot; {t.ReplyCount} repl{(t.ReplyCount == 1 ? "y" : "ies")} &middot; Updated {AppTimeZone.FormatWithZone(last, "MMM d, HH:mm")}</p>
                </article>
                """);
        }

        var newThread = store.IsLoggedIn
            ? """
                <form method="post" action="/app-feedback/forum/thread" class="panel form" style="margin-top:20px">
                    <h2>Start a thread</h2>
                    <p class="muted small-text">Ask questions, share tips, or talk with the owner and other authors.</p>
                    <label>Title
                        <input name="title" maxlength="120" required placeholder="e.g. Tips for Tumblr auto-post">
                    </label>
                    <label>Opening message
                        <textarea name="body" rows="4" required maxlength="4000"></textarea>
                    </label>
                    <button class="button" type="submit">Post thread</button>
                </form>
                """
            : """
                <section class="panel" style="margin-top:20px">
                    <h2>Join the forum</h2>
                    <p class="muted"><a href="/start">Log in or sign up</a> to start a thread or reply. Everyone can read without an account.</p>
                </section>
                """;

        return $"""
            <section class="panel">
                <h2>Community forum</h2>
                <p class="muted small-text">Authors and the BookPromoter AI owner can talk here. Be respectful — the owner can remove posts.</p>
                {list}
            </section>
            {newThread}
            """;
    }

    static string RenderThreadDetail(AppStoreDb store, ForumThread thread, List<ForumPost> posts)
    {
        var postCards = new StringBuilder();
        foreach (var post in posts)
        {
            var ownerBadge = post.IsOwnerReply ? """<span class="owner-badge">Owner</span>""" : "";
            var removeBtn = store.IsOwner
                ? $"""<form method="post" action="/app-feedback/forum/post/{post.Id}/remove" class="inline-form tight" style="margin-top:8px"><button class="danger-button small" type="submit">Remove</button></form>"""
                : "";
            postCards.Append($"""
                <article class="forum-post-card">
                    <strong>{H.Encode(post.AuthorDisplayName)}</strong>{ownerBadge}
                    <p class="forum-meta">{AppTimeZone.FormatWithZone(post.CreatedAt, "MMM d, yyyy HH:mm")}</p>
                    <p style="margin-top:10px;white-space:pre-wrap">{H.Encode(post.Body)}</p>
                    {removeBtn}
                </article>
                """);
        }

        var replyForm = store.IsLoggedIn
            ? $"""
                <form method="post" action="/app-feedback/forum/thread/{thread.Id}/reply" class="panel form" style="margin-top:16px">
                    <h2>Reply</h2>
                    <label>Message
                        <textarea name="body" rows="4" required maxlength="4000"></textarea>
                    </label>
                    <button class="button" type="submit">Post reply</button>
                </form>
                """
            : """
                <section class="panel" style="margin-top:16px">
                    <p class="muted"><a href="/start">Log in</a> to reply to this thread.</p>
                </section>
                """;

        var removeThread = store.IsOwner
            ? $"""<form method="post" action="/app-feedback/forum/thread/{thread.Id}/remove" class="inline-form" style="margin-top:12px" onsubmit="return confirm('Remove this entire thread?');"><button class="danger-button small" type="submit">Remove thread</button></form>"""
            : "";

        return $"""
            <p><a href="/app-feedback?tab=forum">&larr; All forum threads</a></p>
            <section class="panel">
                <h2>{H.Encode(thread.Title)}</h2>
                <p class="forum-meta">Started by {H.Encode(thread.AuthorDisplayName)} &middot; {AppTimeZone.FormatWithZone(thread.CreatedAt, "MMM d, yyyy")}</p>
                {removeThread}
            </section>
            {postCards}
            {replyForm}
            """;
    }
}
