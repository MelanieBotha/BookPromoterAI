using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>Discord incoming webhooks and Telegram Bot API — no OAuth app approval needed.</summary>
class DiscordTelegramPostingService
{
    readonly HttpClient _http;

    public DiscordTelegramPostingService(HttpClient http) => _http = http;

    public static bool IsDiscordWebhook(string? token) =>
        !string.IsNullOrWhiteSpace(token) &&
        Uri.TryCreate(token.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) &&
        (uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("discordapp.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("ptb.discord.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("canary.discord.com", StringComparison.OrdinalIgnoreCase)) &&
        Regex.IsMatch(uri.AbsolutePath, "^/api(?:/v\\d+)?/webhooks/[^/]+/[^/]+/?$", RegexOptions.IgnoreCase);

    public async Task<PostingResult> PostDiscordWebhookAsync(
        string webhookUrl,
        string postText,
        byte[]? imageBytes = null,
        string? imageMime = null,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsDiscordWebhook(webhookUrl))
            return PostingResult.Failure("Discord webhook URL looks invalid. Reconnect in My Account.");

        using HttpResponseMessage response = imageBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(imageMime)
            ? await PostDiscordMultipartAsync(webhookUrl.Trim(), postText, imageBytes, imageMime, fileName, cancellationToken)
            : await _http.PostAsJsonAsync(webhookUrl.Trim(), new { content = Truncate(postText, 2000) }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            return PostingResult.Failure($"Discord webhook failed: {err}");
        }

        return PostingResult.LiveOk("Posted to Discord channel.");
    }

    public async Task<(bool Ok, string Error, string? BotUsername)> ValidateTelegramBotAsync(
        string botToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(botToken))
            return (false, "Enter your Telegram bot token.", null);

        var response = await _http.GetAsync($"https://api.telegram.org/bot{botToken.Trim()}/getMe", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, "Telegram bot token was rejected. Check the token from @BotFather.", null);

        var payload = await response.Content.ReadFromJsonAsync<TelegramResponse<TelegramUser>>(cancellationToken: cancellationToken);
        if (payload?.Ok != true || payload.Result is null)
            return (false, "Could not verify Telegram bot.", null);

        return (true, "", payload.Result.Username);
    }

    public async Task<PostingResult> PostTelegramAsync(
        string botToken, string chatId, string postText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
            return PostingResult.Failure("Telegram bot token or chat ID is missing. Reconnect in My Account.");

        var url = $"https://api.telegram.org/bot{botToken.Trim()}/sendMessage";
        var form = new Dictionary<string, string>
        {
            ["chat_id"] = chatId.Trim(),
            ["text"] = Truncate(postText, 4096),
            ["disable_web_page_preview"] = "false"
        };
        var response = await _http.PostAsync(url, new FormUrlEncodedContent(form), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return PostingResult.Failure($"Telegram post failed: {body}");

        var payload = System.Text.Json.JsonSerializer.Deserialize<TelegramResponse<TelegramMessage>>(body);
        if (payload?.Ok != true)
            return PostingResult.Failure($"Telegram rejected the post: {body}");

        return PostingResult.LiveOk("Posted to Telegram.");
    }

    static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";

    async Task<HttpResponseMessage> PostDiscordMultipartAsync(
        string webhookUrl,
        string postText,
        byte[] imageBytes,
        string imageMime,
        string? fileName,
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        var payloadJson = JsonSerializer.Serialize(new
        {
            content = Truncate(postText, 2000)
        });
        form.Add(new StringContent(payloadJson, Encoding.UTF8, "application/json"), "payload_json");

        var image = new ByteArrayContent(imageBytes);
        image.Headers.ContentType = MediaTypeHeaderValue.Parse(imageMime);
        form.Add(image, "files[0]", string.IsNullOrWhiteSpace(fileName) ? DefaultFileName(imageMime) : fileName.Trim());
        return await _http.PostAsync(webhookUrl, form, cancellationToken);
    }

    static string DefaultFileName(string imageMime) => imageMime.ToLowerInvariant() switch
    {
        "image/png" => "cover.png",
        "image/gif" => "cover.gif",
        "image/webp" => "cover.webp",
        _ => "cover.jpg"
    };
}

sealed class TelegramResponse<T>
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("result")] public T? Result { get; set; }
}

sealed class TelegramUser
{
    [JsonPropertyName("username")] public string Username { get; set; } = "";
}

sealed class TelegramMessage
{
    [JsonPropertyName("message_id")] public int MessageId { get; set; }
}
