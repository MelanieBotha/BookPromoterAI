using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>Medium REST API posting via per-user integration tokens.</summary>
class MediumService
{
    const string ApiBase = "https://api.medium.com/v1";

    readonly HttpClient _http;

    public MediumService(HttpClient http) => _http = http;

    public async Task<(bool Ok, string Error, MediumConnection? Connection)> VerifyAsync(
        string integrationToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(integrationToken))
            return (false, "Enter your Medium integration token.", null);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/me");
        request.Headers.Authorization = BearerAuth(integrationToken.Trim());

        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, DescribeError(response.StatusCode, body), null);

        var envelope = System.Text.Json.JsonSerializer.Deserialize<MediumEnvelope<MediumUserResponse>>(body);
        var user = envelope?.Data;
        if (user is null || string.IsNullOrWhiteSpace(user.Id))
            return (false, "Medium returned an unexpected response. Try again.", null);

        var displayName = string.IsNullOrWhiteSpace(user.Name) ? user.Username ?? "Medium" : user.Name.Trim();
        return (true, "", new MediumConnection(
            user.Id.Trim(),
            user.Username?.Trim() ?? "",
            displayName,
            integrationToken.Trim()));
    }

    public async Task<PostingResult> PostAsync(
        MediumConnection connection,
        string title,
        string htmlContent,
        IReadOnlyList<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["title"] = title,
            ["contentFormat"] = "html",
            ["content"] = htmlContent,
            ["publishStatus"] = "public",
            ["notifyFollowers"] = true
        };

        var tagList = NormalizeTags(tags);
        if (tagList.Count > 0)
            payload["tags"] = tagList;

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{ApiBase}/users/{Uri.EscapeDataString(connection.UserId)}/posts");
        request.Headers.Authorization = BearerAuth(connection.IntegrationToken);
        request.Content = JsonContent.Create(payload);

        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return PostingResult.Failure($"Medium post failed: {DescribeError(response.StatusCode, body)}");

        var envelope = System.Text.Json.JsonSerializer.Deserialize<MediumEnvelope<MediumPostResponse>>(body);
        var post = envelope?.Data;
        return PostingResult.LiveOk("Posted to Medium.", post?.Url);
    }

    public async Task<(bool Ok, string Error, string? Url)> UploadImageAsync(
        MediumConnection connection,
        byte[] imageBytes,
        string imageMime,
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        if (imageBytes.Length == 0)
            return (false, "Image is empty.", null);

        var safeName = string.IsNullOrWhiteSpace(fileName) ? "cover.jpg" : Path.GetFileName(fileName);
        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(imageMime);
        content.Add(imageContent, "image", safeName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/images");
        request.Headers.Authorization = BearerAuth(connection.IntegrationToken);
        request.Content = content;

        var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, $"Medium image upload failed: {DescribeError(response.StatusCode, body)}", null);

        var envelope = System.Text.Json.JsonSerializer.Deserialize<MediumEnvelope<MediumImageResponse>>(body);
        return string.IsNullOrWhiteSpace(envelope?.Data?.Url)
            ? (false, "Medium image upload returned an unexpected response.", null)
            : (true, "", envelope.Data.Url);
    }

    static List<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null || tags.Count == 0) return [];

        var result = new List<string>(3);
        foreach (var raw in tags)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var tag = raw.Trim();
            if (tag.Length > 25) tag = tag[..25].TrimEnd();
            if (tag.Length == 0) continue;
            if (result.Contains(tag, StringComparer.OrdinalIgnoreCase)) continue;
            result.Add(tag);
            if (result.Count == 3) break;
        }

        return result;
    }

    static AuthenticationHeaderValue BearerAuth(string token) =>
        new("Bearer", token.Trim());

    static string DescribeError(System.Net.HttpStatusCode status, string body)
    {
        if (status is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            return "Medium rejected this integration token. Medium no longer issues new tokens — you need a legacy token from medium.com/me/settings → Integration tokens. If that section is missing, API posting is not available on your account.";
        if (body.Length > 180)
            body = body[..180] + "…";
        return string.IsNullOrWhiteSpace(body) ? ((int)status).ToString() : body;
    }
}

sealed record MediumConnection(string UserId, string Username, string DisplayName, string IntegrationToken);

sealed class MediumEnvelope<T>
{
    [JsonPropertyName("data")] public T? Data { get; set; }
}

sealed class MediumUserResponse
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("username")] public string? Username { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
}

sealed class MediumPostResponse
{
    [JsonPropertyName("url")] public string? Url { get; set; }
}

sealed class MediumImageResponse
{
    [JsonPropertyName("url")] public string? Url { get; set; }
}
