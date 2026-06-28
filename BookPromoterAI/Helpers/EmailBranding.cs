using SendGrid.Helpers.Mail;

namespace BookPromoterAI;

static class EmailBranding
{
    public const string LogoContentId = "bookpromoter-logo";
    const string LogoFileName = "BookPromoterAI.logo.png";

    public static string ResolveLogoSrc(string? appBaseUrl) =>
        LogoBytes() is not null
            ? $"cid:{LogoContentId}"
            : PostBranding.AbsoluteLogoUrl(EmailTemplate.ResolveBaseUrl(appBaseUrl));

    public static void AttachInlineLogo(SendGridMessage msg)
    {
        var bytes = LogoBytes();
        if (bytes is null) return;

        msg.AddAttachment(new Attachment
        {
            Content = Convert.ToBase64String(bytes),
            Type = "image/png",
            Filename = LogoFileName,
            Disposition = "inline",
            ContentId = LogoContentId
        });
    }

    static byte[]? LogoBytes()
    {
        foreach (var path in CandidateLogoPaths())
        {
            if (!File.Exists(path)) continue;
            try { return File.ReadAllBytes(path); }
            catch { /* try next path */ }
        }

        return null;
    }

    static IEnumerable<string> CandidateLogoPaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "wwwroot", "images", LogoFileName);
        var cwd = Directory.GetCurrentDirectory();
        if (!string.IsNullOrWhiteSpace(cwd))
            yield return Path.Combine(cwd, "wwwroot", "images", LogoFileName);
    }
}
