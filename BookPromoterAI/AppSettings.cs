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

    public bool IsStripeWebhookConfigured =>
        !string.IsNullOrWhiteSpace(StripeWebhookSecret) &&
        StripeWebhookSecret.StartsWith("whsec_", StringComparison.Ordinal);

    public string DescribeStripeSecretKey()
    {
        if (string.IsNullOrWhiteSpace(StripeSecretKey) || StripeSecretKey == "YOUR_STRIPE_SECRET_KEY")
            return "Missing — add Stripe__SecretKey in Railway.";
        if (StripeSecretKey.StartsWith("rk_", StringComparison.Ordinal))
            return "Wrong key type — you pasted a restricted key (rk_live_...). Use the standard Secret key (sk_live_...) from Stripe → API keys.";
        if (!StripeSecretKey.StartsWith("sk_", StringComparison.Ordinal))
            return "Invalid — must start with sk_live_ or sk_test_.";
        var prefix = StripeSecretKey.Length >= 12 ? StripeSecretKey[..12] : StripeSecretKey;
        var suffix = StripeSecretKey.Length > 4 ? StripeSecretKey[^4..] : "";
        return $"Detected ({prefix}...{suffix}).";
    }

    public string DescribeStripePublishableKey()
    {
        if (string.IsNullOrWhiteSpace(StripePublishableKey))
            return "Missing — add Stripe__PublishableKey in Railway.";
        if (!StripePublishableKey.StartsWith("pk_", StringComparison.Ordinal))
            return "Invalid — must start with pk_live_ or pk_test_.";
        return "Detected.";
    }

    public string DescribeStripeWebhookSecret()
    {
        if (string.IsNullOrWhiteSpace(StripeWebhookSecret))
            return "Missing — add Stripe__WebhookSecret in Railway (from Stripe → Webhooks → Signing secret).";
        if (!StripeWebhookSecret.StartsWith("whsec_", StringComparison.Ordinal))
            return "Invalid — must start with whsec_.";
        return "Detected.";
    }

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
