using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>Reddit OAuth 2.0 + submit API for text posts.</summary>
class RedditService
{
    public const string CallbackPath = "/social-accounts/oauth-callback/Reddit";
    public const string Scopes = "submit identity read";
    public const string UserAgent = "BookPromoterAI/1.0 (author social posting; contact bookpromoterai@gmail.com)";

    readonly HttpClient _http;
    readonly AppSettings _settings;

    public RedditService(HttpClient http, AppSettings settings)
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
            ["client_id"] = _settings.RedditClientId,
            ["response_type"] = "code",
            ["state"] = state,
            ["redirect_uri"] = redirectUri,
            ["duration"] = "permanent",
            ["scope"] = Scopes
        };
        var authorizeUrl = "https://www.reddit.com/api/v1/authorize?" +
                           string.Join("&", query.Select(kv =>
                               $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return (authorizeUrl, state);
    }

    public async Task<(bool Ok, string Error, RedditTokenSet? Tokens, RedditUser? User)> CompleteAuthorizationAsync(
        string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        var tokens = await ExchangeCodeAsync(code, redirectUri, cancellationToken);
        if (tokens is null)
            return (false, "Reddit did not return an access token. Try connecting again.", null, null);

        var user = await GetCurrentUserAsync(tokens.AccessToken, cancellationToken);
        if (user is null)
            return (false, "Connected to Reddit but could not read your profile. Try again.", null, null);

        return (true, "", tokens, user);
    }

    public async Task<PostingResult> PostAsync(
        RedditTokenSet tokens,
        string subreddit,
        string postText,
        CancellationToken cancellationToken = default)
    {
        var sr = NormalizeSubreddit(subreddit);
        if (string.IsNullOrWhiteSpace(sr))
            return PostingResult.Failure("Reddit subreddit is missing. Reconnect and enter a subreddit name.");

        var (title, body) = SplitTitleAndBody(postText);
        if (string.IsNullOrWhiteSpace(title))
            return PostingResult.Failure("Reddit post needs a title. Add a short first line to the caption.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.reddit.com/api/submit");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["sr"] = sr,
            ["kind"] = "self",
            ["title"] = title,
            ["text"] = body,
            ["resubmit"] = "true"
        });

        var response = await _http.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return PostingResult.Failure(DescribeSubmitError(raw, response.StatusCode));

        try
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<RedditSubmitResponse>(raw);
            if (payload?.Json?.Errors is { Count: > 0 })
            {
                var msg = string.Join("; ", payload.Json.Errors.Select(e => string.Join(", ", e)));
                return PostingResult.Failure(string.IsNullOrWhiteSpace(msg) ? "Reddit rejected the post." : msg);
            }
        }
        catch { /* non-json success is ok */ }

        return PostingResult.LiveOk($"Posted to r/{sr} on Reddit.");
    }

    async Task<RedditTokenSet?> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.reddit.com/api/v1/access_token");
        var basic = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{_settings.RedditClientId}:{_settings.RedditClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri
        });

        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<RedditTokenResponse>(cancellationToken: cancellationToken);
        return string.IsNullOrWhiteSpace(payload?.AccessToken)
            ? null
            : new RedditTokenSet(payload.AccessToken, payload.RefreshToken ?? "");
    }

    async Task<RedditUser?> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://oauth.reddit.com/api/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd(UserAgent);

        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content.ReadFromJsonAsync<RedditMeResponse>(cancellationToken: cancellationToken);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Name))
            return null;

        return new RedditUser(payload.Id ?? payload.Name, payload.Name);
    }

    public static string NormalizeSubreddit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var trimmed = value.Trim().TrimStart('r', 'R', '/');
        return trimmed.Split('/')[0].Trim();
    }

    static (string Title, string Body) SplitTitleAndBody(string postText)
    {
        var text = postText.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return ("", "");

        var breakIdx = text.IndexOf('\n');
        if (breakIdx < 0)
            return (TruncateTitle(text), "");

        var title = text[..breakIdx].Trim();
        var body = text[(breakIdx + 1)..].Trim();
        return (TruncateTitle(title), body);
    }

    static string TruncateTitle(string title)
    {
        const int max = 280;
        if (title.Length <= max) return title;
        return title[..max].TrimEnd() + "…";
    }

    static string DescribeSubmitError(string body, System.Net.HttpStatusCode status)
    {
        if (body.Contains("RATELIMIT", StringComparison.OrdinalIgnoreCase))
            return "Reddit rate limit reached. Wait a few minutes and try again.";
        if (body.Contains("SUBREDDIT_NOTALLOWED", StringComparison.OrdinalIgnoreCase))
            return "You cannot post to that subreddit with this account. Pick a subreddit you can submit to.";
        return $"Reddit error ({(int)status}). Check the subreddit name and try again.";
    }

    sealed class RedditTokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
    }

    sealed class RedditMeResponse
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    sealed class RedditSubmitResponse
    {
        [JsonPropertyName("json")] public RedditSubmitJson? Json { get; set; }
    }

    sealed class RedditSubmitJson
    {
        [JsonPropertyName("errors")] public List<List<string>>? Errors { get; set; }
    }
}

record RedditTokenSet(string AccessToken, string RefreshToken);

record RedditUser(string Id, string Username);
