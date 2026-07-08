using System.Net;
using System.Text;

namespace BookPromoterAI;

static class MediumPostFormatter
{
    public static string BuildTitle(string postText, string? bookTitle, bool isBrand) =>
        WordPressPostFormatter.BuildTitle(postText, bookTitle, isBrand);

    public static string ToHtmlContent(
        string postText,
        string appBaseUrl,
        bool isBrand,
        string? heroImageUrl = null,
        string? heroAlt = null)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(heroImageUrl) &&
            Uri.TryCreate(heroImageUrl.Trim(), UriKind.Absolute, out var imageUri) &&
            imageUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            var safeAlt = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(heroAlt) ? "Cover image" : heroAlt.Trim());
            sb.Append($"""<figure><img src="{imageUri.AbsoluteUri}" alt="{safeAlt}"/></figure>""");
        }

        sb.Append(TumblrPostFormatter.ToHtmlCaption(postText, appBaseUrl, includeAppCta: !isBrand));
        return sb.ToString();
    }

    public static string[] BuildTags(bool isBrand, string? genre)
    {
        if (isBrand)
            return ["writing", "books", "BookPromoterAI"];

        var tags = new List<string> { "books", "reading" };
        var genreTag = CleanTag(genre);
        if (!string.IsNullOrWhiteSpace(genreTag))
            tags.Add(genreTag);
        return tags.ToArray();
    }

    static string CleanTag(string? genre)
    {
        if (string.IsNullOrWhiteSpace(genre)) return "";
        var cleaned = new string(genre.Where(char.IsLetterOrDigit).ToArray());
        if (cleaned.Length > 25) cleaned = cleaned[..25];
        return cleaned;
    }
}
