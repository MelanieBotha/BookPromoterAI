using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>Mastodon OAuth (dynamic app registration per instance) + status posts with optional image.</summary>
class MastodonService
{
    public const string CallbackPath = "/social-accounts/oauth-callback/Mastodon";
    public const string Scopes = "read write:statuses write:media";

    readonly HttpClient _http;

    public MastodonService(HttpClient http) => _http = http;

    public static string CallbackUrl(string appBaseUrl) =>
        $"{appBaseUrl.TrimEnd('/')}{CallbackPath}";

    public static string NormalizeInstance(string input)
    {
        var raw = input.Trim();
        if (string.IsNullOrWhiteSpace(raw)) return "";
        raw = raw.Replace("https://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "", StringComparison.OrdinalIgnoreCase);
        var slash = raw.IndexOf('/');
        if (slash > 0) raw = raw[..slash];
        return raw.TrimEnd('.').ToLowerInvariant();
    }

    public static string InstanceFromAcct(string acct)
    {
        if (string.IsNullOrWhiteSpace(acct)) return "";
        var at = acct.LastIndexOf('@');
        return at >= 0 && at < acct.Length - 1 ? NormalizeInstance(acct[(at + 1)..]) : "";
    }

    public async Task<(bool Ok, string Error, string? ClientId, string? ClientSecret)> RegisterAppAsync(
        string instance, string redirectUri, CancellationToken cancellationToken = default)
    {
        instance = NormalizeInstance(instance);
        if (string.IsNullOrWhiteSpace(instance))
            return (false, "Enter your Mastodon server (e.g. mastodon.social).", null, null);

        var form = new Dictionary<string, string>
        {
            ["client_name"] = "BookPromoter AI",
            ["redirect_uris"] = redirectUri,
            ["scopes"] = Scopes,
            ["website"] = "https://bookpromoterai.us"
        };
        var response = await _http.PostAsync($"https://{instance}/api/v1/apps", new FormUrlEncodedContent(form), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, $"Could not register on {instance}. Check the server name and try again.", null, null);

        var app = System.Text.Json.JsonSerializer.Deserialize<MastodonAppResponse>(body);
        if (app is null || string.IsNullOrWhiteSpace(app.ClientId) || string.IsNullOrWhiteSpace(app.ClientSecret))
            return (false, "Mastodon returned an unexpected response.", null, null);

        return (true, "", app.ClientId, app.ClientSecret);
    }

    public (string AuthorizeUrl, string State) BuildAuthorizationUrl(
        string instance, string clientId, string redirectUri, string state)
    {
        instance = NormalizeInstance(instance);
        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = Scopes,
            ["state"] = state
        };
        var url = $"https://{instance}/oauth/authorize?" +
                  string.Join("&", query.Select(kv =>
                      $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return (url, state);
    }

    public async Task<MastodonTokenSet?> ExchangeCodeAsync(
        string instance, string clientId, string clientSecret, string code, string redirectUri,
        CancellationToken cancellationToken = default)
    {
        instance = NormalizeInstance(instance);
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["scope"] = Scopes
        };
        var response = await _http.PostAsync($"https://{instance}/oauth/token", new FormUrlEncodedContent(form), cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<MastodonTokenSet>(cancellationToken: cancellationToken);
    }

    public async Task<MastodonAccount?> VerifyCredentialsAsync(
        string instance, string accessToken, CancellationToken cancellationToken = default)
    {
        instance = NormalizeInstance(instance);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://{instance}/api/v1/accounts/verify_credentials");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<MastodonAccount>(cancellationToken: cancellationToken);
    }

    public async Task<PostingResult> PostAsync(
        string instance,
        string accessToken,
        string postText,
        byte[]? imageBytes = null,
        string? imageMime = null,
        CancellationToken cancellationToken = default)
    {
        instance = NormalizeInstance(instance);
        string? mediaId = null;
        if (imageBytes is { Length: > 0 })
        {
            using var mediaRequest = new HttpRequestMessage(HttpMethod.Post, $"https://{instance}/api/v2/media");
            mediaRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(imageMime ?? "image/jpeg");
            content.Add(fileContent, "file", "cover.jpg");
            if (!string.IsNullOrWhiteSpace(postText))
                content.Add(new StringContent(TruncateAlt(postText)), "description");
            mediaRequest.Content = content;
            var mediaResponse = await _http.SendAsync(mediaRequest, cancellationToken);
            var mediaBody = await mediaResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!mediaResponse.IsSuccessStatusCode)
                return PostingResult.Failure($"Mastodon image upload failed: {mediaBody}");

            var media = System.Text.Json.JsonSerializer.Deserialize<MastodonMediaResponse>(mediaBody);
            mediaId = media?.Id;
        }

        using var statusRequest = new HttpRequestMessage(HttpMethod.Post, $"https://{instance}/api/v1/statuses");
        statusRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var form = new Dictionary<string, string> { ["status"] = postText, ["visibility"] = "public" };
        if (!string.IsNullOrWhiteSpace(mediaId))
            form["media_ids[]"] = mediaId;
        statusRequest.Content = new FormUrlEncodedContent(form);
        var statusResponse = await _http.SendAsync(statusRequest, cancellationToken);
        var statusBody = await statusResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!statusResponse.IsSuccessStatusCode)
            return PostingResult.Failure($"Mastodon post failed: {statusBody}");

        return PostingResult.LiveOk($"Posted to Mastodon (@{instance}).");
    }

    static string TruncateAlt(string text) =>
        text.Length <= 420 ? text : text[..417] + "…";
}

sealed class MastodonTokenSet
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = "";
    [JsonPropertyName("scope")] public string Scope { get; set; } = "";
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
}

sealed class MastodonAccount
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("acct")] public string Acct { get; set; } = "";
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
}

sealed class MastodonAppResponse
{
    [JsonPropertyName("client_id")] public string ClientId { get; set; } = "";
    [JsonPropertyName("client_secret")] public string ClientSecret { get; set; } = "";
}

sealed class MastodonMediaResponse
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
}
