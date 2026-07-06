using System.Text;
namespace BookPromoterAI;

static class BooksPage
{
    public static string Render(AppStoreDb store, Book? editing)
    {
        var bookListHtml = BuildBookList(store);

        var limitNotice = "";
        var limitMessage = store.CheckBookLimit();
        if (limitMessage is not null)
            limitNotice = $"""<div class="notice error">{H.Encode(limitMessage)}</div>""";

        var formTitle = editing is null ? "Add Book" : $"Edit Book: {H.Encode(editing.Title)}";
        var formAction = editing is null ? "/books" : $"/books/edit/{editing.Id}";
        var submitLabel = editing is null ? "Save Book" : "Update Book";
        var cancelLink = editing is null ? "" : """<a class="button secondary" href="/books">Cancel</a>""";

        var title = editing?.Title ?? "";
        var author = editing?.AuthorName ?? "";
        var genre = editing?.Genre ?? "";
        var description = editing?.Description ?? "";
        var readAloudExcerpt = editing?.ReadAloudExcerpt ?? "";
        var coverImageUrl = editing?.CoverImageUrl ?? "";
        var currentCoverNote = editing is not null && !string.IsNullOrWhiteSpace(coverImageUrl)
            ? """<p class="muted small-text">Your current cover is saved. Upload a new file only to replace it.</p>"""
            : "";

        var linkRows = new StringBuilder();
        var existingLinks = editing?.Links ?? [];
        var linkCount = Math.Max(existingLinks.Count, 1);
        for (var i = 0; i < linkCount; i++)
        {
            var storeName = i < existingLinks.Count ? existingLinks[i].StoreName : "";
            var url = i < existingLinks.Count ? existingLinks[i].Url : "";
            linkRows.Append(LinkRow(storeName, url));
        }

        var script = $"""
            <script>
            var linkRowTemplate = `{LinkRowTemplateJs()}`;
            """ + """

            function addLinkRow() {
                var list = document.getElementById('link-list');
                var wrapper = document.createElement('div');
                wrapper.innerHTML = linkRowTemplate;
                list.appendChild(wrapper.firstElementChild);
            }
            function toggleCustomStore(select) {
                var row = select.closest('.link-row');
                var custom = row.querySelector('.custom-store');
                custom.style.display = select.value === '__custom__' ? 'block' : 'none';
            }
            function toggleCustomGenre(select) {
                var custom = document.querySelector('.genre-custom');
                if (!custom) return;
                custom.style.display = select.value === '__custom__' ? 'block' : 'none';
            }
            function updateWordCount(textarea) {
                var words = textarea.value.trim().length === 0 ? [] : textarea.value.trim().split(/\s+/);
                var count = words.length;
                var label = document.getElementById('word-count');
                if (count > 200) { words = words.slice(0, 200); textarea.value = words.join(' '); count = 200; }
                label.textContent = count + ' / 200 words';
                label.style.color = count >= 200 ? '#b91c1c' : '';
            }
            function updateExcerptWordCount(textarea) {
                var words = textarea.value.trim().length === 0 ? [] : textarea.value.trim().split(/\s+/);
                var count = words.length;
                var label = document.getElementById('excerpt-word-count');
                if (count > 155) { words = words.slice(0, 155); textarea.value = words.join(' '); count = 155; }
                label.textContent = count + ' / 155 words';
                label.style.color = count >= 155 ? '#b91c1c' : '';
            }
            document.addEventListener('DOMContentLoaded', function () {
                var field = document.getElementById('description-field');
                if (field) updateWordCount(field);
                var excerptField = document.getElementById('excerpt-field');
                if (excerptField) updateExcerptWordCount(excerptField);
                var genreSelect = document.getElementById('genre-select');
                if (genreSelect) toggleCustomGenre(genreSelect);
            });
            </script>
            """;

        return $"""
            <section class="split">
                <form method="post" action="{formAction}" class="panel form" enctype="multipart/form-data">
                    <h1>{formTitle}</h1>
                    {limitNotice}
                    <label>Title <input name="title" value="{H.Encode(title)}" required></label>
                    <label>Author <input name="authorName" value="{H.Encode(author)}" required></label>
                    <label>Genre</label>
                    {BookGenres.RenderSelect(genre)}
                    <label>Description (200 words max)
                        <textarea name="description" id="description-field" oninput="updateWordCount(this)">{H.Encode(description)}</textarea>
                        <span id="word-count" class="small-text muted"></span>
                    </label>
                    <label>Read-aloud excerpt (155 words max — fits a 60s TikTok)
                        <textarea name="readAloudExcerpt" id="excerpt-field" oninput="updateExcerptWordCount(this)" placeholder="Paste a short chapter sample or opening scene. Used on the Videos tab for AI read-aloud promos.">{H.Encode(readAloudExcerpt)}</textarea>
                        <span id="excerpt-word-count" class="small-text muted"></span>
                    </label>

                    <div class="link-list-section">
                        <label>Where can readers buy or read this book?</label>
                        <p class="muted small-text">Add every store (Amazon, Kindle, Apple Books, B&amp;N, Kobo, Inkitt, your website, etc). The first link is used for your tracking redirect.</p>
                        <div class="link-list" id="link-list">{linkRows}</div>
                        <button class="button secondary small" type="button" onclick="addLinkRow()">+ Add another store/link</button>
                    </div>

                    <div class="cover-section">
                        <label>Book Cover</label>
                        <p class="muted small-text">Upload a cover image from your computer (JPG, PNG, or WebP).</p>
                        <label class="sub-label">Upload cover from your computer <input name="coverFile" type="file" accept="image/*"></label>
                        {currentCoverNote}
                    </div>

                    <div class="form-actions">
                        <button class="button" type="submit">{submitLabel}</button>
                        {cancelLink}
                    </div>
                </form>

                <section class="panel">
                    <h1>Your Books</h1>
                    {bookListHtml}
                </section>
            </section>

            {script}
            """;
    }

    // Builds the book list. When multi-client management is enabled,
    // books are grouped into separate panels per client/author.
    // Unmatched books (where AuthorName doesn't match any client) appear
    // in an "Unassigned" section at the bottom.
    static string BuildBookList(AppStoreDb store)
    {
        if (store.Books.Count == 0)
            return """<p class="muted">No books yet. Add your first book using the form.</p>""";

        if (!store.HasMultiClient)
        {
            // Flat list for non-multi-client plans
            var rows = new StringBuilder();
            foreach (var book in store.Books)
                rows.Append(BookRow(book));
            return rows.ToString();
        }

        // Multi-client: group books by matched client
        var result = new StringBuilder();
        foreach (var client in store.Clients)
        {
            var clientBooks = store.Books.Where(b => b.ClientId == client.Id).ToList();
            if (clientBooks.Count == 0) continue;

            result.Append($"""<h3 class="author-heading">{H.Encode(client.Name)}</h3><div class="author-book-group">""");
            foreach (var book in clientBooks)
                result.Append(BookRow(book));
            result.Append("</div>");
        }

        // Books not matched to any client
        var unmatched = store.Books.Where(b => b.ClientId is null).ToList();
        if (unmatched.Count > 0)
        {
            result.Append("""<h3 class="author-heading muted">Unassigned</h3><div class="author-book-group">""");
            foreach (var book in unmatched)
                result.Append(BookRow(book));
            result.Append("</div>");
        }

        return result.ToString();
    }

    static string BookRow(Book book)
    {
        var cover = string.IsNullOrWhiteSpace(book.CoverImageUrl)
            ? """<div class="cover-placeholder">No cover</div>"""
            : $"""<img class="book-cover" src="{H.Encode(book.CoverImageUrl)}" alt="{H.Encode(book.Title)} cover">""";

        var linkBadges = new StringBuilder();
        foreach (var link in book.Links)
            linkBadges.Append($"""<span class="link-badge">{H.Encode(link.StoreName)}</span>""");
        if (book.Links.Count == 0)
            linkBadges.Append("""<span class="link-badge muted-badge">No purchase links yet</span>""");

        var purchaseUrl = PostBranding.PrimaryPurchaseUrl(book);
        var storeLinkLine = purchaseUrl is null
            ? """<small class="muted">No store link yet — add one when editing this book.</small>"""
            : $"""<small>Store link: <a href="{H.Encode(purchaseUrl)}" target="_blank" rel="noopener">{H.Encode(purchaseUrl)}</a></small>""";

        return $"""
            <article class="book-row">
                {cover}
                <div>
                    <strong>{H.Encode(book.Title)}</strong>
                    <p>{H.Encode(book.Genre)} by {H.Encode(book.AuthorName)}</p>
                    {storeLinkLine}
                    <small class="muted">Click tracking link: /go/{H.Encode(book.TrackingCode)}</small>
                    <div class="link-badges">{linkBadges}</div>
                </div>
                <span>{book.MonthlyClicks} clicks</span>
                <div class="row-actions">
                    <a class="button small" href="/books/edit/{book.Id}">Edit</a>
                    <form method="post" action="/books/delete/{book.Id}">
                        <button class="danger-button small" type="submit">Remove</button>
                    </form>
                </div>
            </article>
            """;
    }

    public static string LinkRow(string storeName, string url)
    {
        var stores = new[] { "Amazon", "Kindle", "Apple Books", "Barnes & Noble", "Kobo", "Google Play Books", "Inkitt", "Wattpad", "Author Website", "Other" };
        var options = new StringBuilder();
        var isCustom = !string.IsNullOrWhiteSpace(storeName) && !stores.Contains(storeName);

        options.Append("""<option value="">Choose store...</option>""");
        foreach (var s in stores)
        {
            var selected = s == storeName ? "selected" : "";
            var value = s == "Other" ? "__custom__" : s;
            if (isCustom && s == "Other")
                options.Append("""<option value="__custom__" selected>Other</option>""");
            else
                options.Append($"""<option value="{H.Encode(value)}" {selected}>{H.Encode(s)}</option>""");
        }

        var customDisplay = isCustom ? "block" : "none";
        var customValue = isCustom ? storeName : "";

        return $"""
            <div class="link-row">
                <select name="linkStore" onchange="toggleCustomStore(this)">{options}</select>
                <input class="custom-store" name="linkStoreCustom" placeholder="Platform name" value="{H.Encode(customValue)}" style="display:{customDisplay}">
                <input name="linkUrl" placeholder="https://..." value="{H.Encode(url)}">
            </div>
            """;
    }

    public static string LinkRowTemplateJs()
    {
        var html = LinkRow("", "");
        return html.Replace("`", "\\`").Replace("\n", "").Replace("\r", "");
    }
}
