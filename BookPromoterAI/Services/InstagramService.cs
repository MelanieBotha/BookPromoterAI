using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>Meta Graph API — Instagram Business account discovery and content publishing.</summary>
class InstagramService
{
    public const string CallbackPath = "/social-accounts/oauth-callback/instagram";
    /// <summary>OAuth redirect URI — same as Facebook so Meta returns an authorization code reliably.</summary>
    public const string OAuthRedirectPath = FacebookService.CallbackPath;
    public const string Scopes =
        "pages_show_list,pages_read_engagement,public_profile,instagram_basic,instagram_content_publish";

    readonly HttpClient _http;
    readonly FacebookService _facebook;

    public InstagramService(HttpClient http, FacebookService facebook)
    {
        _http = http;
        _facebook = facebook;
    }

    public static string CallbackUrl(string appBaseUrl) =>
        $"{appBaseUrl.TrimEnd('/')}{CallbackPath}";

    static string GraphUrl(string path) =>
        $"https://graph.facebook.com/{FacebookService.GraphVersion}/{path.TrimStart('/')}";

    public (string AuthorizeUrl, string State) BuildAuthorizationUrl(string redirectUri, bool brandContext = false) =>
        _facebook.BuildAuthorizationUrl(redirectUri, brandContext, forInstagram: true);

    public async Task<InstagramAuthOutcome> CompleteAuthorizationAsync(
        string userAccessToken, bool brandContext, CancellationToken cancellationToken = default)
    {
        var (pages, pagesError) = await _facebook.TryGetManagedPagesAsync(userAccessToken, cancellationToken);
        if (pages.Count == 0)
            return InstagramAuthOutcome.Failed(pagesError ??
                "No Facebook Pages found. Instagram Business accounts must be linked to a Facebook Page.");

        var linked = new List<InstagramPageLink>();
        foreach (var page in pages)
        {
            var ig = await GetInstagramBusinessAccountAsync(page.Id, page.AccessToken, cancellationToken);
            if (ig is not null)
                linked.Add(new InstagramPageLink(page, ig));
        }

        if (linked.Count == 0)
            return InstagramAuthOutcome.Failed(
                "No Instagram Business or Creator accounts found. In Meta Business Suite, link your Instagram account to a Facebook Page you manage, then try again.");

        if (brandContext)
        {
            var link = PickBrandLink(linked);
            return link is null
                ? InstagramAuthOutcome.Failed("Could not select an Instagram account to connect.")
                : InstagramAuthOutcome.Connected(new InstagramConnection(link, userAccessToken));
        }

        var authorLinks = linked.Where(l => !FacebookService.IsBookPromoterBrandPage(l.Page)).ToList();
        if (authorLinks.Count == 0)
            return InstagramAuthOutcome.Failed(
                "No author Instagram accounts found (only the BookPromoter AI business Page was detected). Link your author Instagram to your own Facebook Page, or click Edit settings on the Meta dialog to choose a different Page.");

        if (authorLinks.Count == 1)
            return InstagramAuthOutcome.Connected(new InstagramConnection(authorLinks[0], userAccessToken));

        return InstagramAuthOutcome.NeedsAccountSelection(authorLinks, userAccessToken);
    }

    /// <summary>Discover IG via an already-connected Facebook Page (page token), without listing pages from the user token.</summary>
    public async Task<InstagramAuthOutcome> CompleteAuthorizationFromConnectedFacebookAsync(
        string pageId,
        string pageAccessToken,
        string pageName,
        string pageHandle,
        string? userAccessToken,
        bool brandContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(pageAccessToken))
            return InstagramAuthOutcome.Failed("Facebook Page token missing. Reconnect Facebook in Brand Social Accounts.");

        var page = new FacebookPage(pageId, pageName, pageHandle, pageAccessToken);
        var (ig, igError) = await TryGetInstagramBusinessAccountAsync(page.Id, page.AccessToken, cancellationToken);
        if (ig is null)
            return InstagramAuthOutcome.Failed(igError ??
                "No Instagram account is linked to your Facebook Page. In Meta Business Suite, link your Instagram Business account to the Book Promoter AI Page, then try again.");

        var userToken = userAccessToken ?? "";
        var link = new InstagramPageLink(page, ig);
        if (brandContext)
            return InstagramAuthOutcome.Connected(new InstagramConnection(link, userToken));

        if (FacebookService.IsBookPromoterBrandPage(page))
            return InstagramAuthOutcome.Failed(
                "Only the BookPromoter AI business Page was detected. Link your author Instagram to your own Facebook Page.");

        return InstagramAuthOutcome.Connected(new InstagramConnection(link, userToken));
    }

    public async Task<InstagramBusinessAccount?> GetInstagramBusinessAccountAsync(
        string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
    {
        var (account, _) = await TryGetInstagramBusinessAccountAsync(pageId, pageAccessToken, cancellationToken);
        return account;
    }

    public async Task<(InstagramBusinessAccount? Account, string? Error)> TryGetInstagramBusinessAccountAsync(
        string pageId, string pageAccessToken, CancellationToken cancellationToken = default)
    {
        var url = GraphUrl(pageId) +
                  "?fields=instagram_business_account{id,username,name}" +
                  $"&access_token={Uri.EscapeDataString(pageAccessToken)}";

        var response = await _http.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (null, DescribeGraphError(body, "Could not read Instagram from Facebook Page. Reconnect Facebook and try again."));

        var payload = System.Text.Json.JsonSerializer.Deserialize<InstagramPageResponse>(body);
        var ig = payload?.InstagramBusinessAccount;
        if (ig is null || string.IsNullOrWhiteSpace(ig.Id))
            return (null, "No Instagram account is linked to this Facebook Page. Link IG in Meta Business Suite → Settings → Instagram accounts.");

        return (new InstagramBusinessAccount(
            ig.Id,
            ig.Username?.Trim() ?? ig.Id,
            ig.Name?.Trim()), null);
    }

    static string? DescribeGraphError(string body, string fallback)
    {
        try
        {
            var err = System.Text.Json.JsonSerializer.Deserialize<InstagramGraphErrorResponse>(body);
            if (!string.IsNullOrWhiteSpace(err?.Error?.Message))
                return err.Error.Message;
        }
        catch { /* ignore */ }
        return fallback;
    }

    public async Task<(PostingResult Result, string? RefreshedPageToken)> PostAsync(
        InstagramConnection connection,
        string caption,
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return (PostingResult.Failure("Instagram requires a book cover image. Add a cover to your book and try again."), null);

        var first = await TryPublishAsync(connection, caption, imageUrl, cancellationToken);
        if (first.Success)
            return (PostingResult.LiveOk("Posted to Instagram with book cover."), null);

        if (!first.NeedsRefresh || string.IsNullOrWhiteSpace(connection.UserAccessToken))
            return (PostingResult.Failure(first.Error), null);

        var pages = await _facebook.GetManagedPagesAsync(connection.UserAccessToken, cancellationToken);
        var refreshedPage = await FindPageForInstagramAsync(pages, connection.Link.Instagram.Id, cancellationToken);
        if (refreshedPage is null || string.IsNullOrWhiteSpace(refreshedPage.AccessToken))
            return (PostingResult.Failure("Instagram Page access expired. Reconnect your Instagram account in My Account."), null);

        var refreshedLink = new InstagramPageLink(refreshedPage, connection.Link.Instagram);
        var refreshedConnection = new InstagramConnection(refreshedLink, connection.UserAccessToken);
        var retry = await TryPublishAsync(refreshedConnection, caption, imageUrl, cancellationToken);
        if (retry.Success)
            return (PostingResult.LiveOk("Posted to Instagram with book cover."), refreshedPage.AccessToken);

        return (PostingResult.Failure(retry.Error), refreshedPage.AccessToken);
    }

    async Task<FacebookPage?> FindPageForInstagramAsync(
        IReadOnlyList<FacebookPage> pages, string igUserId, CancellationToken cancellationToken)
    {
        foreach (var page in pages)
        {
            var ig = await GetInstagramBusinessAccountAsync(page.Id, page.AccessToken, cancellationToken);
            if (ig?.Id == igUserId)
                return page;
        }

        return null;
    }

    async Task<(bool Success, bool NeedsRefresh, string Error)> TryPublishAsync(
        InstagramConnection connection, string caption, string imageUrl, CancellationToken cancellationToken)
    {
        var igUserId = connection.Link.Instagram.Id;
        var pageToken = connection.Link.Page.AccessToken;

        var containerId = await CreateMediaContainerAsync(igUserId, pageToken, caption, imageUrl, cancellationToken);
        if (string.IsNullOrWhiteSpace(containerId))
            return (false, false, "Instagram could not prepare the image. Confirm the book cover is available.");

        if (!await WaitForContainerReadyAsync(containerId, pageToken, cancellationToken))
            return (false, false, "Instagram is still processing the image. Try again in a minute.");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["creation_id"] = containerId,
            ["access_token"] = pageToken
        });

        var response = await _http.PostAsync(GraphUrl($"{igUserId}/media_publish"), content, cancellationToken);
        if (response.IsSuccessStatusCode)
            return (true, false, "");

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ClassifyFailure(response.StatusCode, body);
    }

    async Task<string?> CreateMediaContainerAsync(
        string igUserId, string pageToken, string caption, string imageUrl, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["image_url"] = imageUrl,
            ["caption"] = caption,
            ["access_token"] = pageToken
        });

        var response = await _http.PostAsync(GraphUrl($"{igUserId}/media"), content, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<InstagramMediaResponse>(cancellationToken: cancellationToken);
        return payload?.Id;
    }

    async Task<bool> WaitForContainerReadyAsync(string containerId, string pageToken, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var url = GraphUrl(containerId) +
                      "?fields=status_code" +
                      $"&access_token={Uri.EscapeDataString(pageToken)}";

            var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return attempt > 0;

            var payload = await response.Content.ReadFromJsonAsync<InstagramContainerStatusResponse>(cancellationToken: cancellationToken);
            var status = payload?.StatusCode?.Trim();
            if (string.Equals(status, "FINISHED", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(status, "ERROR", StringComparison.OrdinalIgnoreCase))
                return false;

            await Task.Delay(TimeSpan.FromSeconds(1.5), cancellationToken);
        }

        return true;
    }

    static InstagramPageLink? PickBrandLink(IReadOnlyList<InstagramPageLink> links)
    {
        if (links.Count == 0) return null;
        var brand = links.FirstOrDefault(l => FacebookService.IsBookPromoterBrandPage(l.Page));
        return brand ?? links[0];
    }

    static (bool Success, bool NeedsRefresh, string Error) ClassifyFailure(System.Net.HttpStatusCode status, string body)
    {
        if (status == System.Net.HttpStatusCode.Unauthorized ||
            body.Contains("expired", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("Session has expired", StringComparison.OrdinalIgnoreCase))
            return (false, true, "Instagram Page token expired.");

        if (status == System.Net.HttpStatusCode.Forbidden)
            return (false, false, "Instagram API access denied. Confirm instagram_content_publish is enabled and your account is a Business or Creator profile linked to a Facebook Page.");

        return (false, false, DescribeError(body));
    }

    static string DescribeError(string body)
    {
        try
        {
            var err = System.Text.Json.JsonSerializer.Deserialize<InstagramGraphErrorResponse>(body);
            if (!string.IsNullOrWhiteSpace(err?.Error?.Message))
                return err.Error.Message;
        }
        catch { /* ignore parse errors */ }
        return "Instagram posting failed. Try again or reconnect your account.";
    }

    sealed class InstagramPageResponse
    {
        [JsonPropertyName("instagram_business_account")] public InstagramAccountData? InstagramBusinessAccount { get; set; }
    }

    sealed class InstagramAccountData
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("username")] public string? Username { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    sealed class InstagramMediaResponse
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
    }

    sealed class InstagramContainerStatusResponse
    {
        [JsonPropertyName("status_code")] public string? StatusCode { get; set; }
    }

    sealed class InstagramGraphErrorResponse
    {
        [JsonPropertyName("error")] public InstagramGraphError? Error { get; set; }
    }

    sealed class InstagramGraphError
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
    }
}

record InstagramBusinessAccount(string Id, string Username, string? Name);

record InstagramPageLink(FacebookPage Page, InstagramBusinessAccount Instagram);

record InstagramConnection(InstagramPageLink Link, string UserAccessToken);

enum InstagramAuthStatus { Failed, Connected, NeedsAccountSelection }

record InstagramAuthOutcome(
    InstagramAuthStatus Status,
    string? Error,
    InstagramConnection? Connection,
    IReadOnlyList<InstagramPageLink>? LinksToSelect,
    string? UserAccessToken)
{
    public static InstagramAuthOutcome Failed(string error) =>
        new(InstagramAuthStatus.Failed, error, null, null, null);

    public static InstagramAuthOutcome Connected(InstagramConnection connection) =>
        new(InstagramAuthStatus.Connected, null, connection, null, connection.UserAccessToken);

    public static InstagramAuthOutcome NeedsAccountSelection(IReadOnlyList<InstagramPageLink> links, string userAccessToken) =>
        new(InstagramAuthStatus.NeedsAccountSelection, null, null, links, userAccessToken);
}
