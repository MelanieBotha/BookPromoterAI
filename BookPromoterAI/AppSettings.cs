namespace BookPromoterAI;

class AppSettings
{
    public string SendGridApiKey { get; init; } = "";
    public string SendGridSenderEmail { get; init; } = "";
    public string SendGridSenderName { get; init; } = "Book Promoter AI";
    public string PublicBaseUrl { get; init; } = "";
    public bool ShowSoftLaunchBanner { get; init; } = true;

    public string StripeSecretKey { get; init; } = "";
    public string StripePublishableKey { get; init; } = "";
    public string StripeWebhookSecret { get; init; } = "";

    public bool IsSendGridConfigured =>
        !string.IsNullOrWhiteSpace(SendGridApiKey) &&
        SendGridApiKey != "YOUR_SENDGRID_API_KEY_HERE" &&
        !string.IsNullOrWhiteSpace(SendGridSenderEmail);

    public bool IsStripeConfigured =>
        !string.IsNullOrWhiteSpace(StripeSecretKey) &&
        StripeSecretKey != "YOUR_STRIPE_SECRET_KEY" &&
        StripeSecretKey.StartsWith("sk_", StringComparison.Ordinal);

    public bool IsBillingConfigured => IsStripeConfigured;

    public static AppSettings FromConfiguration(IConfiguration config)
    {
        return new()
        {
            SendGridApiKey = config["SendGrid:ApiKey"] ?? "",
            SendGridSenderEmail = config["SendGrid:SenderEmail"] ?? config["SendGrid:FromEmail"] ?? "",
            SendGridSenderName = config["SendGrid:SenderName"] ?? config["SendGrid:FromName"] ?? "Book Promoter AI",
            PublicBaseUrl = config["App:PublicBaseUrl"] ?? "",
            ShowSoftLaunchBanner = config.GetValue("Launch:ShowBetaBanner", true),
            StripeSecretKey = CleanSecret(config["Stripe:SecretKey"]),
            StripePublishableKey = CleanSecret(config["Stripe:PublishableKey"]),
            StripeWebhookSecret = CleanSecret(config["Stripe:WebhookSecret"])
        };
    }

    /// <summary>Strip quotes, spaces, and line breaks accidentally pasted into Railway env vars.</summary>
    public static string CleanSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        value = value.Trim().Trim('"', '\'');
        return string.Concat(value.Where(c => !char.IsWhiteSpace(c)));
    }
}
