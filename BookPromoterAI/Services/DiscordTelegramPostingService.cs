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

    public async Task<(bool Ok, string Error, string? ChatTitle)> ValidateTelegramChatAsync(
        string botToken,
        string chatId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chatId))
            return (false, "Enter your Telegram channel or group chat ID.", null);

        var normalized = NormalizeChatId(chatId);
        var url = $"https://api.telegram.org/bot{botToken.Trim()}/getChat?chat_id={Uri.EscapeDataString(normalized)}";
        var response = await _http.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, "Telegram rejected that chat ID. Check the ID and try again.", null);

        var payload = System.Text.Json.JsonSerializer.Deserialize<TelegramResponse<TelegramChat>>(body);
        if (payload?.Ok != true || payload.Result is null)
        {
            var apiError = ExtractTelegramError(body);
            return (false, apiError ?? "Bot cannot access that chat. Add the bot as an admin to your channel, then try again.", null);
        }

        var title = string.IsNullOrWhiteSpace(payload.Result.Title)
            ? payload.Result.Username
            : payload.Result.Title;
        return (true, "", title?.Trim());
    }

    public async Task<PostingResult> PostTelegramAsync(
        string botToken,
        string chatId,
        string postText,
        byte[]? imageBytes = null,
        string? imageMime = null,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
            return PostingResult.Failure("Telegram bot token or chat ID is missing. Reconnect in My Account.");

        var normalizedChatId = NormalizeChatId(chatId);
        var token = botToken.Trim();

        if (imageBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(imageMime))
        {
            var caption = Truncate(postText, 1024);
            return await PostTelegramPhotoAsync(
                token, normalizedChatId, imageBytes, imageMime, fileName, caption, cancellationToken);
        }

        var url = $"https://api.telegram.org/bot{token}/sendMessage";
        var form = new Dictionary<string, string>
        {
            ["chat_id"] = normalizedChatId,
            ["text"] = Truncate(postText, 4096),
            ["disable_web_page_preview"] = "false"
        };
        var response = await _http.PostAsync(url, new FormUrlEncodedContent(form), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseTelegramPostResponse(body, "Posted to Telegram.");
    }

    static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";

    static string NormalizeChatId(string chatId)
    {
        var value = chatId.Trim();
        if (value.StartsWith('@'))
            return value;
        if (value.StartsWith("-100", StringComparison.Ordinal))
            return value;
        if (long.TryParse(value, out _))
            return value;
        return value;
    }

    static string? ExtractTelegramError(string body)
    {
        try
        {
            var envelope = System.Text.Json.JsonSerializer.Deserialize<TelegramResponse<object>>(body);
            if (envelope?.Ok == false && !string.IsNullOrWhiteSpace(envelope.Description))
                return envelope.Description;
        }
        catch
        {
            // ignore parse errors
        }
        return null;
    }

    static PostingResult ParseTelegramPostResponse(string body, string successMessage)
    {
        var payload = System.Text.Json.JsonSerializer.Deserialize<TelegramResponse<TelegramMessage>>(body);
        if (payload?.Ok != true)
        {
            var apiError = ExtractTelegramError(body);
            return PostingResult.Failure(apiError is not null
                ? $"Telegram rejected the post: {apiError}"
                : $"Telegram rejected the post: {body}");
        }

        var externalId = payload.Result?.MessageId > 0 ? payload.Result.MessageId.ToString() : null;
        return PostingResult.LiveOk(successMessage, externalId);
    }

    async Task<PostingResult> PostTelegramPhotoAsync(
        string botToken,
        string chatId,
        byte[] imageBytes,
        string imageMime,
        string? fileName,
        string caption,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.telegram.org/bot{botToken}/sendPhoto";
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(chatId), "chat_id");
        if (!string.IsNullOrWhiteSpace(caption))
            form.Add(new StringContent(caption), "caption");

        var image = new ByteArrayContent(imageBytes);
        image.Headers.ContentType = MediaTypeHeaderValue.Parse(imageMime);
        form.Add(image, "photo", string.IsNullOrWhiteSpace(fileName) ? DefaultFileName(imageMime) : fileName.Trim());

        var response = await _http.PostAsync(url, form, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return PostingResult.Failure($"Telegram photo post failed: {body}");
        return ParseTelegramPostResponse(body, "Posted to Telegram with photo.");
    }

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
    [JsonPropertyName("description")] public string? Description { get; set; }
}

sealed class TelegramUser
{
    [JsonPropertyName("username")] public string Username { get; set; } = "";
}

sealed class TelegramChat
{
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("username")] public string? Username { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
}

sealed class TelegramMessage
{
    [JsonPropertyName("message_id")] public int MessageId { get; set; }
}
