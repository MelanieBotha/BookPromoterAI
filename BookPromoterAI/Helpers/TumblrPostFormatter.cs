using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace BookPromoterAI;

/// <summary>Turns generated promo text into Tumblr HTML captions, tags, and click-through URLs.</summary>
static class TumblrPostFormatter
{
    const int MaxTagLength = 40;
    const int MaxTags = 12;

    static readonly Regex UrlRegex = new(@"https?://[^\s<>""']+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string ToHtmlCaption(string postText, string appBaseUrl)
    {
        var sb = new StringBuilder();
        var paragraphOpen = false;
        string? bookUrl = null;

        foreach (var rawLine in postText.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var url = ExtractUrl(line);
            if (url is not null)
            {
                bookUrl ??= url;
                CloseParagraph(sb, ref paragraphOpen);
                AppendLinkParagraph(sb, url, "Get your copy →");
                continue;
            }

            if (line.Equals("Get your copy:", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!paragraphOpen)
            {
                sb.Append("<p>");
                paragraphOpen = true;
            }
            else
            {
                sb.Append("<br/>");
            }

            sb.Append(LinkifyEncodedText(line));
        }

        CloseParagraph(sb, ref paragraphOpen);

        if (bookUrl is not null)
            AppendPlainUrlFallback(sb, bookUrl);

        AppendAppCta(sb, appBaseUrl);
        return sb.ToString();
    }

    public static string BuildTags(string? bookTitle, string? authorName, string? genre)
    {
        var tags = new List<string> { "books", "reading", "bookpromoter ai", "indie author" };

        AddTag(tags, genre);
        AddTag(tags, authorName);
        AddTag(tags, bookTitle);

        return string.Join(",", tags.Take(MaxTags));
    }

    static void AppendAppCta(StringBuilder sb, string appBaseUrl)
    {
        var startUrl = $"{appBaseUrl.TrimEnd('/')}/start";
        AppendLinkParagraph(sb, startUrl, "Authors — promote your books with BookPromoter AI");
        AppendPlainUrlFallback(sb, startUrl);
    }

    static void AppendLinkParagraph(StringBuilder sb, string url, string label)
    {
        sb.Append("<p><strong><a href=\"")
            .Append(EscapeAttr(url))
            .Append("\" target=\"_blank\" rel=\"noopener noreferrer\">")
            .Append(WebUtility.HtmlEncode(label))
            .Append("</a></strong></p>");
    }

    static void AppendPlainUrlFallback(StringBuilder sb, string url)
    {
        sb.Append("<p>")
            .Append(WebUtility.HtmlEncode(url))
            .Append("</p>");
    }

    static void CloseParagraph(StringBuilder sb, ref bool paragraphOpen)
    {
        if (!paragraphOpen) return;
        sb.Append("</p>");
        paragraphOpen = false;
    }

    static string LinkifyEncodedText(string line)
    {
        var lastIndex = 0;
        var sb = new StringBuilder();
        foreach (Match match in UrlRegex.Matches(line))
        {
            sb.Append(WebUtility.HtmlEncode(line[lastIndex..match.Index]));
            var url = match.Value.TrimEnd('.', ',', ')', ']', '!');
            sb.Append("<a href=\"")
                .Append(EscapeAttr(url))
                .Append("\" target=\"_blank\" rel=\"noopener noreferrer\">")
                .Append(WebUtility.HtmlEncode(url))
                .Append("</a>");
            lastIndex = match.Index + match.Length;
        }

        sb.Append(WebUtility.HtmlEncode(line[lastIndex..]));
        return sb.ToString();
    }

    static string? ExtractUrl(string line)
    {
        if (IsHttpUrl(line))
            return line;

        var match = UrlRegex.Match(line);
        return match.Success ? match.Value.TrimEnd('.', ',', ')', ']', '!') : null;
    }

    static void AddTag(List<string> tags, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var tag = SanitizeTag(value);
        if (string.IsNullOrEmpty(tag)) return;
        if (tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase))) return;
        tags.Add(tag);
    }

    static string SanitizeTag(string value)
    {
        var tag = value.Trim().ToLowerInvariant();
        tag = tag.Replace(',', ' ');
        if (tag.Length > MaxTagLength)
            tag = tag[..MaxTagLength].TrimEnd();
        return tag;
    }

    static string EscapeAttr(string value) =>
        WebUtility.HtmlEncode(value);

    static bool IsHttpUrl(string line) =>
        line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
