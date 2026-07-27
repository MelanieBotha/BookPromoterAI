using System.Text;
namespace BookPromoterAI;

static class PublicFeedbackPage
{
    public static string Render(
        AppStoreDb store,
        string tab,
        string notice = "",
        ForumThread? openThread = null,
        List<ForumPost>? openPosts = null,
        string forumSort = "latest",
        string forumCategory = "all")
    {
        var activeTab = string.Equals(tab, "forum", StringComparison.OrdinalIgnoreCase) ? "forum" : "reviews";
        var reviewsClass = activeTab == "reviews" ? "public-tab active" : "public-tab";
        var forumClass = activeTab == "forum" ? "public-tab active" : "public-tab";

        var reviewsPane = activeTab == "reviews" ? RenderReviewsTab(store) : "";
        var forumPane = activeTab == "forum"
            ? (openThread is not null
                ? RenderThreadDetail(store, openThread, openPosts ?? [])
                : RenderForumList(store, forumSort, forumCategory))
            : "";

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Community</p>
                    <h1>Reviews &amp; forum</h1>
                    <p class="muted">Read what authors say about BookPromoter AI, leave your own review, and join the forum — no purchase required to browse.</p>
                </div>
            </section>

            {notice}

            <nav class="public-tabs" aria-label="Community sections">
                <a class="{reviewsClass}" href="/app-feedback?tab=reviews">Reviews</a>
                <a class="{forumClass}" href="/app-feedback?tab=forum">Forum</a>
            </nav>

            {reviewsPane}
            {forumPane}
            {PageStyles}
            """;
    }

    const string PageStyles = """
        <style>
        .public-tabs{display:flex;gap:8px;margin:0 0 20px;flex-wrap:wrap}
        .public-tab{display:inline-block;padding:10px 18px;border:1px solid var(--line);border-radius:8px;text-decoration:none;color:var(--ink);font-weight:600;background:var(--paper)}
        .public-tab.active{background:var(--accent);color:#fff;border-color:var(--accent)}
        .review-card,.forum-post-card{border:1px solid var(--line);border-radius:10px;padding:14px 16px;margin-bottom:12px;background:var(--paper)}
        .review-stars{color:#ca8a04;letter-spacing:2px;font-size:16px;font-weight:700}
        .review-meta,.forum-meta{color:var(--muted);font-size:13px;margin-top:6px}
        .review-summary{display:flex;gap:18px;flex-wrap:wrap;align-items:baseline;margin-bottom:16px}
        .review-summary .avg{font-size:28px;font-weight:800;color:var(--ink)}
        .owner-badge{display:inline-block;margin-left:6px;padding:2px 8px;border-radius:999px;background:#0f766e;color:#fff;font-size:11px;font-weight:700}
        .forum-toolbar{display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin-bottom:14px}
        .forum-sort a{margin-right:14px;text-decoration:none;color:var(--muted);font-weight:600;padding-bottom:4px}
        .forum-sort a.active{color:var(--accent);border-bottom:2px solid var(--accent)}
        .forum-table{width:100%;border-collapse:collapse}
        .forum-table th{text-align:left;font-size:12px;color:var(--muted);font-weight:600;padding:8px 10px;border-bottom:1px solid var(--line)}
        .forum-table td{padding:14px 10px;border-bottom:1px solid var(--line);vertical-align:middle}
        .forum-table tr:hover td{background:#fafafa}
        .forum-topic a{color:var(--ink);text-decoration:none;font-weight:700;font-size:15px}
        .forum-topic a:hover{color:var(--accent)}
        .forum-cat{display:inline-flex;align-items:center;gap:6px;margin-top:6px;font-size:12px;color:var(--muted);font-weight:600;text-transform:lowercase}
        .forum-cat-dot{width:10px;height:10px;border-radius:2px;display:inline-block}
        .forum-cat-dot.ideas{background:#3b82f6}
        .forum-cat-dot.bugs{background:#f97316}
        .forum-cat-dot.help{background:#8b5cf6}
        .forum-cat-dot.general{background:#94a3b8}
        .forum-num{text-align:center;color:var(--muted);font-size:13px;white-space:nowrap}
        .forum-activity{text-align:right;color:var(--muted);font-size:13px;white-space:nowrap}
        .forum-avatar{width:28px;height:28px;border-radius:50%;background:#e2e8f0;color:#334155;display:inline-flex;align-items:center;justify-content:center;font-size:12px;font-weight:700}
        @media (max-width:700px){
          .forum-table .col-views,.forum-table .col-avatar{display:none}
        }
        </style>
        """;

    static string RenderReviewsTab(AppStoreDb store)
    {
        var reviews = store.ListAppReviews();
        var (avg, count) = store.AppReviewSummary();
        var cards = new StringBuilder();
        if (reviews.Count == 0)
        {
            cards.Append("""<p class="muted">No reviews yet. Be the first to share how BookPromoter AI works for you.</p>""");
        }
        else
        {
            foreach (var review in reviews)
            {
                var remove = store.IsOwner
                    ? $"""<form method="post" action="/app-feedback/review/{review.Id}/remove" class="inline-form tight" style="margin-top:8px"><button class="danger-button small" type="submit">Remove</button></form>"""
                    : "";
                cards.Append($"""
                    <article class="review-card">
                        <div class="review-stars">{Stars(review.Rating)}</div>
                        <p style="margin-top:8px;white-space:pre-wrap">{H.Encode(review.Body)}</p>
                        <p class="review-meta">{H.Encode(review.AuthorDisplayName)} &middot; {AppTimeZone.FormatWithZone(review.CreatedAt, "MMM d, yyyy")}</p>
                        {remove}
                    </article>
                    """);
            }
        }

        var summary = count > 0
            ? $"""<div class="review-summary"><span class="avg">{avg:0.#}</span><span class="review-stars">{Stars((int)Math.Round(avg))}</span><span class="muted">{count} review{(count == 1 ? "" : "s")}</span></div>"""
            : "";

        var form = store.IsLoggedIn
            ? """
                <form method="post" action="/app-feedback/review" class="panel form" style="margin-top:20px">
                    <h2>Add your review</h2>
                    <p class="muted small-text">One review per account — submitting again updates your existing review.</p>
                    <label>Rating
                        <select name="rating" required>
                            <option value="5">5 — Excellent</option>
                            <option value="4">4 — Good</option>
                            <option value="3">3 — Okay</option>
                            <option value="2">2 — Poor</option>
                            <option value="1">1 — Bad</option>
                        </select>
                    </label>
                    <label>Your review
                        <textarea name="body" rows="4" maxlength="2000" required placeholder="What helped you promote your books?"></textarea>
                    </label>
                    <button class="button" type="submit">Submit review</button>
                </form>
                """
            : """
                <section class="panel" style="margin-top:20px">
                    <h2>Add a review</h2>
                    <p class="muted"><a href="/start">Log in or create a free account</a> to leave a star rating and review. Everyone can read reviews without signing up.</p>
                </section>
                """;

        return $"""
            <section class="panel">
                <h2>Author reviews</h2>
                <p class="muted small-text">Public ratings from BookPromoter AI users.</p>
                {summary}
                {cards}
            </section>
            {form}
            """;
    }

    static string Stars(int rating)
    {
        rating = Math.Clamp(rating, 0, 5);
        return new string('★', rating) + new string('☆', 5 - rating);
    }

    static string RenderForumList(AppStoreDb store, string sort, string category)
    {
        sort = sort.Equals("top", StringComparison.OrdinalIgnoreCase) ? "top" : "latest";
        category = string.IsNullOrWhiteSpace(category) ? "all" : category.Trim().ToLowerInvariant();
        var threads = store.ListForumThreads(sort, category);

        var latestClass = sort == "latest" ? "active" : "";
        var topClass = sort == "top" ? "active" : "";
        var catQuery = category == "all" ? "" : $"&category={Uri.EscapeDataString(category)}";

        var rows = new StringBuilder();
        if (threads.Count == 0)
        {
            rows.Append("""<tr><td colspan="4" class="muted" style="padding:20px 10px">No topics yet. Start a thread below.</td></tr>""");
        }
        else
        {
            foreach (var t in threads)
            {
                var cat = AppStoreDb.FormatForumCategory(t.Category);
                var activity = AppStoreDb.FormatRelativeActivity(t.LastPostAt ?? t.UpdatedAt);
                var initial = string.IsNullOrWhiteSpace(t.AuthorDisplayName) ? "?" : char.ToUpperInvariant(t.AuthorDisplayName[0]).ToString();
                rows.Append($"""
                    <tr>
                        <td class="forum-topic">
                            <a href="/app-feedback/thread/{t.Id}">{H.Encode(t.Title)}</a>
                            <div class="forum-cat"><span class="forum-cat-dot {H.Encode(cat)}"></span>{H.Encode(cat)}</div>
                        </td>
                        <td class="forum-num col-avatar"><span class="forum-avatar" title="{H.Encode(t.AuthorDisplayName)}">{H.Encode(initial)}</span></td>
                        <td class="forum-num">{t.ReplyCount}</td>
                        <td class="forum-num col-views">{FormatViews(t.ViewCount)}</td>
                        <td class="forum-activity">{H.Encode(activity)}</td>
                    </tr>
                    """);
            }
        }

        var newThread = store.IsLoggedIn
            ? """
                <form method="post" action="/app-feedback/forum/thread" class="panel form" style="margin-top:20px">
                    <h2>Start a topic</h2>
                    <label>Category
                        <select name="category">
                            <option value="general">general</option>
                            <option value="ideas">ideas</option>
                            <option value="bugs">bugs</option>
                            <option value="help">help</option>
                        </select>
                    </label>
                    <label>Title
                        <input name="title" maxlength="120" required placeholder="e.g. Tips for Tumblr auto-post">
                    </label>
                    <label>Opening message
                        <textarea name="body" rows="4" required maxlength="4000"></textarea>
                    </label>
                    <button class="button" type="submit">Post topic</button>
                </form>
                """
            : """
                <section class="panel" style="margin-top:20px">
                    <h2>Join the forum</h2>
                    <p class="muted"><a href="/start">Log in or sign up</a> to start a topic or reply. Everyone can read without an account.</p>
                </section>
                """;

        return $"""
            <section class="panel">
                <div class="forum-toolbar">
                    <div class="forum-sort">
                        <a class="{latestClass}" href="/app-feedback?tab=forum&amp;sort=latest{catQuery}">Latest</a>
                        <a class="{topClass}" href="/app-feedback?tab=forum&amp;sort=top{catQuery}">Top</a>
                    </div>
                    <form method="get" action="/app-feedback" class="inline-form tight">
                        <input type="hidden" name="tab" value="forum">
                        <input type="hidden" name="sort" value="{H.Encode(sort)}">
                        <label class="muted small-text">categories
                            <select name="category" onchange="this.form.submit()">
                                <option value="all" {(category == "all" ? "selected" : "")}>all</option>
                                <option value="general" {(category == "general" ? "selected" : "")}>general</option>
                                <option value="ideas" {(category == "ideas" ? "selected" : "")}>ideas</option>
                                <option value="bugs" {(category == "bugs" ? "selected" : "")}>bugs</option>
                                <option value="help" {(category == "help" ? "selected" : "")}>help</option>
                            </select>
                        </label>
                    </form>
                </div>
                <div style="overflow-x:auto">
                    <table class="forum-table">
                        <thead>
                            <tr>
                                <th>Topic</th>
                                <th class="forum-num col-avatar"></th>
                                <th class="forum-num">Replies</th>
                                <th class="forum-num col-views">Views</th>
                                <th class="forum-activity">Activity</th>
                            </tr>
                        </thead>
                        <tbody>
                            {rows}
                        </tbody>
                    </table>
                </div>
            </section>
            {newThread}
            """;
    }

    static string FormatViews(int views)
    {
        if (views >= 1000) return $"{views / 1000.0:0.#}k";
        return views.ToString();
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
                    <p class="muted"><a href="/start">Log in</a> to reply to this topic.</p>
                </section>
                """;

        var removeThread = store.IsOwner
            ? $"""<form method="post" action="/app-feedback/forum/thread/{thread.Id}/remove" class="inline-form" style="margin-top:12px" onsubmit="return confirm('Remove this entire topic?');"><button class="danger-button small" type="submit">Remove topic</button></form>"""
            : "";

        var cat = AppStoreDb.FormatForumCategory(thread.Category);

        return $"""
            <p><a href="/app-feedback?tab=forum">&larr; All topics</a></p>
            <section class="panel">
                <h2>{H.Encode(thread.Title)}</h2>
                <div class="forum-cat"><span class="forum-cat-dot {H.Encode(cat)}"></span>{H.Encode(cat)}</div>
                <p class="forum-meta">Started by {H.Encode(thread.AuthorDisplayName)} &middot; {AppTimeZone.FormatWithZone(thread.CreatedAt, "MMM d, yyyy")} &middot; {thread.ViewCount} view{(thread.ViewCount == 1 ? "" : "s")}</p>
                {removeThread}
            </section>
            {postCards}
            {replyForm}
            """;
    }
}
