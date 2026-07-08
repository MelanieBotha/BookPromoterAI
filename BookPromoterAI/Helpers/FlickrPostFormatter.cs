using System.Text;

namespace BookPromoterAI;

static class FlickrPostFormatter
{
    public static string BuildTitle(string postText, string? bookTitle, bool isBrand) =>
        WordPressPostFormatter.BuildTitle(postText, bookTitle, isBrand);

    public static string BuildDescription(string postText, string appBaseUrl, bool isBrand)
    {
        var sb = new StringBuilder(postText.Trim());
        if (isBrand)
        {
            sb.Append("\n\nAuthors — promote your books with BookPromoter AI: ");
            sb.Append(appBaseUrl.TrimEnd('/'));
            sb.Append("/start");
        }

        var plain = sb.ToString().Replace("\r\n", "\n").Trim();
        return plain.Length <= 4000 ? plain : plain[..3997].TrimEnd() + "…";
    }

    public static string BuildTags(bool isBrand, string? genre)
    {
        if (isBrand)
            return "writing books bookpromoterai author";

        var tags = new List<string> { "books", "reading", "author" };
        var genreTag = CleanTag(genre);
        if (!string.IsNullOrWhiteSpace(genreTag))
            tags.Add(genreTag);
        return string.Join(' ', tags.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    static string CleanTag(string? genre)
    {
        if (string.IsNullOrWhiteSpace(genre)) return "";
        var cleaned = new string(genre.Where(char.IsLetterOrDigit).ToArray());
        if (cleaned.Length > 75) cleaned = cleaned[..75];
        return cleaned;
    }
}
