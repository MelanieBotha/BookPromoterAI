using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BookPromoterAI;

class PayPalBillingService
{
    private readonly AppSettings _settings;
    private readonly IHttpClientFactory _httpFactory;

    public PayPalBillingService(AppSettings settings, IHttpClientFactory httpFactory)
    {
        _settings = settings;
        _httpFactory = httpFactory;
    }

    string ApiBase => _settings.PayPalUseSandbox
        ? "https://api-m.sandbox.paypal.com"
        : "https://api-m.paypal.com";

    public async Task<(bool Ok, string? ApproveUrl, string? Error)> CreateSubscriptionAsync(
        HttpRequest request, DbUser user, DbSubscriptionPlan plan, string planId)
    {
        if (!_settings.IsPayPalConfigured)
            return (false, null, "PayPal is not configured yet.");
        if (string.IsNullOrWhiteSpace(plan.PayPalPlanId))
            return (false, null, "PayPal is not set up for this plan yet. The site owner must add a PayPal Plan ID on the Owner page.");

        var token = await GetAccessTokenAsync();
        if (token is null)
            return (false, null, "Could not connect to PayPal. Check your PayPal API credentials.");

        var baseUrl = PublicUrl.Base(request, _settings);
        var payload = new
        {
            plan_id = plan.PayPalPlanId,
            custom_id = $"{user.Id}:{planId}",
            subscriber = new { email_address = user.Email },
            application_context = new
            {
                brand_name = "BookPromoter AI",
                user_action = "SUBSCRIBE_NOW",
                return_url = $"{baseUrl}/subscription/paypal/return",
                cancel_url = $"{baseUrl}/subscription?cancelled=1"
            }
        };

        var client = _httpFactory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/v1/billing/subscriptions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("Prefer", "return=representation");
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var resp = await client.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return (false, null, ParsePayPalError(body) ?? "PayPal could not start the subscription.");

        using var doc = JsonDocument.Parse(body);
        var approve = doc.RootElement.GetProperty("links").EnumerateArray()
            .FirstOrDefault(l => l.GetProperty("rel").GetString() == "approve");
        var url = approve.ValueKind == JsonValueKind.Object ? approve.GetProperty("href").GetString() : null;
        return string.IsNullOrWhiteSpace(url)
            ? (false, null, "PayPal did not return an approval link.")
            : (true, url, null);
    }

    public async Task<bool> FulfillSubscriptionAsync(string subscriptionId, AppStoreDb store)
    {
        if (!_settings.IsPayPalConfigured || string.IsNullOrWhiteSpace(subscriptionId))
            return false;

        var token = await GetAccessTokenAsync();
        if (token is null) return false;

        var client = _httpFactory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/v1/billing/subscriptions/{subscriptionId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return false;

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var status = doc.RootElement.GetProperty("status").GetString() ?? "";
        if (!status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) &&
            !status.Equals("APPROVAL_PENDING", StringComparison.OrdinalIgnoreCase))
            return false;

        var customId = doc.RootElement.TryGetProperty("custom_id", out var custom) ? custom.GetString() : null;
        if (!TryParseCustomId(customId, out var userId, out var planId))
            return false;

        var periodEnd = DateTime.UtcNow.AddMonths(1);
        if (doc.RootElement.TryGetProperty("billing_info", out var billing) &&
            billing.TryGetProperty("next_billing_time", out var next) &&
            DateTime.TryParse(next.GetString(), out var nextDt))
            periodEnd = nextDt.ToUniversalTime();

        return store.ActivatePaidSubscriptionFromProvider(
            userId,
            planId,
            "paypal",
            null,
            null,
            subscriptionId,
            periodEnd,
            "PayPal");
    }

    public async Task<(bool Ok, string? Error)> CancelSubscriptionAsync(DbUser user)
    {
        if (string.IsNullOrWhiteSpace(user.PayPalSubscriptionId))
            return (false, "No PayPal subscription to cancel.");

        var token = await GetAccessTokenAsync();
        if (token is null) return (false, "Could not connect to PayPal.");

        var client = _httpFactory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/v1/billing/subscriptions/{user.PayPalSubscriptionId}/cancel");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent("""{"reason":"Customer requested cancellation"}""", Encoding.UTF8, "application/json");

        var resp = await client.SendAsync(req);
        if (resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.NoContent)
            return (true, null);

        var body = await resp.Content.ReadAsStringAsync();
        return (false, ParsePayPalError(body) ?? "PayPal could not cancel the subscription.");
    }

    public async Task<bool> HandleWebhookAsync(string json, AppStoreDb store)
    {
        if (!_settings.IsPayPalConfigured) return false;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("event_type", out var eventTypeEl))
            return false;

        var eventType = eventTypeEl.GetString() ?? "";
        if (!doc.RootElement.TryGetProperty("resource", out var resource))
            return false;

        switch (eventType)
        {
            case "BILLING.SUBSCRIPTION.ACTIVATED":
            case "BILLING.SUBSCRIPTION.RE-ACTIVATED":
                if (resource.TryGetProperty("id", out var idEl))
                    await FulfillSubscriptionAsync(idEl.GetString() ?? "", store);
                break;

            case "BILLING.SUBSCRIPTION.CANCELLED":
            case "BILLING.SUBSCRIPTION.EXPIRED":
                if (resource.TryGetProperty("id", out var cancelledId))
                    store.MarkSubscriptionCancelledByProvider(cancelledId.GetString() ?? "", "paypal", DateTime.UtcNow);
                break;

            case "BILLING.SUBSCRIPTION.SUSPENDED":
                if (resource.TryGetProperty("id", out var suspendedId))
                    store.SetBillingStatus(suspendedId.GetString() ?? "", "paypal", "past_due");
                break;
        }

        return true;
    }

    async Task<string?> GetAccessTokenAsync()
    {
        var client = _httpFactory.CreateClient();
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.PayPalClientId}:{_settings.PayPalClientSecret}"));
        var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/v1/oauth2/token");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
        req.Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("grant_type", "client_credentials")]);

        var resp = await client.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("access_token", out var token) ? token.GetString() : null;
    }

    static bool TryParseCustomId(string? customId, out int userId, out string planId)
    {
        userId = 0;
        planId = "";
        if (string.IsNullOrWhiteSpace(customId)) return false;
        var parts = customId.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[0], out userId)) return false;
        planId = parts[1];
        return !string.IsNullOrWhiteSpace(planId);
    }

    static string? ParsePayPalError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString();
            if (doc.RootElement.TryGetProperty("details", out var details) &&
                details.ValueKind == JsonValueKind.Array &&
                details.GetArrayLength() > 0 &&
                details[0].TryGetProperty("description", out var desc))
                return desc.GetString();
        }
        catch { /* ignore */ }
        return null;
    }
}
