namespace BookPromoterAI;

class AppSettings
{
    public string SendGridApiKey { get; init; } = "";
    public string SendGridSenderEmail { get; init; } = "";
    public string SendGridSenderName { get; init; } = "Book Promoter AI";
    public string OwnerPin { get; init; } = "";
    public string PublicBaseUrl { get; init; } = "";
    public bool ShowSoftLaunchBanner { get; init; } = true;

    public bool IsSendGridConfigured =>
        !string.IsNullOrWhiteSpace(SendGridApiKey) &&
        SendGridApiKey != "YOUR_SENDGRID_API_KEY_HERE" &&
        !string.IsNullOrWhiteSpace(SendGridSenderEmail);

    public static AppSettings FromConfiguration(IConfiguration config) => new()
    {
        SendGridApiKey = config["SendGrid:ApiKey"] ?? "",
        SendGridSenderEmail = config["SendGrid:SenderEmail"] ?? config["SendGrid:FromEmail"] ?? "",
        SendGridSenderName = config["SendGrid:SenderName"] ?? config["SendGrid:FromName"] ?? "BookPromoter AI",
        var ownerPin = config["Owner:Pin"];
        OwnerPin = string.IsNullOrWhiteSpace(ownerPin) ? "" : ownerPin.Trim(),
        PublicBaseUrl = config["App:PublicBaseUrl"] ?? "",
        ShowSoftLaunchBanner = config.GetValue("Launch:ShowBetaBanner", true)
    };
}
