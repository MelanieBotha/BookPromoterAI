using System.Text;
using System.Text.RegularExpressions;

namespace BookPromoterAI;

/// <summary>Builds Bluesky richtext facets so links and hashtags are clickable in the app.</summary>
static class BlueskyRichText
{
    static readonly Regex UrlRegex = new(@"https?://[^\s<>\[\]()]+", RegexOptions.Compiled);
    static readonly Regex TagRegex = new(@"(?<![\w])#([\w]+)", RegexOptions.Compiled);

    public static List<Dictionary<string, object?>> BuildFacets(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var facets = new List<Dictionary<string, object?>>();

        foreach (Match match in UrlRegex.Matches(text))
        {
            var uri = TrimTrailingPunctuation(match.Value);
            if (uri.Length == 0) continue;

            var byteStart = Utf8ByteOffset(text, match.Index);
            var byteEnd = Utf8ByteOffset(text, match.Index + uri.Length);
            facets.Add(LinkFacet(byteStart, byteEnd, uri));
        }

        foreach (Match match in TagRegex.Matches(text))
        {
            var tag = match.Groups[1].Value;
            if (tag.Length == 0) continue;

            var byteStart = Utf8ByteOffset(text, match.Index);
            var byteEnd = Utf8ByteOffset(text, match.Index + match.Length);
            facets.Add(TagFacet(byteStart, byteEnd, tag));
        }

        facets.Sort((a, b) => ByteStart(a).CompareTo(ByteStart(b)));
        return facets;
    }

    static int ByteStart(Dictionary<string, object?> facet) =>
        ((Dictionary<string, object?>)facet["index"]!)["byteStart"] is int i ? i : 0;

    static int Utf8ByteOffset(string text, int charIndex) =>
        Encoding.UTF8.GetByteCount(text.AsSpan(0, charIndex));

    static string TrimTrailingPunctuation(string value) =>
        value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}', '"', '\'');

    static Dictionary<string, object?> LinkFacet(int byteStart, int byteEnd, string uri) => new()
    {
        ["index"] = new Dictionary<string, object?> { ["byteStart"] = byteStart, ["byteEnd"] = byteEnd },
        ["features"] = new object[]
        {
            new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.richtext.facet#link",
                ["uri"] = uri
            }
        }
    };

    static Dictionary<string, object?> TagFacet(int byteStart, int byteEnd, string tag) => new()
    {
        ["index"] = new Dictionary<string, object?> { ["byteStart"] = byteStart, ["byteEnd"] = byteEnd },
        ["features"] = new object[]
        {
            new Dictionary<string, object?>
            {
                ["$type"] = "app.bsky.richtext.facet#tag",
                ["tag"] = tag
            }
        }
    };
}
