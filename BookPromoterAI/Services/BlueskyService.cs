using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>AT Protocol client for Bluesky session + posting (app password auth).</summary>
class BlueskyService
{
    readonly HttpClient _http;

    public BlueskyService(HttpClient http) => _http = http;

    public async Task<(bool Ok, string Error, BlueskySession? Session)> CreateSessionAsync(
        string handle, string appPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(handle) || string.IsNullOrWhiteSpace(appPassword))
            return (false, "Enter your Bluesky handle and app password.", null);

        var payload = new { identifier = NormalizeHandle(handle), password = appPassword.Trim() };
        var response = await _http.PostAsJsonAsync("/xrpc/com.atproto.server.createSession", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            return (false, DescribeSessionError(response.StatusCode, detail), null);
        }

        var session = await response.Content.ReadFromJsonAsync<BlueskySessionResponse>(cancellationToken: cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.AccessJwt) || string.IsNullOrWhiteSpace(session.Did))
            return (false, "Bluesky returned an unexpected response. Try again.", null);

        return (true, "", new BlueskySession(session.AccessJwt, session.RefreshJwt ?? "", session.Did, session.Handle ?? NormalizeHandle(handle)));
    }

    public async Task<(PostingResult Result, BlueskySession? UpdatedSession)> PostAsync(
        BlueskySession session, string postText, CancellationToken cancellationToken = default)
    {
        var active = session;
        var firstTry = await TryCreatePostAsync(active, postText, cancellationToken);
        if (firstTry.Result.Success)
            return (firstTry.Result, firstTry.UpdatedSession);

        if (!firstTry.NeedsRefresh || string.IsNullOrWhiteSpace(active.RefreshJwt))
            return (firstTry.Result, firstTry.UpdatedSession);

        var refreshed = await RefreshSessionAsync(active.RefreshJwt, cancellationToken);
        if (!refreshed.Ok || refreshed.Session is null)
            return (PostingResult.Failure("Bluesky session expired. Reconnect your Bluesky account in My Account."), null);

        active = refreshed.Session;
        var retry = await TryCreatePostAsync(active, postText, cancellationToken);
        return (retry.Result, retry.UpdatedSession ?? active);
    }

    async Task<(PostingResult Result, BlueskySession? UpdatedSession, bool NeedsRefresh)> TryCreatePostAsync(
        BlueskySession session, string postText, CancellationToken cancellationToken)
    {
        var postRecord = new Dictionary<string, object?>
        {
            ["$type"] = "app.bsky.feed.post",
            ["text"] = postText,
            ["createdAt"] = DateTime.UtcNow.ToString("o"),
            ["langs"] = new[] { "en" }
        };

        var facets = BlueskyRichText.BuildFacets(postText);
        if (facets.Count > 0)
            postRecord["facets"] = facets;

        var recordBody = new Dictionary<string, object?>
        {
            ["repo"] = session.Did,
            ["collection"] = "app.bsky.feed.post",
            ["record"] = postRecord
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.repo.createRecord");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessJwt);
        request.Content = JsonContent.Create(recordBody, options: BlueskyJson.Options);

        var response = await _http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return (PostingResult.Ok("Posted to Bluesky."), session, false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.BadRequest
            && body.Contains("ExpiredToken", StringComparison.OrdinalIgnoreCase))
            return (PostingResult.Failure("Bluesky session expired."), null, true);

        return (PostingResult.Failure(DescribePostError(response.StatusCode, body)), null, false);
    }

    async Task<(bool Ok, BlueskySession? Session)> RefreshSessionAsync(string refreshJwt, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/xrpc/com.atproto.server.refreshSession");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshJwt);
        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, null);

        var session = await response.Content.ReadFromJsonAsync<BlueskySessionResponse>(cancellationToken: cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.AccessJwt) || string.IsNullOrWhiteSpace(session.Did))
            return (false, null);

        return (true, new BlueskySession(
            session.AccessJwt,
            session.RefreshJwt ?? refreshJwt,
            session.Did,
            session.Handle ?? ""));
    }

    static string NormalizeHandle(string handle)
    {
        var clean = handle.Trim().TrimStart('@');
        return clean.Contains('.', StringComparison.Ordinal) ? clean : $"{clean}.bsky.social";
    }

    static string DescribeSessionError(System.Net.HttpStatusCode status, string detail)
    {
        if (detail.Contains("AuthenticationRequired", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("Invalid identifier", StringComparison.OrdinalIgnoreCase)
            || status == System.Net.HttpStatusCode.Unauthorized)
            return "Bluesky rejected the handle or app password. Create a new app password in Bluesky Settings and try again.";

        return "Could not connect to Bluesky. Check your handle and app password, then try again.";
    }

    static string DescribePostError(System.Net.HttpStatusCode status, string detail)
    {
        if (detail.Contains("InvalidRequest", StringComparison.OrdinalIgnoreCase) && detail.Contains("record", StringComparison.OrdinalIgnoreCase))
            return "Bluesky rejected the post format. Try regenerating the ad.";
        return $"Bluesky error ({(int)status}). Try again or reconnect your account.";
    }

    sealed class BlueskySessionResponse
    {
        [JsonPropertyName("accessJwt")] public string? AccessJwt { get; set; }
        [JsonPropertyName("refreshJwt")] public string? RefreshJwt { get; set; }
        [JsonPropertyName("did")] public string? Did { get; set; }
        [JsonPropertyName("handle")] public string? Handle { get; set; }
    }

    static class BlueskyJson
    {
        public static readonly System.Text.Json.JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}

record BlueskySession(string AccessJwt, string RefreshJwt, string Did, string Handle);

class PostingOutcome
{
    public PostingResult Result { get; init; } = PostingResult.Failure("Unknown error.");
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
}
