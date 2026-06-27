using System.Text;
namespace BookPromoterAI;

static class ClientsPage
{
    public static string Render(AppStoreDb store, string notice)
    {
        var lockedNotice = !store.HasMultiClient
            ? $"""<div class="notice error">Multi-Client Management requires the Agency plan. You're currently on <strong>{H.Encode(store.CurrentPlan?.Name ?? store.AccessType)}</strong>. <a href="/billing">Upgrade your plan</a> to manage real clients.</div>"""
            : "";

        var rows = new StringBuilder();
        foreach (var client in store.Clients)
        {
            rows.Append($"""
                <article class="book-row">
                    <div>
                        <strong>{H.Encode(client.Name)}</strong>
                        <p>{H.Encode(client.ContactEmail)}</p>
                        <small>{H.Encode(client.Notes)}</small>
                    </div>
                    <form method="post" action="/clients/delete/{client.Id}">
                        <button class="danger-button small" type="submit">Remove</button>
                    </form>
                </article>
                """);
        }
        if (store.Clients.Count == 0)
            rows.Append("""<p class="muted">No clients added yet.</p>""");

        var clientBookCards = new StringBuilder();
        foreach (var client in store.Clients)
        {
            clientBookCards.Append($"""
                <article class="panel">
                    <h2>{H.Encode(client.Name)}</h2>
                    <p class="muted">{store.Books.Count} book(s) in the shared pool</p>
                </article>
                """);
        }

        return $"""
            <section class="hero">
                <div>
                    <p class="eyebrow">Clients</p>
                    <h1>Manage the authors and clients you promote books for.</h1>
                </div>
            </section>

            {notice}
            {lockedNotice}

            <section class="split">
                <form method="post" action="/clients" class="panel form">
                    <h1>Add Client</h1>
                    <label>Client/Author Name <input name="name" required></label>
                    <label>Contact Email <input name="contactEmail" type="email"></label>
                    <label>Notes <textarea name="notes"></textarea></label>
                    <button class="button" type="submit">Add Client</button>
                </form>
                <section class="panel">
                    <h1>Your Clients</h1>
                    {rows}
                </section>
            </section>

            <section class="post-grid">
                {clientBookCards}
            </section>
            """;
    }
}
