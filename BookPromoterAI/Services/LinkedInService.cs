using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>LinkedIn OAuth 2.0 + member feed posting (UGC Posts API).</summary>
class LinkedInService
{
    public const string CallbackPath = "/social-accounts/oauth-callback/LinkedIn";
    public const string Scopes = "openid profile w_member_social";

    const string AuthorizeUrl = "https://www.linkedin.com/oauth/v2/authorization";
    const string TokenUrl = "https://www.linkedin.com/oauth/v2/accessToken";
    const string UserInfoUrl = "https://api.linkedin.com/v2/userinfo";
    const string UgcPostsUrl = "https://api.linkedin.com/v2/ugcPosts";

    readonly HttpClient _http;
    readonly AppSettings _settings;

    public LinkedInService(HttpClient http, AppSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public static string CallbackUrl(string appBaseUrl) =>
        $"{appBaseUrl.TrimEnd('/')}{CallbackPath}";

    public (string AuthorizeUrl, string State) BuildAuthorizationUrl(string redirectUri)
    {
        var state = Guid.NewGuid().ToString("N");
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = _settings.LinkedInClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = Scopes,
            ["state"] = state
        };
        var url = AuthorizeUrl + "?" +
                  string.Join("&", query.Select(kv =>
                      $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return (url, state);
    }

    public async Task<(bool Ok, string Error, LinkedInTokenSet? Tokens, LinkedInUser? User)> CompleteAuthorizationAsync(
        string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        var tokens = await ExchangeCodeAsync(code, redirectUri, cancellationToken);
        if (tokens is null)
            return (false, "LinkedIn did not return an access token. Try connecting again.", null, null);

        var user = await GetCurrentUserAsync(tokens.AccessToken, cancellationToken);
        if (user is null)
            return (false, "Connected to LinkedIn but could not read your profile. Try again.", null, null);

        return (true, "", tokens, user);
    }

    public async Task<(PostingResult Result, LinkedInTokenSet? UpdatedTokens)> PostAsync(
        LinkedInTokenSet tokens,
        string personId,
        string postText,
        CancellationToken cancellationToken = default)
    {
        var first = await TryPostAsync(tokens.AccessToken, personId, postText, cancellationToken);
        if (first.Success)
            return (PostingResult.LiveOk("Posted to LinkedIn."), null);

        if (!first.NeedsRefresh || string.IsNullOrWhiteSpace(tokens.RefreshToken))
            return (PostingResult.Failure(first.Error), null);

        var refreshed = await RefreshTokensAsync(tokens.RefreshToken, cancellationToken);
        if (refreshed is null)
            return (PostingResult.Failure("LinkedIn session expired. Reconnect your LinkedIn account in My Account."), null);

        var retry = await TryPostAsync(refreshed.AccessToken, personId, postText, cancellationToken);
        if (retry.Success)
            return (PostingResult.LiveOk("Posted to LinkedIn."), refreshed);

        return (PostingResult.Failure(retry.Error), refreshed);
    }

    async Task<LinkedInTokenSet?> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = _settings.LinkedInClientId,
            ["client_secret"] = _settings.LinkedInClientSecret
        });

        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<LinkedInTokenResponse>(cancellationToken: cancellationToken);
        return payload is null || string.IsNullOrWhiteSpace(payload.AccessToken)
            ? null
            : new LinkedInTokenSet(payload.AccessToken, payload.RefreshToken ?? "", payload.ExpiresIn);
    }

    async Task<LinkedInTokenSet?> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _settings.LinkedInClientId,
            ["client_secret"] = _settings.LinkedInClientSecret
        });

        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<LinkedInTokenResponse>(cancellationToken: cancellationToken);
        return payload is null || string.IsNullOrWhiteSpace(payload.AccessToken)
            ? null
            : new LinkedInTokenSet(payload.AccessToken, payload.RefreshToken ?? refreshToken, payload.ExpiresIn);
    }

    async Task<LinkedInUser?> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<LinkedInUserInfoResponse>(cancellationToken: cancellationToken);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Sub))
            return null;

        var name = string.IsNullOrWhiteSpace(payload.Name)
            ? $"{payload.GivenName} {payload.FamilyName}".Trim()
            : payload.Name.Trim();
        var handle = SlugHandle(name, payload.Sub);
        return new LinkedInUser(payload.Sub, handle, name);
    }

    async Task<(bool Success, bool NeedsRefresh, string Error)> TryPostAsync(
        string accessToken, string personId, string postText, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, UgcPostsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Restli-Protocol-Version", "2.0.0");
        request.Content = JsonContent.Create(new
        {
            author = $"urn:li:person:{personId}",
            lifecycleState = "PUBLISHED",
            specificContent = new Dictionary<string, object>
            {
                ["com.linkedin.ugc.ShareContent"] = new
                {
                    shareCommentary = new { text = postText },
                    shareMediaCategory = "NONE"
                }
            },
            visibility = new Dictionary<string, object>
            {
                ["com.linkedin.ugc.MemberNetworkVisibility"] = "PUBLIC"
            }
        });

        var response = await _http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return (true, false, "");

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return (false, true, "LinkedIn session expired.");

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            return (false, false, "LinkedIn API access denied. Confirm your developer app has Share on LinkedIn enabled and w_member_social scope.");

        return (false, false, DescribePostError(response.StatusCode, body));
    }

    static string DescribePostError(System.Net.HttpStatusCode status, string detail)
    {
        if (detail.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            return "LinkedIn rejected the post as a duplicate.";
        return $"LinkedIn error ({(int)status}). Try again or reconnect your account.";
    }

    static string SlugHandle(string name, string memberId)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            var slug = new string(name.ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c) || c == '-')
                .ToArray())
                .Trim('-');
            if (slug.Length >= 2)
                return slug;
        }
        return $"member-{memberId[..Math.Min(8, memberId.Length)]}";
    }

    sealed class LinkedInTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    sealed class LinkedInUserInfoResponse
    {
        [JsonPropertyName("sub")] public string? Sub { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("given_name")] public string? GivenName { get; set; }
        [JsonPropertyName("family_name")] public string? FamilyName { get; set; }
    }
}

record LinkedInTokenSet(string AccessToken, string RefreshToken, int ExpiresIn);

record LinkedInUser(string Id, string Handle, string Name);
