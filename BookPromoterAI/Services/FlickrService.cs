using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace BookPromoterAI;

/// <summary>Flickr OAuth 1.0a + photo uploads for book promos.</summary>
class FlickrService
{
    public const string CallbackPath = "/social-accounts/oauth-callback/Flickr";

    const string RequestTokenUrl = "https://www.flickr.com/services/oauth/request_token";
    const string AuthorizeUrl = "https://www.flickr.com/services/oauth/authorize";
    const string AccessTokenUrl = "https://www.flickr.com/services/oauth/access_token";
    const string RestUrl = "https://api.flickr.com/services/rest";
    const string UploadUrl = "https://up.flickr.com/services/upload/";

    readonly HttpClient _http;
    readonly AppSettings _settings;

    public FlickrService(HttpClient http, AppSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public static string CallbackUrl(string appBaseUrl) =>
        $"{appBaseUrl.TrimEnd('/')}{CallbackPath}";

    public async Task<(bool Ok, string Error, string? RequestToken, string? RequestTokenSecret)> RequestTokenAsync(
        string callbackUrl, CancellationToken cancellationToken = default)
    {
        if (!_settings.IsFlickrConfigured)
            return (false, "Flickr API credentials are not configured.", null, null);

        var signed = TumblrOAuth1.BuildSignedParameters(
            "POST", RequestTokenUrl,
            _settings.FlickrApiKey, _settings.FlickrApiSecret,
            null, null,
            [new KeyValuePair<string, string>("oauth_callback", callbackUrl)]);

        using var request = new HttpRequestMessage(HttpMethod.Post, RequestTokenUrl);
        request.Headers.TryAddWithoutValidation("Authorization", TumblrOAuth1.AuthorizationHeader(signed));

        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = string.IsNullOrWhiteSpace(body) ? "" : $" {TumblrOAuth1.Truncate(body, 120)}";
            return (false, $"Flickr request token failed ({(int)response.StatusCode}).{detail}", null, null);
        }

        var parsed = TumblrOAuth1.ParseFormBody(body);
        if (!parsed.TryGetValue("oauth_token", out var token) ||
            !parsed.TryGetValue("oauth_token_secret", out var secret))
            return (false, "Flickr returned an unexpected response.", null, null);

        return (true, "", token, secret);
    }

    public string BuildAuthorizeUrl(string requestToken) =>
        $"{AuthorizeUrl}?oauth_token={Uri.EscapeDataString(requestToken)}&perms=write";

    public async Task<(bool Ok, string Error, FlickrTokenSet? Tokens)> ExchangeAccessTokenAsync(
        string requestToken, string requestTokenSecret, string verifier,
        CancellationToken cancellationToken = default)
    {
        var signed = TumblrOAuth1.BuildSignedParameters(
            "POST", AccessTokenUrl,
            _settings.FlickrApiKey, _settings.FlickrApiSecret,
            requestToken, requestTokenSecret,
            [new KeyValuePair<string, string>("oauth_verifier", verifier)]);

        using var request = new HttpRequestMessage(HttpMethod.Post, AccessTokenUrl);
        request.Headers.TryAddWithoutValidation("Authorization", TumblrOAuth1.AuthorizationHeader(signed));

        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, "Could not complete Flickr login. Try again.", null);

        var parsed = TumblrOAuth1.ParseFormBody(body);
        if (!parsed.TryGetValue("oauth_token", out var token) ||
            !parsed.TryGetValue("oauth_token_secret", out var secret))
            return (false, "Flickr returned an unexpected token response.", null);

        parsed.TryGetValue("user_nsid", out var nsid);
        parsed.TryGetValue("fullname", out var fullName);
        parsed.TryGetValue("username", out var username);

        return (true, "", new FlickrTokenSet(
            token,
            secret,
            nsid ?? "",
            username ?? "",
            fullName ?? ""));
    }

    public async Task<FlickrUserInfo?> GetUserInfoAsync(
        FlickrTokenSet tokens, CancellationToken cancellationToken = default)
    {
        var (ok, _, doc) = await CallRestAsync(
            tokens,
            "flickr.test.login",
            cancellationToken);
        if (!ok || doc is null) return null;

        var user = doc.Root?.Element("user");
        if (user is null) return null;

        var nsid = user.Attribute("id")?.Value ?? tokens.UserNsid;
        var username = user.Element("username")?.Value ?? tokens.Username;
        var displayName = string.IsNullOrWhiteSpace(tokens.FullName) ? username : tokens.FullName;
        return new FlickrUserInfo(nsid, username, displayName);
    }

    public async Task<PostingResult> UploadPhotoAsync(
        FlickrTokenSet tokens,
        byte[] imageBytes,
        string imageMime,
        string title,
        string description,
        string tags,
        CancellationToken cancellationToken = default)
    {
        if (imageBytes.Length == 0)
            return PostingResult.Failure("Flickr requires a photo. Add a book cover or reconnect with an image.");

        var formFields = new List<KeyValuePair<string, string>>
        {
            new("title", Truncate(title, 255)),
            new("description", Truncate(description, 4000)),
            new("tags", Truncate(tags, 500)),
            new("is_public", "1"),
            new("is_friend", "0"),
            new("is_family", "0"),
            new("safety_level", "1"),
            new("content_type", "1")
        };

        var signed = TumblrOAuth1.BuildSignedParameters(
            "POST", UploadUrl,
            _settings.FlickrApiKey, _settings.FlickrApiSecret,
            tokens.Token, tokens.TokenSecret,
            formFields);

        using var content = new MultipartFormDataContent();
        foreach (var (key, value) in formFields)
            content.Add(new StringContent(value), key);

        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(imageMime);
        content.Add(imageContent, "photo", GuessFileName(imageMime));

        using var request = new HttpRequestMessage(HttpMethod.Post, UploadUrl);
        request.Headers.TryAddWithoutValidation("Authorization", TumblrOAuth1.AuthorizationHeader(signed));
        request.Content = content;

        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return PostingResult.Failure($"Flickr upload failed ({(int)response.StatusCode}): {TumblrOAuth1.Truncate(body, 200)}");

        try
        {
            var xml = XDocument.Parse(body);
            var photoid = xml.Root?.Element("photoid")?.Value;
            var stat = xml.Root?.Attribute("stat")?.Value;
            if (!string.Equals(stat, "ok", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(photoid))
            {
                var msg = xml.Root?.Element("err")?.Attribute("msg")?.Value ?? TumblrOAuth1.Truncate(body, 200);
                return PostingResult.Failure($"Flickr rejected the upload: {msg}");
            }

            var photoUrl = string.IsNullOrWhiteSpace(tokens.Username)
                ? $"https://www.flickr.com/photos/{tokens.UserNsid}/{photoid}"
                : $"https://www.flickr.com/photos/{tokens.Username}/{photoid}";
            return PostingResult.LiveOk("Posted to Flickr.", photoUrl);
        }
        catch
        {
            return PostingResult.Failure($"Flickr returned an unexpected upload response: {TumblrOAuth1.Truncate(body, 200)}");
        }
    }

    async Task<(bool Ok, string Error, XDocument? Doc)> CallRestAsync(
        FlickrTokenSet tokens,
        string method,
        CancellationToken cancellationToken)
    {
        var query = new StringBuilder(RestUrl);
        query.Append("?method=").Append(Uri.EscapeDataString(method));
        query.Append("&api_key=").Append(Uri.EscapeDataString(_settings.FlickrApiKey));
        query.Append("&format=json&nojsoncallback=1");

        var signed = TumblrOAuth1.BuildSignedParameters(
            "GET", RestUrl,
            _settings.FlickrApiKey, _settings.FlickrApiSecret,
            tokens.Token, tokens.TokenSecret,
            [
                new KeyValuePair<string, string>("method", method),
                new KeyValuePair<string, string>("api_key", _settings.FlickrApiKey),
                new KeyValuePair<string, string>("format", "json"),
                new KeyValuePair<string, string>("nojsoncallback", "1")
            ]);

        using var request = new HttpRequestMessage(HttpMethod.Get, query.ToString());
        request.Headers.TryAddWithoutValidation("Authorization", TumblrOAuth1.AuthorizationHeader(signed));

        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, $"Flickr API call failed ({(int)response.StatusCode}).", null);

        try
        {
            var json = System.Text.Json.JsonSerializer.Deserialize<FlickrJsonEnvelope>(body);
            if (json is null || !string.Equals(json.Stat, "ok", StringComparison.OrdinalIgnoreCase))
                return (false, json?.Message ?? "Flickr API returned an error.", null);

            if (method == "flickr.test.login" && json.User is not null)
            {
                var xml = new XDocument(
                    new XElement("rsp",
                        new XAttribute("stat", "ok"),
                        new XElement("user",
                            new XAttribute("id", json.User.Id ?? ""),
                            new XElement("username", json.User.Username?.Content ?? ""))));
                return (true, "", xml);
            }

            return (true, "", null);
        }
        catch
        {
            return (false, "Flickr returned an unexpected response.", null);
        }
    }

    static string GuessFileName(string mime) => mime.ToLowerInvariant() switch
    {
        "image/png" => "cover.png",
        "image/gif" => "cover.gif",
        _ => "cover.jpg"
    };

    static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";
}

sealed record FlickrTokenSet(
    string Token,
    string TokenSecret,
    string UserNsid,
    string Username,
    string FullName);

sealed record FlickrUserInfo(string UserNsid, string Username, string DisplayName);

sealed class FlickrJsonEnvelope
{
    [JsonPropertyName("stat")] public string? Stat { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("user")] public FlickrJsonUser? User { get; set; }
}

sealed class FlickrJsonUser
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("username")] public FlickrJsonContent? Username { get; set; }
}

sealed class FlickrJsonContent
{
    [JsonPropertyName("_content")] public string? Content { get; set; }
}
