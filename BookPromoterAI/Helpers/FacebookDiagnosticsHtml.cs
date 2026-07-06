using System.Text;
namespace BookPromoterAI;

static class FacebookDiagnosticsHtml
{
    public static string RenderPanel(
        IReadOnlyList<FacebookPostingDiagnostic> diagnostics,
        string formAction,
        string sectionAnchor = "",
        bool showAuthorAccountsOption = false)
    {
        var anchor = string.IsNullOrWhiteSpace(sectionAnchor) ? "" : $" id=\"{sectionAnchor}\"";
        var results = diagnostics.Count > 0 ? RenderResults(diagnostics) : "";
        var authorCheckbox = showAuthorAccountsOption
            ? """
                <label class="checkbox">
                    <input type="checkbox" name="includeAuthors" value="true">
                    Include all author Facebook accounts (every user)
                </label>
                """
            : "";

        return $"""
            <div class="panel owner-settings facebook-diagnostics"{anchor}>
                <h3>Facebook posting diagnostics</h3>
                <p class="muted">Checks stored Page tokens, Meta permissions, and optionally sends an <strong>unpublished</strong> probe post (safe to delete in Meta).</p>
                <form method="post" action="{H.Encode(formAction)}" class="form inline-form">
                    <label class="checkbox">
                        <input type="checkbox" name="runProbePost" value="true">
                        Run live probe post (unpublished)
                    </label>
                    {authorCheckbox}
                    <button class="button small" type="submit">Run diagnostics</button>
                </form>
                {results}
            </div>
            """;
    }

    static string RenderResults(IReadOnlyList<FacebookPostingDiagnostic> diagnostics)
    {
        var rows = new StringBuilder();
        foreach (var diag in diagnostics)
        {
            var noticeClass = diag.Status switch
            {
                "ok" => "success",
                "warn" => "",
                "meta_identity" => "error",
                _ => diag.Status == "skip" ? "" : "error"
            };
            var noticeAttr = string.IsNullOrEmpty(noticeClass) ? "notice" : $"notice {noticeClass}";

            var title = string.IsNullOrWhiteSpace(diag.UserEmail)
                ? $"{diag.Context}: {diag.PageName}"
                : $"{diag.Context} ({diag.UserEmail}): {diag.PageName}";

            var details = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(diag.PageId))
                details.Append($"""<li><strong>Page ID:</strong> <code>{H.Encode(diag.PageId)}</code></li>""");
            details.Append($"""<li><strong>Live connection:</strong> {(diag.IsLiveConnection ? "Yes" : "No")}</li>""");
            if (diag.TokenValid is bool valid)
                details.Append($"""<li><strong>Token valid:</strong> {(valid ? "Yes" : "No")}</li>""");
            if (diag.TokenExpiresAt is DateTime expires)
                details.Append($"""<li><strong>Token expires:</strong> {H.Encode(AppTimeZone.FormatWithZone(expires, "MMM d, yyyy HH:mm"))}</li>""");
            if (diag.Scopes.Count > 0)
                details.Append($"""<li><strong>Scopes:</strong> <code>{H.Encode(string.Join(", ", diag.Scopes))}</code></li>""");
            if (!string.IsNullOrWhiteSpace(diag.LastLogMessage))
                details.Append($"""<li><strong>Last failure:</strong> {H.Encode(diag.LastLogMessage)}</li>""");
            if (diag.ProbePostOk is bool probeOk)
                details.Append($"""<li><strong>Probe post:</strong> {(probeOk ? "Succeeded" : "Failed")} — {H.Encode(diag.ProbePostMessage ?? "")}</li>""");
            if (!string.IsNullOrWhiteSpace(diag.Recommendation))
                details.Append($"""<li><strong>Fix:</strong> {H.Encode(diag.Recommendation)}</li>""");

            rows.Append($"""
                <div class="{noticeAttr}">
                    <p><strong>{H.Encode(title)}</strong> — {H.Encode(diag.Summary)}</p>
                    <ul class="plan-features">{details}</ul>
                </div>
                """);
        }

        return $"""<div class="facebook-diagnostics-results">{rows}</div>""";
    }
}
