namespace BookPromoterAI;

public sealed class HelpGuideStep
{
    public required string Path { get; init; }
    public required string NavLabel { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string[] Instructions { get; init; }
    public required string UpNext { get; init; }
}

static class HelpGuide
{
    static readonly HelpGuideStep[] Steps =
    [
        new()
        {
            Path = "/dashboard",
            NavLabel = "Dashboard",
            Title = "Dashboard",
            Summary = "Your home base — see click stats, recent books, and quick actions to regenerate posts.",
            Instructions =
            [
                "Review click counts for each book at the top.",
                "Scroll to your book cards and use Regenerate post for a fresh caption.",
                "Use the tracking links on Books when sharing outside the app."
            ],
            UpNext = "Add or edit your books with covers and store links."
        },
        new()
        {
            Path = "/books",
            NavLabel = "Books",
            Title = "Books",
            Summary = "Build your catalog — every post and ad is generated from the books you add here.",
            Instructions =
            [
                "Click Add Book (or edit an existing one).",
                "Upload a cover image, add title, author, genre, and store buy links.",
                "Save — your first post may be generated automatically.",
                "Use the /go/ tracking link from each book to count clicks in BookPromoter."
            ],
            UpNext = "Connect social accounts and set your weekly posting schedule."
        },
        new()
        {
            Path = "/my-account",
            NavLabel = "Social & Schedule",
            Title = "Social Media & Posting Schedule",
            Summary = "Connect platforms, set how often to post, and choose auto-post or manual approval.",
            Instructions =
            [
                "Connect or manually add each social platform once.",
                "Set Posts/week for each account (e.g. 1–3 to start).",
                "Check Auto-post to publish on schedule, or leave Approval required and approve in Ad Library.",
                "Click Save Posting Schedule — posts generate and auto-post runs (simulated until OAuth is live)."
            ],
            UpNext = "Generate this week's posts and copy them to social media."
        },
        new()
        {
            Path = "/ad-library",
            NavLabel = "Ad Library",
            Title = "Ad Library",
            Summary = "AI-generated posts for the current month — copy, regenerate, or approve for auto-posting.",
            Instructions =
            [
                "Click Generate This Week's Posts if the library is empty.",
                "Use Copy post + cover to paste into Facebook, Instagram, X, etc.",
                "Click Regenerate for a new caption while keeping the same book.",
                "If approval is required, click Approve for Auto-Post before the scheduler sends it."
            ],
            UpNext = "Track clicks and see which books perform best."
        },
        new()
        {
            Path = "/analytics",
            NavLabel = "Analytics",
            Title = "Analytics",
            Summary = "See clicks, posts generated, and platform mix — advanced charts unlock on higher plans.",
            Instructions =
            [
                "Review total clicks and posts at the top.",
                "Compare books and platforms to see what resonates.",
                "Use insights to adjust Posts/week on My Account.",
                "Publisher and Agency plans unlock deeper analytics sections."
            ],
            UpNext = "Manage your subscription, plan limits, and promo codes."
        },
        new()
        {
            Path = "/billing",
            NavLabel = "Billing",
            Title = "Subscription & Billing",
            Summary = "View your plan, change tier, apply promo codes, or manage Stripe billing.",
            Instructions =
            [
                "See your current plan and what's included.",
                "Apply a promotional or lifetime code if you have one.",
                "Upgrade or change plan as your catalog grows.",
                "Stripe checkout handles monthly billing securely."
            ],
            UpNext = "You're set — revisit any step from Help in the nav, or send us Feedback."
        }
    ];

    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        path = path.Split('?', 2)[0].TrimEnd('/').ToLowerInvariant();
        if (path == "") return "/";

        if (path.StartsWith("/books", StringComparison.Ordinal)) return "/books";
        if (path.StartsWith("/dashboard", StringComparison.Ordinal)) return "/dashboard";
        if (path.StartsWith("/my-account", StringComparison.Ordinal) || path.StartsWith("/schedule", StringComparison.Ordinal))
            return "/my-account";
        if (path.StartsWith("/ad-library", StringComparison.Ordinal)) return "/ad-library";
        if (path.StartsWith("/analytics", StringComparison.Ordinal)) return "/analytics";
        if (path.StartsWith("/billing", StringComparison.Ordinal) || path.StartsWith("/subscription", StringComparison.Ordinal))
            return "/billing";
        if (path.StartsWith("/help", StringComparison.Ordinal)) return "/help";

        return path;
    }

    public static HelpGuideStep? FindStep(string? path)
    {
        var normalized = NormalizePath(path);
        if (normalized == "/billing" || normalized == "/subscription")
            normalized = "/billing";
        return Steps.FirstOrDefault(s => s.Path == normalized);
    }

    public static int StepIndex(string? path)
    {
        var normalized = NormalizePath(path);
        if (normalized == "/subscription") normalized = "/billing";
        for (var i = 0; i < Steps.Length; i++)
            if (Steps[i].Path == normalized) return i;
        return -1;
    }

    public static IReadOnlyList<HelpGuideStep> AllSteps => Steps;

    public static string RenderPanel(AppStoreDb store, string? requestPath)
    {
        if (!store.HasCustomerAccess) return "";

        var normalized = NormalizePath(requestPath);
        if (normalized == "/help") return "";

        var index = StepIndex(normalized);
        if (index < 0) return "";

        var step = Steps[index];
        var instructions = string.Join("", step.Instructions.Select(i => $"<li>{H.Encode(i)}</li>"));

        var prev = index > 0 ? Steps[index - 1] : null;
        var next = index < Steps.Length - 1 ? Steps[index + 1] : null;

        var prevBtn = prev is null
            ? ""
            : $"""<a class="button secondary small" href="{H.Encode(BillingPath(store, prev.Path))}">&larr; Previous: {H.Encode(prev.NavLabel)}</a>""";

        var nextBtn = next is null
            ? """<a class="button small" href="/feedback">Send feedback &rarr;</a>"""
            : $"""<a class="button small" href="{H.Encode(BillingPath(store, next.Path))}">Next: {H.Encode(next.NavLabel)} &rarr;</a>""";

        return $"""
            <aside class="help-guide panel">
                <div class="help-guide-top">
                    <div>
                        <p class="eyebrow">App guide &middot; Step {index + 1} of {Steps.Length}</p>
                        <h2 class="help-guide-title">{H.Encode(step.Title)}</h2>
                    </div>
                    <a class="button secondary small" href="/help">Full guide</a>
                </div>
                <p class="muted">{H.Encode(step.Summary)}</p>
                <ul class="plan-features help-guide-list">{instructions}</ul>
                <p class="help-guide-next"><strong>What to do next:</strong> {H.Encode(step.UpNext)}</p>
                <div class="help-guide-actions">
                    {prevBtn}
                    {nextBtn}
                </div>
            </aside>
            """;
    }

    static string BillingPath(AppStoreDb store, string path) =>
        path == "/billing" && !store.HasCustomerAccess ? "/subscription" : path;
}
