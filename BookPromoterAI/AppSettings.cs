namespace BookPromoterAI;

class AppSettings
{
    public string SendGridApiKey { get; init; } = "";
    public string SendGridSenderEmail { get; init; } = "";
    public string SendGridSenderName { get; init; } = "Book Promoter AI";
    public string PublicBaseUrl { get; init; } = "";
    public bool ShowSoftLaunchBanner { get; init; } = true;
    public bool RailwayCleanupDone { get; init; }

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
            return "Missing - add Stripe__SecretKey in Railway.";
        if (StripeSecretKey.StartsWith("pk_", StringComparison.Ordinal))
            return "Wrong field - this looks like a Publishable key (pk_...). Use sk_live_... in Stripe__SecretKey instead.";
        if (StripeSecretKey.StartsWith("whsec_", StringComparison.Ordinal))
            return "Wrong field - this looks like a Webhook secret. Use sk_live_... in Stripe__SecretKey instead.";
        if (StripeSecretKey.StartsWith("rk_", StringComparison.Ordinal))
            return "Wrong key type - restricted key (rk_live_...). Use the standard Secret key (sk_live_...) from Stripe API keys.";
        if (!StripeSecretKey.StartsWith("sk_", StringComparison.Ordinal))
        {
            var head = StripeSecretKey.Length >= 8 ? StripeSecretKey[..8] : StripeSecretKey;
            return $"Invalid (starts with '{head}...') - must be sk_live_... from Stripe Standard keys.";
        }
        var prefix = StripeSecretKey.Length >= 12 ? StripeSecretKey[..12] : StripeSecretKey;
        var suffix = StripeSecretKey.Length > 4 ? StripeSecretKey[^4..] : "";
        return $"OK - detected ({prefix}...{suffix}).";
    }

    public string DescribeStripePublishableKey()
    {
        if (string.IsNullOrWhiteSpace(StripePublishableKey))
            return "Missing - add Stripe__PublishableKey in Railway.";
        if (StripePublishableKey.StartsWith("sk_", StringComparison.Ordinal) || StripePublishableKey.StartsWith("rk_", StringComparison.Ordinal))
            return "Wrong field - this looks like a Secret key. Use pk_live_... in Stripe__PublishableKey instead.";
        if (StripePublishableKey.StartsWith("whsec_", StringComparison.Ordinal))
            return "Wrong field - this looks like a Webhook secret. Use pk_live_... in Stripe__PublishableKey instead.";
        if (!StripePublishableKey.StartsWith("pk_", StringComparison.Ordinal))
        {
            var head = StripePublishableKey.Length >= 8 ? StripePublishableKey[..8] : StripePublishableKey;
            return $"Invalid (starts with '{head}...') - must be pk_live_... from Stripe Standard keys.";
        }
        return "OK - detected.";
    }

    public string DescribeStripeWebhookSecret()
    {
        if (string.IsNullOrWhiteSpace(StripeWebhookSecret))
            return "Missing - add Stripe__WebhookSecret in Railway (Stripe Webhooks signing secret).";
        if (StripeWebhookSecret.StartsWith("sk_", StringComparison.Ordinal) || StripeWebhookSecret.StartsWith("pk_", StringComparison.Ordinal))
            return "Wrong field - this looks like an API key. Use whsec_... from your Stripe webhook endpoint.";
        if (!StripeWebhookSecret.StartsWith("whsec_", StringComparison.Ordinal))
            return "Invalid - must start with whsec_.";
        return "OK - detected.";
    }

    public string DescribeSendGridApiKey()
    {
        if (string.IsNullOrWhiteSpace(SendGridApiKey) || SendGridApiKey == "YOUR_SENDGRID_API_KEY_HERE")
            return "Missing - add SendGrid__ApiKey in Railway.";
        if (!SendGridApiKey.StartsWith("SG.", StringComparison.Ordinal))
        {
            var head = SendGridApiKey.Length >= 6 ? SendGridApiKey[..6] : SendGridApiKey;
            return $"Invalid (starts with '{head}...') - SendGrid API keys start with SG.";
        }
        return "OK - detected.";
    }

    public string DescribeSendGridSenderEmail()
    {
        if (string.IsNullOrWhiteSpace(SendGridSenderEmail))
            return "Missing - add SendGrid__SenderEmail in Railway (must be verified in SendGrid).";
        if (!SendGridSenderEmail.Contains('@'))
            return "Invalid - must be a full email address verified in SendGrid.";
        return $"OK - {SendGridSenderEmail.Trim()}.";
    }

    public string DescribePublicBaseUrl()
    {
        if (string.IsNullOrWhiteSpace(PublicBaseUrl))
            return "Not set - checkout still works via the Railway URL you browse. Set App__PublicBaseUrl when your custom domain is live.";

        var url = PublicBaseUrl.TrimEnd('/');
        if (url.Contains("railway.app", StringComparison.OrdinalIgnoreCase))
            return $"Using Railway URL ({url}) - fine until bookpromoterai.us DNS is connected.";
        if (url.Contains("bookpromoterai.us", StringComparison.OrdinalIgnoreCase))
            return $"Custom domain configured ({url}). Point DNS at Railway if the site does not load yet.";
        return $"Configured ({url}).";
    }

    public bool UsesCustomDomain =>
        !string.IsNullOrWhiteSpace(PublicBaseUrl) &&
        PublicBaseUrl.Contains("bookpromoterai.us", StringComparison.OrdinalIgnoreCase);

    public static AppSettings FromConfiguration(IConfiguration config)
    {
        return new()
        {
            SendGridApiKey = config["SendGrid:ApiKey"] ?? "",
            SendGridSenderEmail = config["SendGrid:SenderEmail"] ?? config["SendGrid:FromEmail"] ?? "",
            SendGridSenderName = config["SendGrid:SenderName"] ?? config["SendGrid:FromName"] ?? "Book Promoter AI",
            PublicBaseUrl = config["App:PublicBaseUrl"] ?? "",
            ShowSoftLaunchBanner = config.GetValue("Launch:ShowBetaBanner", true),
            RailwayCleanupDone = config.GetValue("Launch:RailwayCleanupDone", false),
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
