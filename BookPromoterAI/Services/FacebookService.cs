using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>Meta Graph API — Facebook Login + Page feed posting.</summary>
class FacebookService
{
    public const string CallbackPath = "/social-accounts/oauth-callback/facebook";
    /// <summary>Legacy Meta redirect URI (underscore path) — still accepted for token exchange.</summary>
    public const string LegacyCallbackPath = "/social_accounts/oauth_callback/facebook";
    // business_management is required for Pages linked to a Meta Business portfolio (Book Promoter AI).
    public const string Scopes = "pages_show_list,pages_manage_posts,pages_read_engagement,business_management,public_profile";
    /// <summary>Permissions to enable on the Meta Login Configuration (config mode only).</summary>
    public static readonly string[] LoginConfigurationPermissions =
        ["pages_show_list", "pages_manage_posts", "pages_read_engagement", "business_management", "public_profile"];
    public const string GraphVersion = "v22.0";

    public const string MetaBusinessIntegrationHelp =
        "Wrong Facebook account: log out of the BookPromoter business portfolio and sign in as Melanie Botha (personal). Remove AuthorPromoter AI at facebook.com/settings?tab=business_tools. On Meta click Edit settings (not Continue), pick Book Promoter AI Page only.";

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

    public (string AuthorizeUrl, string State, string FlowLabel) BuildAuthorizationUrl(
        string redirectUri, bool brandContext = false, bool forceScope = false, bool forceConfig = false)
    {
        var state = Guid.NewGuid().ToString("N");
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _settings.FacebookAppId,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["response_type"] = "code"
        };

        // Scope mode (Railway default) matches v1.9.44/57 — the last working owner connect path.
        // Config_id is only used when OAuthMode=config or the user explicitly picks Login for Business.
        var useConfigLogin = !forceScope && (forceConfig || _settings.FacebookUsesConfigLogin);

        string flowLabel;
        if (useConfigLogin)
        {
            if (!AppSettings.IsValidFacebookLoginConfigId(_settings.FacebookLoginConfigId))
                throw new InvalidOperationException(
                    brandContext
                        ? "Facebook Login Config ID is required for Login for Business."
                        : "Facebook Login Config ID is not configured.");
            query["config_id"] = _settings.FacebookLoginConfigId.Trim();
            // Meta User access token configs: config_id only (+ standard oauth params).
            // Do NOT send override_default_response_type or auth_type — both break User token configs.
            flowLabel = "Login for Business (config_id)";
        }
        else
        {
            query["scope"] = Scopes;
            // Never add auth_type for brand scope — Meta's Continue dialog loops without returning a code.
            if (!brandContext)
                query["auth_type"] = "rerequest";
            flowLabel = "Page permissions (scope)";
        }

        var url = $"https://www.facebook.com/{GraphVersion}/dialog/oauth?" +
                  string.Join("&", query.Select(kv =>
                      $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return (url, state, flowLabel);
    }

    /// <summary>Non-secret OAuth diagnostics for owner troubleshooting.</summary>
    public FacebookOAuthDiagnostics DescribeOAuth(string redirectUri, bool brandContext, bool forceScope, bool forceConfig)
    {
        try
        {
            var (_, _, flow) = BuildAuthorizationUrl(redirectUri, brandContext, forceScope, forceConfig);
            return new FacebookOAuthDiagnostics(
                true,
                flow,
                redirectUri,
                MaskId(_settings.FacebookAppId),
                AppSettings.IsValidFacebookLoginConfigId(_settings.FacebookLoginConfigId)
                    ? MaskId(_settings.FacebookLoginConfigId)
                    : "(not set)",
                _settings.FacebookOAuthMode,
                null);
        }
        catch (Exception ex)
        {
            return new FacebookOAuthDiagnostics(false, "", redirectUri, MaskId(_settings.FacebookAppId), "", _settings.FacebookOAuthMode, ex.Message);
        }
    }

    static string MaskId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "(missing)";
        var id = value.Trim();
        return id.Length > 8 ? $"{id[..4]}...{id[^4..]}" : id;
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
                    $"Meta only granted \"{pages[0].Name}\" (not Book Promoter AI). " +
                    "Your personal Facebook account must be a Page admin on Book Promoter AI (Page ID 1210277848829044). " +
                    "In Meta Business Suite → Settings → Pages → Book Promoter AI → Page access, add Melanie Botha with Full control, then reconnect. " +
                    "Do not connect Melanie Botha Novels for brand posting.");
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

record FacebookOAuthDiagnostics(
    bool Ready,
    string FlowLabel,
    string RedirectUri,
    string AppIdMasked,
    string ConfigIdMasked,
    string OAuthMode,
    string? Error);

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
