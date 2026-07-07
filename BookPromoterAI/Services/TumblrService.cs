using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>Tumblr OAuth 1.0a + blog text/photo posts.</summary>
class TumblrService
{
    public const string CallbackPath = "/social-accounts/oauth-callback/Tumblr";

    const string RequestTokenUrl = "https://www.tumblr.com/oauth/request_token";
    const string AuthorizeUrl = "https://www.tumblr.com/oauth/authorize";
    const string AccessTokenUrl = "https://www.tumblr.com/oauth/access_token";
    const string ApiBase = "https://api.tumblr.com/v2";

    readonly HttpClient _http;
    readonly AppSettings _settings;

    public TumblrService(HttpClient http, AppSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public static string CallbackUrl(string appBaseUrl) =>
        $"{appBaseUrl.TrimEnd('/')}{CallbackPath}";

    public async Task<(bool Ok, string Error, string? RequestToken, string? RequestTokenSecret)> RequestTokenAsync(
        string callbackUrl, CancellationToken cancellationToken = default)
    {
        if (!_settings.IsTumblrConfigured)
            return (false, "Tumblr API credentials are not configured.", null, null);

        var signed = TumblrOAuth1.BuildSignedParameters(
            "POST", RequestTokenUrl,
            _settings.TumblrConsumerKey, _settings.TumblrConsumerSecret,
            null, null,
            [new KeyValuePair<string, string>("oauth_callback", callbackUrl)]);

        using var request = new HttpRequestMessage(HttpMethod.Post, RequestTokenUrl);
        request.Headers.TryAddWithoutValidation("Authorization",
            TumblrOAuth1.AuthorizationHeader(signed));

        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = string.IsNullOrWhiteSpace(body) ? "" : $" {TumblrOAuth1.Truncate(body, 120)}";
            return (false, $"Tumblr request token failed ({(int)response.StatusCode}).{detail}", null, null);
        }

        var parsed = TumblrOAuth1.ParseFormBody(body);
        if (!parsed.TryGetValue("oauth_token", out var token) ||
            !parsed.TryGetValue("oauth_token_secret", out var secret))
            return (false, "Tumblr returned an unexpected response.", null, null);

        return (true, "", token, secret);
    }

    public string BuildAuthorizeUrl(string requestToken) =>
        $"{AuthorizeUrl}?oauth_token={Uri.EscapeDataString(requestToken)}";

    public async Task<(bool Ok, string Error, TumblrTokenSet? Tokens)> ExchangeAccessTokenAsync(
        string requestToken, string requestTokenSecret, string verifier,
        CancellationToken cancellationToken = default)
    {
        var signed = TumblrOAuth1.BuildSignedParameters(
            "POST", AccessTokenUrl,
            _settings.TumblrConsumerKey, _settings.TumblrConsumerSecret,
            requestToken, requestTokenSecret,
            [new KeyValuePair<string, string>("oauth_verifier", verifier)]);

        using var request = new HttpRequestMessage(HttpMethod.Post, AccessTokenUrl);
        request.Headers.TryAddWithoutValidation("Authorization",
            TumblrOAuth1.AuthorizationHeader(signed));

        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, "Could not complete Tumblr login. Try again.", null);

        var parsed = TumblrOAuth1.ParseFormBody(body);
        if (!parsed.TryGetValue("oauth_token", out var token) ||
            !parsed.TryGetValue("oauth_token_secret", out var secret))
            return (false, "Tumblr returned an unexpected token response.", null);

        return (true, "", new TumblrTokenSet(token, secret));
    }

    public async Task<TumblrUserInfo?> GetUserInfoAsync(
        TumblrTokenSet tokens, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiBase}/user/info";
        var signed = TumblrOAuth1.BuildSignedParameters(
            "GET", url,
            _settings.TumblrConsumerKey, _settings.TumblrConsumerSecret,
            tokens.Token, tokens.TokenSecret);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization",
            TumblrOAuth1.AuthorizationHeader(signed));

        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var payload = await response.Content.ReadFromJsonAsync<TumblrUserInfoResponse>(cancellationToken);
        if (payload?.Response?.User is null) return null;

        var user = payload.Response.User;
        var blogs = user.Blogs?
            .Where(b => !string.IsNullOrWhiteSpace(b.Identifier))
            .Select(b => new TumblrBlog(
                b.Identifier,
                string.IsNullOrWhiteSpace(b.Title) ? b.Identifier : b.Title.Trim(),
                b.Primary))
            .ToList() ?? [];

        return new TumblrUserInfo(user.Name ?? "", blogs);
    }

    public async Task<PostingResult> PostAsync(
        TumblrTokenSet tokens,
        string blogIdentifier,
        string postText,
        string? imageUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blogIdentifier))
            return PostingResult.Failure("Tumblr blog is missing. Reconnect your account in My Account.");

        var blog = Uri.EscapeDataString(blogIdentifier.Trim());
        var url = $"{ApiBase}/blog/{blog}/post";

        var form = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            form["type"] = "photo";
            form["source"] = imageUrl.Trim();
            form["caption"] = Truncate(postText, 4096);
        }
        else
        {
            form["type"] = "text";
            form["body"] = Truncate(postText, 4096);
        }

        var signed = TumblrOAuth1.BuildSignedParameters(
            "POST", url,
            _settings.TumblrConsumerKey, _settings.TumblrConsumerSecret,
            tokens.Token, tokens.TokenSecret,
            form.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)));

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("Authorization",
            TumblrOAuth1.AuthorizationHeader(signed));
        request.Content = new FormUrlEncodedContent(form);

        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return PostingResult.Failure($"Tumblr post failed ({(int)response.StatusCode}): {Truncate(body, 200)}");

        try
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<TumblrPostResponse>(body);
            if (payload?.Meta?.Status is int status && status is >= 200 and < 300)
                return PostingResult.LiveOk($"Posted to Tumblr ({blogIdentifier}).");
            if (!string.IsNullOrWhiteSpace(payload?.Meta?.Msg))
                return PostingResult.Failure($"Tumblr rejected the post: {payload.Meta.Msg}");
        }
        catch { /* fall through */ }

        return PostingResult.LiveOk($"Posted to Tumblr ({blogIdentifier}).");
    }

    static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";
}

sealed record TumblrTokenSet(string Token, string TokenSecret);

sealed record TumblrBlog(string Identifier, string Title, bool Primary);

sealed record TumblrUserInfo(string Username, IReadOnlyList<TumblrBlog> Blogs);

sealed class TumblrUserInfoResponse
{
    [JsonPropertyName("response")] public TumblrUserInfoBody? Response { get; set; }
}

sealed class TumblrUserInfoBody
{
    [JsonPropertyName("user")] public TumblrUserPayload? User { get; set; }
}

sealed class TumblrUserPayload
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("blogs")] public List<TumblrBlogPayload>? Blogs { get; set; }
}

sealed class TumblrBlogPayload
{
    [JsonPropertyName("name")] public string Identifier { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("primary")] public bool Primary { get; set; }
}

sealed class TumblrPostResponse
{
    [JsonPropertyName("meta")] public TumblrMeta? Meta { get; set; }
}

sealed class TumblrMeta
{
    [JsonPropertyName("status")] public int Status { get; set; }
    [JsonPropertyName("msg")] public string? Msg { get; set; }
}
