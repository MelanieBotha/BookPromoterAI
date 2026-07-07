using System.Security.Cryptography;
using System.Text;

namespace BookPromoterAI;

/// <summary>RFC 5849 OAuth 1.0a signing for Tumblr.</summary>
static class TumblrOAuth1
{
    public static string PercentEncode(string value) =>
        Uri.EscapeDataString(value).Replace("%7E", "~", StringComparison.Ordinal);

    public static Dictionary<string, string> BuildSignedParameters(
        string method,
        string url,
        string consumerKey,
        string consumerSecret,
        string? token,
        string? tokenSecret,
        IEnumerable<KeyValuePair<string, string>>? extraParameters = null)
    {
        var oauth = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["oauth_consumer_key"] = consumerKey,
            ["oauth_nonce"] = Guid.NewGuid().ToString("N"),
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["oauth_version"] = "1.0"
        };
        if (!string.IsNullOrEmpty(token))
            oauth["oauth_token"] = token;

        var all = new SortedDictionary<string, string>(oauth, StringComparer.Ordinal);
        if (extraParameters is not null)
        {
            foreach (var (key, value) in extraParameters)
                all[key] = value;
        }

        var paramString = string.Join("&",
            all.Select(kv => $"{PercentEncode(kv.Key)}={PercentEncode(kv.Value)}"));

        var baseUrl = url.Split('?')[0];
        var signatureBase =
            $"{method.ToUpperInvariant()}&{PercentEncode(baseUrl)}&{PercentEncode(paramString)}";

        var signingKey = $"{PercentEncode(consumerSecret)}&{PercentEncode(tokenSecret ?? "")}";
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(signingKey));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureBase)));

        var result = new Dictionary<string, string>(oauth, StringComparer.Ordinal);
        if (extraParameters is not null)
        {
            foreach (var (key, value) in extraParameters)
            {
                if (!key.StartsWith("oauth_", StringComparison.Ordinal))
                    result[key] = value;
            }
        }

        result["oauth_signature"] = signature;
        return result;
    }

    public static string AuthorizationHeader(Dictionary<string, string> signedOAuthParameters)
    {
        var oauthOnly = signedOAuthParameters
            .Where(kv => kv.Key.StartsWith("oauth_", StringComparison.Ordinal))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal);

        var parts = oauthOnly.Select(kv => $"{PercentEncode(kv.Key)}=\"{PercentEncode(kv.Value)}\"");
        return "OAuth " + string.Join(", ", parts);
    }

    public static Dictionary<string, string> ParseFormBody(string body) =>
        body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                var eq = part.IndexOf('=');
                if (eq < 0) return new KeyValuePair<string, string>(Uri.UnescapeDataString(part), "");
                return new KeyValuePair<string, string>(
                    Uri.UnescapeDataString(part[..eq]),
                    Uri.UnescapeDataString(part[(eq + 1)..]));
            })
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
}
