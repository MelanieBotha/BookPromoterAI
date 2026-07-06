using System.Text;
namespace BookPromoterAI;

static class FeedbackPage
{
    public static string Render(AppStoreDb store, string notice)
    {
        var rows = new StringBuilder();
        // Only show the current user's own feedback history, not everyone's
        var myEntries = store.FeedbackEntries
            .Where(f => f.Email.Equals(store.LoggedInEmail, StringComparison.OrdinalIgnoreCase))
            .Take(10);

        foreach (var entry in myEntries)
        {
            rows.Append($"""
                <article class="book-row">
                    <div>
                        <strong>{H.Encode(entry.Category)}</strong>
                        <p>{H.Encode(entry.Message)}</p>
                        <small>{AppTimeZone.FormatWithZone(entry.SubmittedAt, "MMM d, yyyy HH:mm")}</small>
                    </div>
                </article>
                """);
        }

        if (!myEntries.Any())
            rows.Append("""<p class="muted">You haven't submitted any feedback yet.</p>""");

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Feedback &amp; Suggestions</p>
                    <h1>Help us improve BookPromoter AI.</h1>
                </div>
            </section>

            {notice}

            <section class="split">
                <form method="post" action="/feedback" class="panel form">
                    <h1>Send Feedback</h1>
                    <p class="muted">Found a bug, have an idea, or just want to tell us something? We read every submission.</p>
                    <label>Category
                        <select name="category">
                            <option>Suggestion</option>
                            <option>Bug Report</option>
                            <option>Feature Request</option>
                            <option>General Feedback</option>
                        </select>
                    </label>
                    <label>Your Email <input name="email" type="email" value="{H.Encode(store.LoggedInEmail ?? "")}" required></label>
                    <label>Message
                        <textarea name="message" placeholder="Tell us what's on your mind..." required></textarea>
                    </label>
                    <button class="button" type="submit">Submit Feedback</button>
                </form>

                <section class="panel">
                    <h1>Your Past Feedback</h1>
                    {rows}
                </section>
            </section>
            """;
    }
}
