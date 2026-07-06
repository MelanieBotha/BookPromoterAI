using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>X (Twitter) OAuth 2.0 PKCE + API v2 posting.</summary>
class XService
{
    public const string CallbackPath = "/social-accounts/oauth-callback/X";
    public const string Scopes = "tweet.read tweet.write users.read offline.access";

    readonly HttpClient _http;
    readonly AppSettings _settings;

    public XService(HttpClient http, AppSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public static string CallbackUrl(string appBaseUrl) =>
        $"{appBaseUrl.TrimEnd('/')}{CallbackPath}";

    public (string AuthorizeUrl, string State, string CodeVerifier) BuildAuthorizationUrl(string redirectUri)
    {
        var verifier = CreateCodeVerifier();
        var challenge = CreateCodeChallenge(verifier);
        var state = Guid.NewGuid().ToString("N");
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = _settings.XClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = Scopes,
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };
        var authorizeUrl = "https://twitter.com/i/oauth2/authorize?" +
                           string.Join("&", query.Select(kv =>
                               $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return (authorizeUrl, state, verifier);
    }

    public async Task<(bool Ok, string Error, XTokenSet? Tokens, XUser? User)> CompleteAuthorizationAsync(
        string code, string redirectUri, string codeVerifier, CancellationToken cancellationToken = default)
    {
        var tokens = await ExchangeCodeAsync(code, redirectUri, codeVerifier, cancellationToken);
        if (tokens is null)
            return (false, "X did not return an access token. Try connecting again.", null, null);

        var user = await GetCurrentUserAsync(tokens.AccessToken, cancellationToken);
        if (user is null)
            return (false, "Connected to X but could not read your profile. Try again.", null, null);

        return (true, "", tokens, user);
    }

    public async Task<(PostingResult Result, XTokenSet? UpdatedTokens)> PostAsync(
        XTokenSet tokens,
        string postText,
        byte[]? imageBytes = null,
        string? imageMime = null,
        CancellationToken cancellationToken = default)
    {
        string? mediaId = null;
        if (imageBytes is { Length: > 0 })
        {
            mediaId = await UploadMediaAsync(tokens.AccessToken, imageBytes, imageMime ?? "image/png", cancellationToken);
            if (mediaId is null)
                return (PostingResult.Failure("Could not upload the image to X. Try again."), null);
        }

        var first = await TryPostTweetAsync(tokens.AccessToken, postText, mediaId, cancellationToken);
        if (first.Success)
            return (PostingResult.LiveOk(imageBytes is { Length: > 0 }
                ? "Posted to X with logo."
                : "Posted to X.", first.PostId), null);

        if (!first.NeedsRefresh || string.IsNullOrWhiteSpace(tokens.RefreshToken))
            return (PostingResult.Failure(first.Error), null);

        var refreshed = await RefreshTokensAsync(tokens.RefreshToken, cancellationToken);
        if (refreshed is null)
            return (PostingResult.Failure("X session expired. Reconnect your X account in My Account."), null);

        if (imageBytes is { Length: > 0 })
        {
            mediaId = await UploadMediaAsync(refreshed.AccessToken, imageBytes, imageMime ?? "image/png", cancellationToken);
            if (mediaId is null)
                return (PostingResult.Failure("Could not upload the image to X after refresh. Try again."), refreshed);
        }

        var retry = await TryPostTweetAsync(refreshed.AccessToken, postText, mediaId, cancellationToken);
        if (retry.Success)
            return (PostingResult.LiveOk(imageBytes is { Length: > 0 }
                ? "Posted to X with logo."
                : "Posted to X.", retry.PostId), refreshed);

        return (PostingResult.Failure(retry.Error), refreshed);
    }

    async Task<string?> UploadMediaAsync(
        string accessToken, byte[] imageBytes, string mimeType, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("tweet_image"), "media_category");
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        form.Add(imageContent, "media", "logo.png");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://upload.twitter.com/1.1/media/upload.json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = form;

        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<XMediaUploadResponse>(cancellationToken: cancellationToken);
        return string.IsNullOrWhiteSpace(payload?.MediaIdString) ? null : payload.MediaIdString;
    }

    async Task<XTokenSet?> ExchangeCodeAsync(
        string code, string redirectUri, string codeVerifier, CancellationToken cancellationToken)
    {
        using var request = BuildTokenRequest();
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
            ["client_id"] = _settings.XClientId
        });

        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<XTokenResponse>(cancellationToken: cancellationToken);
        return payload is null || string.IsNullOrWhiteSpace(payload.AccessToken)
            ? null
            : new XTokenSet(payload.AccessToken, payload.RefreshToken ?? "", payload.ExpiresIn);
    }

    async Task<XTokenSet?> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var request = BuildTokenRequest();
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _settings.XClientId
        });

        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<XTokenResponse>(cancellationToken: cancellationToken);
        return payload is null || string.IsNullOrWhiteSpace(payload.AccessToken)
            ? null
            : new XTokenSet(payload.AccessToken, payload.RefreshToken ?? refreshToken, payload.ExpiresIn);
    }

    async Task<XUser?> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/2/users/me?user.fields=username,name");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<XUserResponse>(cancellationToken: cancellationToken);
        return payload?.Data is null || string.IsNullOrWhiteSpace(payload.Data.Id)
            ? null
            : new XUser(payload.Data.Id, payload.Data.Username ?? "", payload.Data.Name ?? "");
    }

    async Task<(bool Success, bool NeedsRefresh, string Error, string? PostId)> TryPostTweetAsync(
        string accessToken, string postText, string? mediaId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/2/tweets");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = string.IsNullOrWhiteSpace(mediaId)
            ? JsonContent.Create(new { text = postText })
            : JsonContent.Create(new { text = postText, media = new { media_ids = new[] { mediaId } } });

        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var postId = ParseTweetId(body);
            return (true, false, "", postId);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return (false, true, "X session expired.", null);

        if (body.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            return (false, false, "X rejected the post as a duplicate.", null);

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            return (false, false, "X API access denied. Confirm your developer app has write access and the connected account is allowed to post.", null);

        return (false, false, DescribePostError(response.StatusCode, body), null);
    }

    public async Task<SocialPostMetrics?> TryGetPostMetricsAsync(
        string tweetId, string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tweetId)) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/2/tweets/{Uri.EscapeDataString(tweetId)}?tweet.fields=public_metrics");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var payload = await response.Content.ReadFromJsonAsync<XTweetMetricsResponse>(cancellationToken: cancellationToken);
        var metrics = payload?.Data?.PublicMetrics;
        if (metrics is null) return null;
        return new SocialPostMetrics(metrics.LikeCount + metrics.ReplyCount, null);
    }

    static string? ParseTweetId(string body)
    {
        try
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<XTweetCreateResponse>(body);
            return payload?.Data?.Id;
        }
        catch { return null; }
    }

    HttpRequestMessage BuildTokenRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/2/oauth2/token");
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_settings.XClientId}:{_settings.XClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return request;
    }

    static string DescribePostError(System.Net.HttpStatusCode status, string detail)
    {
        if (detail.Contains("character limit", StringComparison.OrdinalIgnoreCase))
            return "Post exceeds X's character limit. Regenerate or shorten the post.";
        return $"X error ({(int)status}). Try again or reconnect your account.";
    }

    static string CreateCodeVerifier()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    static string CreateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    sealed class XTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    sealed class XUserResponse
    {
        [JsonPropertyName("data")] public XUserData? Data { get; set; }
    }

    sealed class XUserData
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("username")] public string? Username { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    sealed class XMediaUploadResponse
    {
        [JsonPropertyName("media_id_string")] public string? MediaIdString { get; set; }
    }

    sealed class XTweetCreateResponse
    {
        [JsonPropertyName("data")] public XTweetCreateData? Data { get; set; }
    }

    sealed class XTweetCreateData
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
    }

    sealed class XTweetMetricsResponse
    {
        [JsonPropertyName("data")] public XTweetMetricsData? Data { get; set; }
    }

    sealed class XTweetMetricsData
    {
        [JsonPropertyName("public_metrics")] public XTweetPublicMetrics? PublicMetrics { get; set; }
    }

    sealed class XTweetPublicMetrics
    {
        [JsonPropertyName("like_count")] public int LikeCount { get; set; }
        [JsonPropertyName("reply_count")] public int ReplyCount { get; set; }
        [JsonPropertyName("impression_count")] public int ImpressionCount { get; set; }
    }
}

record XTokenSet(string AccessToken, string RefreshToken, int ExpiresIn);

record XUser(string Id, string Username, string Name);
