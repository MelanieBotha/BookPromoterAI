using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>WordPress REST API posting via per-site application passwords.</summary>
class WordPressService
{
    readonly HttpClient _http;

    public WordPressService(HttpClient http) => _http = http;

    public static string NormalizeSiteUrl(string siteUrl)
    {
        var trimmed = siteUrl.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(trimmed))
            return "";

        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            trimmed = "https://" + trimmed;

        return trimmed;
    }

    public async Task<(bool Ok, string Error, WordPressConnection? Connection)> VerifyAsync(
        string siteUrl,
        string username,
        string appPassword,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = NormalizeSiteUrl(siteUrl);
        if (!UrlSafety.IsSafeRedirect(baseUrl))
            return (false, "Enter a valid WordPress site URL (https://yourblog.com).", null);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(appPassword))
            return (false, "Enter your WordPress username and application password.", null);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/wp-json/wp/v2/users/me?context=edit");
        request.Headers.Authorization = BasicAuth(username.Trim(), appPassword.Trim());

        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, DescribeError(response.StatusCode, body), null);

        var user = System.Text.Json.JsonSerializer.Deserialize<WordPressUserResponse>(body);
        if (user is null || string.IsNullOrWhiteSpace(user.Name))
            return (false, "WordPress returned an unexpected response. Try again.", null);

        return (true, "", new WordPressConnection(baseUrl, username.Trim(), appPassword.Trim(), user.Name.Trim()));
    }

    public async Task<PostingResult> PostAsync(
        WordPressConnection connection,
        string title,
        string htmlContent,
        byte[]? imageBytes = null,
        string? imageMime = null,
        string? imageFileName = null,
        CancellationToken cancellationToken = default)
    {
        int? featuredMediaId = null;
        if (imageBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(imageMime))
        {
            var upload = await UploadMediaAsync(connection, imageBytes, imageMime, imageFileName, cancellationToken);
            if (!upload.Ok)
                return PostingResult.Failure(upload.Error);
            featuredMediaId = upload.MediaId;
        }

        var payload = new Dictionary<string, object?>
        {
            ["title"] = title,
            ["content"] = htmlContent,
            ["status"] = "publish"
        };
        if (featuredMediaId is int mediaId)
            payload["featured_media"] = mediaId;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{connection.SiteUrl}/wp-json/wp/v2/posts");
        request.Headers.Authorization = BasicAuth(connection.Username, connection.AppPassword);
        request.Content = JsonContent.Create(payload);

        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return PostingResult.Failure($"WordPress post failed: {DescribeError(response.StatusCode, body)}");

        var post = System.Text.Json.JsonSerializer.Deserialize<WordPressPostResponse>(body);
        var link = post?.Link;
        return PostingResult.LiveOk(
            featuredMediaId is not null ? "Posted to WordPress with featured image." : "Posted to WordPress.",
            link);
    }

    async Task<(bool Ok, string Error, int? MediaId)> UploadMediaAsync(
        WordPressConnection connection,
        byte[] imageBytes,
        string imageMime,
        string? fileName,
        CancellationToken cancellationToken)
    {
        var safeName = string.IsNullOrWhiteSpace(fileName) ? "cover.jpg" : Path.GetFileName(fileName);
        using var content = new ByteArrayContent(imageBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(imageMime);
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = safeName
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{connection.SiteUrl}/wp-json/wp/v2/media");
        request.Headers.Authorization = BasicAuth(connection.Username, connection.AppPassword);
        request.Content = content;

        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, $"WordPress image upload failed: {DescribeError(response.StatusCode, body)}", null);

        var media = System.Text.Json.JsonSerializer.Deserialize<WordPressMediaResponse>(body);
        return media?.Id is int id && id > 0
            ? (true, "", id)
            : (false, "WordPress image upload returned an unexpected response.", null);
    }

    static AuthenticationHeaderValue BasicAuth(string username, string appPassword)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{appPassword}"));
        return new AuthenticationHeaderValue("Basic", token);
    }

    static string DescribeError(System.Net.HttpStatusCode status, string body)
    {
        if (status is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            return "WordPress rejected the login. Check your site URL, username, and application password.";
        if (body.Contains("rest_cannot_create", StringComparison.OrdinalIgnoreCase))
            return "This WordPress user cannot create posts. Use an Editor or Administrator account.";
        if (body.Length > 180)
            body = body[..180] + "…";
        return string.IsNullOrWhiteSpace(body) ? ((int)status).ToString() : body;
    }
}

sealed record WordPressConnection(string SiteUrl, string Username, string AppPassword, string DisplayName);

sealed class WordPressUserResponse
{
    [JsonPropertyName("name")] public string? Name { get; set; }
}

sealed class WordPressPostResponse
{
    [JsonPropertyName("link")] public string? Link { get; set; }
}

sealed class WordPressMediaResponse
{
    [JsonPropertyName("id")] public int Id { get; set; }
}
