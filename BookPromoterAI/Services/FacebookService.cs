using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>Meta Graph API — Facebook Login for Business + Page feed posting.</summary>
class FacebookService
{
    public const string CallbackPath = "/social-accounts/oauth-callback/facebook";
    public const string Scopes = "pages_show_list,pages_manage_posts,pages_read_engagement,public_profile";
    /// <summary>Permissions to enable on the Meta Login Configuration (not passed in OAuth URL).</summary>
    public static readonly string[] LoginConfigurationPermissions =
        ["pages_show_list", "pages_manage_posts", "pages_read_engagement", "public_profile"];
    public const string GraphVersion = "v21.0";

    readonly HttpClient _http;
    readonly AppSettings _settings;

    public FacebookService(HttpClient http, AppSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public static string CallbackUrl(string appBaseUrl) =>
        $"{appBaseUrl.TrimEnd('/')}{CallbackPath}";

    static string GraphUrl(string path) =>
        $"https://graph.facebook.com/{GraphVersion}/{path.TrimStart('/')}";

    public (string AuthorizeUrl, string State) BuildAuthorizationUrl(string redirectUri)
    {
        if (string.IsNullOrWhiteSpace(_settings.FacebookLoginConfigId))
            throw new InvalidOperationException("Facebook Login Config ID is not configured.");

        var state = Guid.NewGuid().ToString("N");
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _settings.FacebookAppId,
            ["redirect_uri"] = redirectUri,
            ["config_id"] = _settings.FacebookLoginConfigId,
            ["state"] = state,
            ["response_type"] = "code",
            ["override_default_response_type"] = "true"
        };
        var url = $"https://www.facebook.com/{GraphVersion}/dialog/oauth?" +
                  string.Join("&", query.Select(kv =>
                      $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return (url, state);
    }

    public async Task<(bool Ok, string Error, FacebookPageConnection? Connection)> CompleteAuthorizationAsync(
        string code, string redirectUri, bool brandContext, CancellationToken cancellationToken = default)
    {
        var shortLived = await ExchangeCodeAsync(code, redirectUri, cancellationToken);
        if (shortLived is null)
            return (false, "Facebook did not return an access token. Try connecting again.", null);

        var userToken = await ExchangeForLongLivedUserTokenAsync(shortLived, cancellationToken);
        if (string.IsNullOrWhiteSpace(userToken))
            return (false, "Facebook connected but the session could not be extended. Try again.", null);

        var pages = await GetManagedPagesAsync(userToken, cancellationToken);
        if (pages.Count == 0)
            return (false, "No Facebook Pages found. Create a Page and make sure you are an admin, then try again.", null);

        var page = PickPage(pages, brandContext);
        if (page is null)
            return (false, "Could not select a Facebook Page to connect.", null);

        return (true, "", new FacebookPageConnection(page, userToken));
    }

    public async Task<(PostingResult Result, FacebookTokenUpdate? Updated)> PostAsync(
        FacebookPageConnection connection,
        string postText,
        CancellationToken cancellationToken = default)
    {
        var first = await TryPostToPageAsync(connection.Page.Id, connection.Page.AccessToken, postText, cancellationToken);
        if (first.Success)
            return (PostingResult.LiveOk("Posted to Facebook Page."), null);

        if (!first.NeedsRefresh || string.IsNullOrWhiteSpace(connection.UserAccessToken))
            return (PostingResult.Failure(first.Error), null);

        var pages = await GetManagedPagesAsync(connection.UserAccessToken, cancellationToken);
        var refreshedPage = pages.FirstOrDefault(p => p.Id == connection.Page.Id);
        if (refreshedPage is null || string.IsNullOrWhiteSpace(refreshedPage.AccessToken))
            return (PostingResult.Failure("Facebook Page access expired. Reconnect your Facebook account in My Account."), null);

        var retry = await TryPostToPageAsync(refreshedPage.Id, refreshedPage.AccessToken, postText, cancellationToken);
        if (retry.Success)
        {
            return (PostingResult.LiveOk("Posted to Facebook Page."),
                new FacebookTokenUpdate(refreshedPage.AccessToken, connection.UserAccessToken));
        }

        return (PostingResult.Failure(retry.Error),
            new FacebookTokenUpdate(refreshedPage.AccessToken, connection.UserAccessToken));
    }

    async Task<string?> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        var url = GraphUrl("oauth/access_token") +
                  $"?client_id={Uri.EscapeDataString(_settings.FacebookAppId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&client_secret={Uri.EscapeDataString(_settings.FacebookAppSecret)}" +
                  $"&code={Uri.EscapeDataString(code)}";

        var response = await _http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<FacebookTokenResponse>(cancellationToken: cancellationToken);
        return string.IsNullOrWhiteSpace(payload?.AccessToken) ? null : payload.AccessToken;
    }

    async Task<string?> ExchangeForLongLivedUserTokenAsync(string shortLivedToken, CancellationToken cancellationToken)
    {
        var url = GraphUrl("oauth/access_token") +
                  $"?grant_type=fb_exchange_token" +
                  $"&client_id={Uri.EscapeDataString(_settings.FacebookAppId)}" +
                  $"&client_secret={Uri.EscapeDataString(_settings.FacebookAppSecret)}" +
                  $"&fb_exchange_token={Uri.EscapeDataString(shortLivedToken)}";

        var response = await _http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return shortLivedToken;

        var payload = await response.Content.ReadFromJsonAsync<FacebookTokenResponse>(cancellationToken: cancellationToken);
        return string.IsNullOrWhiteSpace(payload?.AccessToken) ? shortLivedToken : payload.AccessToken;
    }

    async Task<List<FacebookPage>> GetManagedPagesAsync(string userAccessToken, CancellationToken cancellationToken)
    {
        var url = GraphUrl("me/accounts") +
                  "?fields=id,name,access_token,username" +
                  $"&access_token={Uri.EscapeDataString(userAccessToken)}";

        var response = await _http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];

        var payload = await response.Content.ReadFromJsonAsync<FacebookPagesResponse>(cancellationToken: cancellationToken);
        return payload?.Data?
            .Where(p => !string.IsNullOrWhiteSpace(p.Id) && !string.IsNullOrWhiteSpace(p.AccessToken))
            .Select(p => new FacebookPage(
                p.Id!,
                p.Name?.Trim() ?? "Facebook Page",
                p.Username?.Trim() ?? p.Id!,
                p.AccessToken!))
            .ToList() ?? [];
    }

    async Task<(bool Success, bool NeedsRefresh, string Error)> TryPostToPageAsync(
        string pageId, string pageAccessToken, string postText, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["message"] = postText,
            ["access_token"] = pageAccessToken
        });

        var response = await _http.PostAsync(GraphUrl($"{pageId}/feed"), content, cancellationToken);
        if (response.IsSuccessStatusCode)
            return (true, false, "");

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            body.Contains("expired", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("Session has expired", StringComparison.OrdinalIgnoreCase))
            return (false, true, "Facebook Page token expired.");

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            return (false, false, "Facebook API access denied. Confirm pages_manage_posts is enabled and you are a Page admin.");

        return (false, false, DescribePostError(response.StatusCode, body));
    }

    static FacebookPage? PickPage(IReadOnlyList<FacebookPage> pages, bool brandContext)
    {
        if (pages.Count == 0) return null;
        if (brandContext)
        {
            var brand = pages.FirstOrDefault(p =>
                p.Name.Contains("Book Promoter", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("BookPromoter", StringComparison.OrdinalIgnoreCase));
            if (brand is not null) return brand;
        }
        return pages[0];
    }

    static string DescribePostError(System.Net.HttpStatusCode status, string detail)
    {
        if (detail.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            return "Facebook rejected the post as a duplicate.";
        return $"Facebook error ({(int)status}). Try again or reconnect your Page.";
    }

    sealed class FacebookTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    }

    sealed class FacebookPagesResponse
    {
        [JsonPropertyName("data")] public List<FacebookPageData>? Data { get; set; }
    }

    sealed class FacebookPageData
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("username")] public string? Username { get; set; }
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    }
}

record FacebookPage(string Id, string Name, string Handle, string AccessToken);

record FacebookPageConnection(FacebookPage Page, string UserAccessToken);

record FacebookTokenUpdate(string PageAccessToken, string UserAccessToken);
