using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>Meta Graph API — Facebook Login + Page feed posting.</summary>
class FacebookService
{
    public const string CallbackPath = "/social-accounts/oauth-callback/facebook";
    /// <summary>Legacy Meta redirect URI (underscore path) — still accepted for token exchange.</summary>
    public const string LegacyCallbackPath = "/social_accounts/oauth_callback/facebook";
    public const string Scopes = "pages_show_list,pages_manage_posts,pages_read_engagement,public_profile";
    /// <summary>Permissions to enable on the Meta Login Configuration (config mode only).</summary>
    public static readonly string[] LoginConfigurationPermissions =
        ["pages_show_list", "pages_manage_posts", "pages_read_engagement", "business_management", "public_profile"];
    public const string GraphVersion = "v22.0";

    public const string MetaBusinessIntegrationHelp =
        "Remove AuthorPromoter AI at facebook.com/settings?tab=business_tools if listed. Brand connect uses Facebook Login for Business — sign in with your personal Facebook account (Melanie Botha) that admins the Book Promoter AI Page, not the BookPromoter AI business portfolio.";

    readonly HttpClient _http;
    readonly AppSettings _settings;

    public FacebookService(HttpClient http, AppSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public static string CallbackUrl(string appBaseUrl) =>
        $"{appBaseUrl.TrimEnd('/')}{CallbackPath}";

    public static string LegacyCallbackUrl(string appBaseUrl) =>
        $"{appBaseUrl.TrimEnd('/')}{LegacyCallbackPath}";

    static string GraphUrl(string path) =>
        $"https://graph.facebook.com/{GraphVersion}/{path.TrimStart('/')}";

    public (string AuthorizeUrl, string State) BuildAuthorizationUrl(string redirectUri, bool brandContext = false)
    {
        var state = Guid.NewGuid().ToString("N");
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _settings.FacebookAppId,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["response_type"] = "code"
        };

        // Brand MUST use Login for Business config_id — scope OAuth triggers Meta's
        // "Continue as BookPromoter AI?" business-integration loop that never redirects back.
        if (brandContext)
        {
            if (!AppSettings.IsValidFacebookLoginConfigId(_settings.FacebookLoginConfigId))
                throw new InvalidOperationException("Facebook Login Config ID is required for brand Page connect.");
            query["config_id"] = _settings.FacebookLoginConfigId.Trim();
        }
        else if (_settings.FacebookUsesConfigLogin)
        {
            if (!AppSettings.IsValidFacebookLoginConfigId(_settings.FacebookLoginConfigId))
                throw new InvalidOperationException("Facebook Login Config ID is not configured.");
            query["config_id"] = _settings.FacebookLoginConfigId.Trim();
        }
        else
        {
            query["scope"] = Scopes;
            query["auth_type"] = "rerequest";
        }

        var url = $"https://www.facebook.com/{GraphVersion}/dialog/oauth?" +
                  string.Join("&", query.Select(kv =>
                      $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return (url, state);
    }

    public async Task<FacebookAuthOutcome> CompleteAuthorizationAsync(
        string code, string redirectUri, bool brandContext, CancellationToken cancellationToken = default)
    {
        var (shortLived, exchangeError) = await ExchangeCodeAsync(code, redirectUri, cancellationToken);
        if (shortLived is null)
            return FacebookAuthOutcome.Failed(exchangeError ?? "Facebook did not return an access token. Try connecting again.");

        var userToken = await ExchangeForLongLivedUserTokenAsync(shortLived, cancellationToken);
        if (string.IsNullOrWhiteSpace(userToken))
            return FacebookAuthOutcome.Failed("Facebook connected but the session could not be extended. Try again.");

        var pages = await GetManagedPagesAsync(userToken, cancellationToken);
        if (pages.Count == 0)
            return FacebookAuthOutcome.Failed("No Facebook Pages found. " + MetaBusinessIntegrationHelp);

        if (brandContext)
        {
            var brandPages = pages.Where(IsBookPromoterBrandPage).ToList();
            if (brandPages.Count == 1)
                return FacebookAuthOutcome.Connected(new FacebookPageConnection(brandPages[0], userToken));
            if (brandPages.Count > 1)
                return FacebookAuthOutcome.NeedsPageSelection(brandPages, userToken);
            if (pages.Count > 1)
                return FacebookAuthOutcome.NeedsPageSelection(pages, userToken);
            if (pages.Count == 1)
                return FacebookAuthOutcome.Failed(
                    $"Meta only granted \"{pages[0].Name}\" (not Book Promoter AI). On Facebook click Edit settings → Choose Pages → tick Book Promoter AI only. " +
                    MetaBusinessIntegrationHelp);
            return FacebookAuthOutcome.Failed("Could not select the Book Promoter AI Page.");
        }

        var authorPages = pages.Where(p => !IsBookPromoterBrandPage(p)).ToList();
        if (authorPages.Count == 0)
            return FacebookAuthOutcome.Failed("No author Facebook Pages found (only the BookPromoter AI business Page was detected). Create your own author Page on Facebook, or click Edit settings on the Facebook dialog to choose a different Page.");

        if (authorPages.Count == 1)
            return FacebookAuthOutcome.Connected(new FacebookPageConnection(authorPages[0], userToken));

        return FacebookAuthOutcome.NeedsPageSelection(authorPages, userToken);
    }

    public async Task<(PostingResult Result, FacebookTokenUpdate? Updated)> PostAsync(
        FacebookPageConnection connection,
        string postText,
        string? photoUrl = null,
        byte[]? photoBytes = null,
        string? photoMime = null,
        CancellationToken cancellationToken = default)
    {
        var hasPhoto = !string.IsNullOrWhiteSpace(photoUrl) || photoBytes is { Length: > 0 };
        var first = hasPhoto
            ? await TryPostPhotoAsync(connection.Page.Id, connection.Page.AccessToken, postText, photoUrl, photoBytes, photoMime, cancellationToken)
            : await TryPostToPageFeedAsync(connection.Page.Id, connection.Page.AccessToken, postText, link: null, cancellationToken);
        if (first.Success)
            return (PostingResult.LiveOk(hasPhoto ? "Posted to Facebook Page with book cover." : "Posted to Facebook Page."), null);

        if (!first.NeedsRefresh || string.IsNullOrWhiteSpace(connection.UserAccessToken))
            return (PostingResult.Failure(first.Error), null);

        var pages = await GetManagedPagesAsync(connection.UserAccessToken, cancellationToken);
        var refreshedPage = pages.FirstOrDefault(p => p.Id == connection.Page.Id);
        if (refreshedPage is null || string.IsNullOrWhiteSpace(refreshedPage.AccessToken))
            return (PostingResult.Failure("Facebook Page access expired. Reconnect your Facebook account in My Account."), null);

        var retry = hasPhoto
            ? await TryPostPhotoAsync(refreshedPage.Id, refreshedPage.AccessToken, postText, photoUrl, photoBytes, photoMime, cancellationToken)
            : await TryPostToPageFeedAsync(refreshedPage.Id, refreshedPage.AccessToken, postText, link: null, cancellationToken);
        if (retry.Success)
        {
            return (PostingResult.LiveOk(hasPhoto ? "Posted to Facebook Page with book cover." : "Posted to Facebook Page."),
                new FacebookTokenUpdate(refreshedPage.AccessToken, connection.UserAccessToken));
        }

        return (PostingResult.Failure(retry.Error),
            new FacebookTokenUpdate(refreshedPage.AccessToken, connection.UserAccessToken));
    }

    async Task<(string? Token, string? Error)> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        var url = GraphUrl("oauth/access_token") +
                  $"?client_id={Uri.EscapeDataString(_settings.FacebookAppId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&client_secret={Uri.EscapeDataString(_settings.FacebookAppSecret)}" +
                  $"&code={Uri.EscapeDataString(code)}";

        var response = await _http.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (null, DescribeGraphError(body, "Facebook token exchange failed."));

        var payload = System.Text.Json.JsonSerializer.Deserialize<FacebookTokenResponse>(body);
        return string.IsNullOrWhiteSpace(payload?.AccessToken)
            ? (null, "Facebook did not return an access token.")
            : (payload.AccessToken, null);
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

    public async Task<(string? UserToken, string? Error)> ObtainUserAccessTokenAsync(
        string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        var (shortLived, exchangeError) = await ExchangeCodeAsync(code, redirectUri, cancellationToken);
        if (shortLived is null)
            return (null, exchangeError ?? "Facebook did not return an access token. Try connecting again.");

        var userToken = await ExchangeForLongLivedUserTokenAsync(shortLived, cancellationToken);
        return string.IsNullOrWhiteSpace(userToken)
            ? (null, "Facebook connected but the session could not be extended. Try again.")
            : (userToken, null);
    }

    public async Task<List<FacebookPage>> GetManagedPagesAsync(string userAccessToken, CancellationToken cancellationToken = default)
    {
        var (pages, _) = await TryGetManagedPagesAsync(userAccessToken, cancellationToken);
        return pages;
    }

    public async Task<(List<FacebookPage> Pages, string? Error)> TryGetManagedPagesAsync(
        string userAccessToken, CancellationToken cancellationToken = default)
    {
        var url = GraphUrl("me/accounts") +
                  "?fields=id,name,access_token,username" +
                  $"&access_token={Uri.EscapeDataString(userAccessToken)}";

        var response = await _http.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return ([], DescribeGraphError(body, "Could not list your Facebook Pages from Meta."));

        var payload = System.Text.Json.JsonSerializer.Deserialize<FacebookPagesResponse>(body);
        var pages = payload?.Data?
            .Where(p => !string.IsNullOrWhiteSpace(p.Id) && !string.IsNullOrWhiteSpace(p.AccessToken))
            .Select(p => new FacebookPage(
                p.Id!,
                p.Name?.Trim() ?? "Facebook Page",
                p.Username?.Trim() ?? p.Id!,
                p.AccessToken!))
            .ToList() ?? [];

        if (pages.Count == 0)
            return ([], "Meta returned no Facebook Pages for this login. " + MetaBusinessIntegrationHelp);

        return (pages, null);
    }

    async Task<(bool Success, bool NeedsRefresh, string Error)> TryPostPhotoAsync(
        string pageId,
        string pageAccessToken,
        string postText,
        string? photoUrl,
        byte[]? photoBytes,
        string? photoMime,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(photoUrl))
        {
            var fromUrl = await TryPostPhotoUrlAsync(pageId, pageAccessToken, postText, photoUrl, cancellationToken);
            if (fromUrl.Success) return fromUrl;
        }

        if (photoBytes is { Length: > 0 })
            return await TryPostPhotoBytesAsync(pageId, pageAccessToken, postText, photoBytes, photoMime, cancellationToken);

        return (false, false, "Book cover image could not be attached.");
    }

    async Task<(bool Success, bool NeedsRefresh, string Error)> TryPostPhotoUrlAsync(
        string pageId, string pageAccessToken, string postText, string photoUrl, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["message"] = postText,
            ["url"] = photoUrl,
            ["access_token"] = pageAccessToken
        });

        var response = await _http.PostAsync(GraphUrl($"{pageId}/photos"), content, cancellationToken);
        if (response.IsSuccessStatusCode)
            return (true, false, "");

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ClassifyPostFailure(response.StatusCode, body);
    }

    async Task<(bool Success, bool NeedsRefresh, string Error)> TryPostPhotoBytesAsync(
        string pageId, string pageAccessToken, string postText, byte[] photoBytes, string? photoMime, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(postText), "message");
        form.Add(new StringContent(pageAccessToken), "access_token");
        var imageContent = new ByteArrayContent(photoBytes);
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(photoMime) ? "image/jpeg" : photoMime);
        form.Add(imageContent, "source", "cover.jpg");

        var response = await _http.PostAsync(GraphUrl($"{pageId}/photos"), form, cancellationToken);
        if (response.IsSuccessStatusCode)
            return (true, false, "");

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ClassifyPostFailure(response.StatusCode, body);
    }

    async Task<(bool Success, bool NeedsRefresh, string Error)> TryPostToPageFeedAsync(
        string pageId, string pageAccessToken, string postText, string? link, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>
        {
            ["message"] = postText,
            ["access_token"] = pageAccessToken
        };
        if (!string.IsNullOrWhiteSpace(link))
            fields["link"] = link;

        using var content = new FormUrlEncodedContent(fields);
        var response = await _http.PostAsync(GraphUrl($"{pageId}/feed"), content, cancellationToken);
        if (response.IsSuccessStatusCode)
            return (true, false, "");

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ClassifyPostFailure(response.StatusCode, body);
    }

    static (bool Success, bool NeedsRefresh, string Error) ClassifyPostFailure(System.Net.HttpStatusCode status, string body)
    {
        if (status == System.Net.HttpStatusCode.Unauthorized ||
            body.Contains("expired", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("Session has expired", StringComparison.OrdinalIgnoreCase))
            return (false, true, "Facebook Page token expired.");

        if (status == System.Net.HttpStatusCode.Forbidden)
            return (false, false, "Facebook API access denied. Confirm pages_manage_posts is enabled and you are a Page admin.");

        return (false, false, DescribePostError(status, body));
    }

    public static bool IsBookPromoterBrandPage(FacebookPage page) =>
        page.Name.Contains("Book Promoter", StringComparison.OrdinalIgnoreCase) ||
        page.Name.Contains("BookPromoter", StringComparison.OrdinalIgnoreCase) ||
        page.Id == "1210277848829044";

    static string DescribeGraphError(string body, string fallback)
    {
        try
        {
            var err = System.Text.Json.JsonSerializer.Deserialize<FacebookGraphErrorResponse>(body);
            if (!string.IsNullOrWhiteSpace(err?.Error?.Message))
                return err.Error.Message;
        }
        catch { /* ignore parse errors */ }
        return fallback;
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

    sealed class FacebookGraphErrorResponse
    {
        [JsonPropertyName("error")] public FacebookGraphError? Error { get; set; }
    }

    sealed class FacebookGraphError
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("code")] public int? Code { get; set; }
    }
}

record FacebookPage(string Id, string Name, string Handle, string AccessToken);

record FacebookPageConnection(FacebookPage Page, string UserAccessToken);

record FacebookTokenUpdate(string PageAccessToken, string UserAccessToken);

enum FacebookAuthStatus { Failed, Connected, NeedsPageSelection }

record FacebookAuthOutcome(
    FacebookAuthStatus Status,
    string? Error,
    FacebookPageConnection? Connection,
    IReadOnlyList<FacebookPage>? PagesToSelect,
    string? UserAccessToken)
{
    public static FacebookAuthOutcome Failed(string error) =>
        new(FacebookAuthStatus.Failed, error, null, null, null);

    public static FacebookAuthOutcome Connected(FacebookPageConnection connection) =>
        new(FacebookAuthStatus.Connected, null, connection, null, connection.UserAccessToken);

    public static FacebookAuthOutcome NeedsPageSelection(IReadOnlyList<FacebookPage> pages, string userAccessToken) =>
        new(FacebookAuthStatus.NeedsPageSelection, null, null, pages, userAccessToken);
}
