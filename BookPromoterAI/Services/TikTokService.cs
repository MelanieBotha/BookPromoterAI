using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>TikTok Content Posting API — OAuth 2.0 PKCE + video upload to creator inbox.</summary>
class TikTokService
{
    public const string CallbackPath = "/social-accounts/oauth-callback/TikTok";
    public const string Scopes = "user.info.basic,video.upload,video.publish";

    readonly HttpClient _http;
    readonly AppSettings _settings;

    public TikTokService(HttpClient http, AppSettings settings)
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
            ["client_key"] = _settings.TikTokClientKey,
            ["scope"] = Scopes,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };
        var authorizeUrl = "https://www.tiktok.com/v2/auth/authorize/?" +
                           string.Join("&", query.Select(kv =>
                               $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return (authorizeUrl, state, verifier);
    }

    public async Task<(bool Ok, string Error, TikTokTokenSet? Tokens, TikTokUser? User)> CompleteAuthorizationAsync(
        string code, string redirectUri, string codeVerifier, CancellationToken cancellationToken = default)
    {
        var tokens = await ExchangeCodeAsync(code, redirectUri, codeVerifier, cancellationToken);
        if (tokens is null)
            return (false, "TikTok did not return an access token. Try connecting again.", null, null);

        var user = await GetCurrentUserAsync(tokens.AccessToken, cancellationToken);
        if (user is null)
            return (false, "Connected to TikTok but could not read your profile. Try again.", null, null);

        return (true, "", tokens, user);
    }

    public async Task<TikTokTokenSet?> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://open.tiktokapis.com/v2/oauth/token/");
        request.Content = JsonContent.Create(new Dictionary<string, string>
        {
            ["client_key"] = _settings.TikTokClientKey,
            ["client_secret"] = _settings.TikTokClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });
        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var parsed = JsonSerializer.Deserialize<TikTokTokenResponse>(body);
        if (parsed?.AccessToken is null) return null;
        return new TikTokTokenSet(parsed.AccessToken, parsed.RefreshToken ?? refreshToken, parsed.ExpiresIn);
    }

    public async Task<(PostingResult Result, TikTokTokenSet? UpdatedTokens, string? PublishId)> SendVideoToInboxAsync(
        TikTokTokenSet tokens,
        string absoluteVideoUrl,
        string title,
        CancellationToken cancellationToken = default)
    {
        var working = tokens;
        TikTokTokenSet? updated = null;

        if (!string.IsNullOrWhiteSpace(working.RefreshToken))
        {
            var refreshed = await RefreshTokensAsync(working.RefreshToken, cancellationToken);
            if (refreshed is not null)
            {
                working = refreshed;
                updated = refreshed;
            }
        }

        var publishId = await InitInboxUploadAsync(working.AccessToken, absoluteVideoUrl, title, cancellationToken);
        if (publishId is null)
            return (PostingResult.Failure("TikTok could not start the video upload. Check that your video URL is public HTTPS and the domain is verified in TikTok Developer Portal."), updated, null);

        for (var i = 0; i < 15; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            var status = await FetchPublishStatusAsync(working.AccessToken, publishId, cancellationToken);
            if (status is null) continue;

            if (status.Equals("FAILED", StringComparison.OrdinalIgnoreCase))
                return (PostingResult.Failure("TikTok rejected the video upload. Check format (MP4/MOV, vertical 9:16 recommended) and try again."), updated, publishId);

            if (status.Equals("SEND_TO_USER_INBOX", StringComparison.OrdinalIgnoreCase) ||
                status.Equals("PUBLISH_COMPLETE", StringComparison.OrdinalIgnoreCase))
            {
                return (PostingResult.LiveOk(
                    "Video sent to your TikTok inbox — open the TikTok app to review, add sounds, and publish."), updated, publishId);
            }
        }

        return (PostingResult.LiveOk(
            "Video upload started on TikTok. Open the TikTok app in a few minutes to check your inbox."), updated, publishId);
    }

    async Task<TikTokTokenSet?> ExchangeCodeAsync(
        string code, string redirectUri, string codeVerifier, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://open.tiktokapis.com/v2/oauth/token/");
        request.Content = JsonContent.Create(new Dictionary<string, string>
        {
            ["client_key"] = _settings.TikTokClientKey,
            ["client_secret"] = _settings.TikTokClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        });
        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var parsed = JsonSerializer.Deserialize<TikTokTokenResponse>(body);
        if (parsed?.AccessToken is null) return null;
        return new TikTokTokenSet(parsed.AccessToken, parsed.RefreshToken ?? "", parsed.ExpiresIn);
    }

    async Task<TikTokUser?> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://open.tiktokapis.com/v2/user/info/?fields=open_id,display_name,avatar_url,username");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var parsed = JsonSerializer.Deserialize<TikTokUserInfoResponse>(body);
        var user = parsed?.Data?.User;
        if (user?.OpenId is null) return null;
        // Never fall back to open_id for Username — readers need @handle for profile links.
        var username = (user.Username ?? "").Trim().TrimStart('@');
        if (string.Equals(username, user.OpenId, StringComparison.OrdinalIgnoreCase))
            username = "";
        return new TikTokUser(user.OpenId, username, user.DisplayName ?? "TikTok");
    }

    async Task<string?> InitInboxUploadAsync(
        string accessToken, string videoUrl, string title, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://open.tiktokapis.com/v2/post/publish/inbox/video/init/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            source_info = new
            {
                source = "PULL_FROM_URL",
                video_url = videoUrl
            },
            post_info = new
            {
                title = Truncate(title, 150),
                privacy_level = "SELF_ONLY",
                disable_duet = false,
                disable_comment = false,
                disable_stitch = false,
                video_cover_timestamp_ms = 1000
            }
        });
        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var parsed = JsonSerializer.Deserialize<TikTokPublishInitResponse>(body);
        return parsed?.Data?.PublishId;
    }

    async Task<string?> FetchPublishStatusAsync(
        string accessToken, string publishId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://open.tiktokapis.com/v2/post/publish/status/fetch/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { publish_id = publishId });
        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var parsed = JsonSerializer.Deserialize<TikTokPublishStatusResponse>(body);
        return parsed?.Data?.Status;
    }

    static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max].TrimEnd() + "…";

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

    sealed class TikTokTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    sealed class TikTokUserInfoResponse
    {
        [JsonPropertyName("data")] public TikTokUserInfoData? Data { get; set; }
    }

    sealed class TikTokUserInfoData
    {
        [JsonPropertyName("user")] public TikTokUserData? User { get; set; }
    }

    sealed class TikTokUserData
    {
        [JsonPropertyName("open_id")] public string? OpenId { get; set; }
        [JsonPropertyName("username")] public string? Username { get; set; }
        [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
    }

    sealed class TikTokPublishInitResponse
    {
        [JsonPropertyName("data")] public TikTokPublishInitData? Data { get; set; }
    }

    sealed class TikTokPublishInitData
    {
        [JsonPropertyName("publish_id")] public string? PublishId { get; set; }
    }

    sealed class TikTokPublishStatusResponse
    {
        [JsonPropertyName("data")] public TikTokPublishStatusData? Data { get; set; }
    }

    sealed class TikTokPublishStatusData
    {
        [JsonPropertyName("status")] public string? Status { get; set; }
    }
}

record TikTokTokenSet(string AccessToken, string RefreshToken, int ExpiresIn);

record TikTokUser(string OpenId, string Username, string DisplayName)
{
    public bool HasPublicUsername =>
        !string.IsNullOrWhiteSpace(Username)
        && !string.Equals(Username, OpenId, StringComparison.OrdinalIgnoreCase);

    public string? ProfileUrl =>
        HasPublicUsername ? $"https://www.tiktok.com/@{Username.Trim().TrimStart('@')}" : null;
}
