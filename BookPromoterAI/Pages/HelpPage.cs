using System.Text;

namespace BookPromoterAI;

static class HelpPage
{
    public static string Render(AppStoreDb store, string? currentPath)
    {
        var currentIndex = HelpGuide.StepIndex(currentPath);
        var steps = HelpGuide.AllSteps;
        var rows = new StringBuilder();

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var path = step.Path == "/billing" && !store.HasCustomerAccess ? "/subscription" : step.Path;
            var isCurrent = i == currentIndex;
            var marker = isCurrent ? "help-step-current" : "";
            var instructions = string.Join("", step.Instructions.Select(x => $"<li>{H.Encode(x)}</li>"));

            var prev = i > 0 ? steps[i - 1] : null;
            var next = i < steps.Count - 1 ? steps[i + 1] : null;
            var prevPath = prev is null ? "" : prev.Path == "/billing" && !store.HasCustomerAccess ? "/subscription" : prev.Path;
            var nextPath = next is null ? "" : next.Path == "/billing" && !store.HasCustomerAccess ? "/subscription" : next.Path;

            var nav = new StringBuilder();
            if (prev is not null)
                nav.Append($"""<a class="button secondary small" href="{H.Encode(prevPath)}">&larr; {H.Encode(prev.NavLabel)}</a> """);
            nav.Append($"""<a class="button small" href="{H.Encode(path)}">Open {H.Encode(step.NavLabel)}</a> """);
            if (next is not null)
                nav.Append($"""<a class="button small" href="{H.Encode(nextPath)}">{H.Encode(next.NavLabel)} &rarr;</a>""");

            rows.Append($"""
                <section class="panel help-step-card {marker}" id="help-step-{i + 1}">
                    <p class="eyebrow">Step {i + 1} of {steps.Count}</p>
                    <h2>{H.Encode(step.Title)}</h2>
                    <p class="muted">{H.Encode(step.Summary)}</p>
                    <ul class="plan-features">{instructions}</ul>
                    <p class="help-guide-next"><strong>What to do next:</strong> {H.Encode(step.UpNext)}</p>
                    <div class="help-guide-actions">{nav}</div>
                </section>
                """);
        }

        var startPath = store.HasCustomerAccess ? "/dashboard" : "/start";
        var startLabel = store.HasCustomerAccess ? "Start the tour on Dashboard" : "Sign in to start";

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Help</p>
                    <h1>How to use BookPromoter AI</h1>
                    <p class="muted">Follow these steps in order — Dashboard, Books, Social schedule, Ad Library, Analytics, and Billing. Each app page shows the same guide with Previous and Next buttons.</p>
                </div>
                <a class="button" href="{H.Encode(startPath)}">{startLabel}</a>
            </section>

            {rows}
            """;
    }
}
