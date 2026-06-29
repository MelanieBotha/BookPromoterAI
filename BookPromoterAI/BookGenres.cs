using System.Text;
using Microsoft.AspNetCore.Http;

namespace BookPromoterAI;

static class BookGenres
{
    public static readonly string[] Standard =
    [
        "Romance",
        "Romantic Comedy",
        "Romantic Suspense",
        "Contemporary Romance",
        "Historical Romance",
        "Paranormal Romance",
        "Fantasy Romance",
        "Mystery",
        "Thriller",
        "Suspense",
        "Crime",
        "Horror",
        "Science Fiction",
        "Fantasy",
        "Urban Fantasy",
        "Epic Fantasy",
        "Contemporary Fiction",
        "Literary Fiction",
        "Historical Fiction",
        "Women's Fiction",
        "Young Adult",
        "New Adult",
        "Middle Grade",
        "Children's",
        "Christian Fiction",
        "Inspirational",
        "Memoir",
        "Biography",
        "Self-Help",
        "Non-Fiction",
        "Poetry"
    ];

    public static bool IsStandard(string? genre) =>
        !string.IsNullOrWhiteSpace(genre) && Standard.Contains(genre, StringComparer.OrdinalIgnoreCase);

    public static string ParseFromForm(IFormCollection form)
    {
        var selected = form["genre"].ToString().Trim();
        if (selected == "__custom__")
            return form["genreCustom"].ToString().Trim();
        return selected;
    }

    public static string RenderSelect(string? currentGenre)
    {
        var genre = currentGenre?.Trim() ?? "";
        var isCustom = !string.IsNullOrWhiteSpace(genre) && !IsStandard(genre);
        var options = new StringBuilder();
        options.Append("""<option value="">Choose genre...</option>""");
        foreach (var g in Standard)
        {
            var selected = !isCustom && string.Equals(g, genre, StringComparison.OrdinalIgnoreCase) ? " selected" : "";
            options.Append($"""<option value="{H.Encode(g)}"{selected}>{H.Encode(g)}</option>""");
        }
        var otherSelectedAttr = isCustom ? " selected" : "";
        options.Append($"""<option value="__custom__"{otherSelectedAttr}>Other</option>""");

        var customDisplay = isCustom ? "block" : "none";
        var customValue = isCustom ? genre : "";

        return $"""
            <select name="genre" id="genre-select" onchange="toggleCustomGenre(this)">{options}</select>
            <input class="genre-custom" name="genreCustom" placeholder="Enter genre" value="{H.Encode(customValue)}" style="display:{customDisplay};margin-top:8px;">
            """;
    }
}
