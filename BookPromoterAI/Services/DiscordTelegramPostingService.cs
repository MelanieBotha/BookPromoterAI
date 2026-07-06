using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BookPromoterAI;

/// <summary>Discord incoming webhooks and Telegram Bot API — no OAuth app approval needed.</summary>
class DiscordTelegramPostingService
{
    readonly HttpClient _http;

    public DiscordTelegramPostingService(HttpClient http) => _http = http;

    public static bool IsDiscordWebhook(string? token) =>
        !string.IsNullOrWhiteSpace(token) &&
        token.Contains("discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase);

    public async Task<PostingResult> PostDiscordWebhookAsync(
        string webhookUrl, string postText, CancellationToken cancellationToken = default)
    {
        if (!IsDiscordWebhook(webhookUrl))
            return PostingResult.Failure("Discord webhook URL looks invalid. Reconnect in My Account.");

        var payload = new { content = Truncate(postText, 2000) };
        var response = await _http.PostAsJsonAsync(webhookUrl.Trim(), payload, cancellationToken);
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
