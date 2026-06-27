namespace BookPromoterAI;

class AppSettings
{
    public string SendGridApiKey { get; init; } = "";
    public string SendGridSenderEmail { get; init; } = "";
    public string SendGridSenderName { get; init; } = "Book Promoter AI";
    public string OwnerPin { get; init; } = "";
    public string PublicBaseUrl { get; init; } = "";
    public bool ShowSoftLaunchBanner { get; init; } = true;

    public string StripeSecretKey { get; init; } = "";
    public string StripePublishableKey { get; init; } = "";
    public string StripeWebhookSecret { get; init; } = "";

    public string PayPalClientId { get; init; } = "";
    public string PayPalClientSecret { get; init; } = "";
    public string PayPalWebhookId { get; init; } = "";
    public bool PayPalUseSandbox { get; init; } = true;

    public bool IsSendGridConfigured =>
        !string.IsNullOrWhiteSpace(SendGridApiKey) &&
        SendGridApiKey != "YOUR_SENDGRID_API_KEY_HERE" &&
        !string.IsNullOrWhiteSpace(SendGridSenderEmail);

    public bool IsStripeConfigured =>
        !string.IsNullOrWhiteSpace(StripeSecretKey) &&
        StripeSecretKey != "YOUR_STRIPE_SECRET_KEY";

    public bool IsPayPalConfigured =>
        !string.IsNullOrWhiteSpace(PayPalClientId) &&
        PayPalClientId != "YOUR_PAYPAL_CLIENT_ID" &&
        !string.IsNullOrWhiteSpace(PayPalClientSecret) &&
        PayPalClientSecret != "YOUR_PAYPAL_CLIENT_SECRET";

    public bool IsBillingConfigured => IsStripeConfigured || IsPayPalConfigured;

    public static AppSettings FromConfiguration(IConfiguration config)
    {
        var ownerPin = config["Owner:Pin"];
        return new()
        {
            SendGridApiKey = config["SendGrid:ApiKey"] ?? "",
            SendGridSenderEmail = config["SendGrid:SenderEmail"] ?? config["SendGrid:FromEmail"] ?? "",
            SendGridSenderName = config["SendGrid:SenderName"] ?? config["SendGrid:FromName"] ?? "BookPromoter AI",
            OwnerPin = string.IsNullOrWhiteSpace(ownerPin) ? "" : ownerPin.Trim(),
            PublicBaseUrl = config["App:PublicBaseUrl"] ?? "",
            ShowSoftLaunchBanner = config.GetValue("Launch:ShowBetaBanner", true),
            StripeSecretKey = config["Stripe:SecretKey"] ?? "",
            StripePublishableKey = config["Stripe:PublishableKey"] ?? "",
            StripeWebhookSecret = config["Stripe:WebhookSecret"] ?? "",
            PayPalClientId = config["PayPal:ClientId"] ?? "",
            PayPalClientSecret = config["PayPal:ClientSecret"] ?? "",
            PayPalWebhookId = config["PayPal:WebhookId"] ?? "",
            PayPalUseSandbox = config.GetValue("PayPal:UseSandbox", true)
        };
    }
}
