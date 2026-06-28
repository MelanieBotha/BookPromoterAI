using SendGrid;
using SendGrid.Helpers.Mail;

namespace BookPromoterAI;

static class EmailService
{
    static bool UseSendGrid(string apiKey, string senderEmail) =>
        !string.IsNullOrWhiteSpace(apiKey) &&
        apiKey != "YOUR_SENDGRID_API_KEY_HERE" &&
        !string.IsNullOrWhiteSpace(senderEmail);

    static async Task<bool> SendSingleEmail(
        string apiKey,
        string senderEmail,
        string senderName,
        string toEmail,
        string? toName,
        string subject,
        string plainBody,
        string htmlBody)
    {
        if (!UseSendGrid(apiKey, senderEmail))
        {
            await Task.CompletedTask;
            return true;
        }

        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(senderEmail, senderName);
        var to = new EmailAddress(toEmail, string.IsNullOrWhiteSpace(toName) ? null : toName);
        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainBody, htmlBody);
        var response = await client.SendEmailAsync(msg);
        return response.IsSuccessStatusCode;
    }

    public static async Task<bool> SendAccessCodeEmail(
        string toEmail,
        string accessCode,
        string apiKey,
        string senderEmail,
        string senderName)
    {
        var subject = "Your BookPromoter AI Access Code";
        var plainBody = $"Your access code is: {accessCode}\n\nThis code gives you 30 days of access. Enter it at the Access Code page along with this email address.";
        var htmlBody = $"""
            <h2>Your BookPromoter AI Access Code</h2>
            <p>Use the code below to activate your 30-day access:</p>
            <p style="font-size:24px;font-weight:bold;letter-spacing:2px;background:#f4f7fb;padding:16px;border-radius:8px;display:inline-block">{accessCode}</p>
            <p>Enter this code along with your email address on the Access Code page.</p>
            <p>This code is assigned to {toEmail} and can only be used once.</p>
            """;
        return await SendSingleEmail(apiKey, senderEmail, senderName, toEmail, null, subject, plainBody, htmlBody);
    }

    public static async Task<bool> SendPasswordResetEmail(
        string toEmail,
        string resetLink,
        string apiKey,
        string senderEmail,
        string senderName)
    {
        var subject = "Reset your BookPromoter AI password";
        var plainBody = $"Click this link to reset your password (valid for 1 hour):\n{resetLink}\n\nIf you didn't request this, ignore this email.";
        var htmlBody = $"""
            <h2>Reset your BookPromoter AI password</h2>
            <p>Click the button below to reset your password. This link expires in <strong>1 hour</strong>.</p>
            <p style="margin:24px 0">
                <a href="{resetLink}" style="background:#0f766e;color:white;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold">Reset Password</a>
            </p>
            <p>Or copy and paste this link: <span style="word-break:break-all;color:#667085">{resetLink}</span></p>
            <p style="color:#667085;font-size:13px">If you didn't request this, ignore this email — your password won't change.</p>
            """;
        return await SendSingleEmail(apiKey, senderEmail, senderName, toEmail, null, subject, plainBody, htmlBody);
    }

    public static async Task<bool> SendTeamInviteEmail(
        string toEmail,
        string inviteCode,
        string role,
        string apiKey,
        string senderEmail,
        string senderName)
    {
        var subject = $"You've been invited to join BookPromoter AI as {role}";
        var plainBody = $"You've been invited to join BookPromoter AI as {role}.\nYour invite code is: {inviteCode}\nUse this code when creating your account.";
        var htmlBody = $"""
            <h2>You're invited to BookPromoter AI</h2>
            <p>You've been invited to join as a <strong>{role}</strong>.</p>
            <p>Your invite code:</p>
            <p style="font-size:24px;font-weight:bold;letter-spacing:2px;background:#f4f7fb;padding:16px;border-radius:8px;display:inline-block">{inviteCode}</p>
            <p>Create your account at BookPromoter AI and enter this code to join the team.</p>
            """;
        return await SendSingleEmail(apiKey, senderEmail, senderName, toEmail, null, subject, plainBody, htmlBody);
    }

    public static string GenerateThankYouEmail(string email, string category, string message)
    {
        var opening = category switch
        {
            "Bug Report" => "Thank you for taking the time to report this issue.",
            "Feature Request" => "Thank you for sharing your idea with us.",
            "Suggestion" => "Thank you for your suggestion — we really appreciate it.",
            _ => "Thank you for reaching out to us."
        };

        var body = category switch
        {
            "Bug Report" =>
                "We've received your bug report and our team will investigate it as soon as possible. " +
                "We take every report seriously and will work to resolve the issue promptly. " +
                "If we need any additional information, we'll be in touch.",
            "Feature Request" =>
                "We love hearing from our users about features they'd like to see. " +
                "Your request has been added to our product backlog and will be considered as we plan future updates. " +
                "We can't promise a timeline, but your input genuinely shapes what we build next.",
            "Suggestion" =>
                "Your suggestion has been noted and shared with our team. " +
                "We're always looking for ways to improve BookPromoter AI, and feedback from users like you is invaluable.",
            _ =>
                "Your message has been received and we'll review it shortly. " +
                "We appreciate every piece of feedback we get from our community."
        };

        return $"""
            Hi there,

            {opening}

            {body}

            Your feedback:
            "{message}"

            Thanks again for helping us make BookPromoter AI better.

            Warm regards,
            The BookPromoter AI Team
            """;
    }

    public static async Task<bool> SendThankYouEmail(
        string toEmail,
        string emailBody,
        string apiKey,
        string senderEmail,
        string senderName)
    {
        var subject = "Thank you for your feedback — BookPromoter AI";
        var htmlBody = emailBody.Replace("\n", "<br>");
        return await SendSingleEmail(apiKey, senderEmail, senderName, toEmail, null, subject, emailBody, htmlBody);
    }

    public static async Task<bool> SendMailingListEmail(
        string toEmail,
        string toName,
        string subject,
        string body,
        string fromDisplayName,
        string apiKey,
        string senderEmail,
        string senderName)
    {
        var htmlBody = body.Replace("\n", "<br>");
        return await SendSingleEmail(apiKey, senderEmail, fromDisplayName, toEmail, toName, subject, body, htmlBody);
    }

    public static async Task<(int Sent, int Failed)> SendProductUpdateEmailAsync(
        IEnumerable<string> recipientEmails,
        ProductUpdate update,
        string appBaseUrl,
        string apiKey,
        string senderEmail,
        string senderName)
    {
        var subject = string.IsNullOrWhiteSpace(update.Title)
            ? $"BookPromoter AI v{update.Version} — What's new"
            : update.Title;
        var sent = 0;
        var failed = 0;

        foreach (var email in recipientEmails.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var (plain, html) = BuildProductUpdateBodies(update, appBaseUrl);
            var ok = await SendSingleEmail(apiKey, senderEmail, senderName, email, null, subject, plain, html);
            if (ok) sent++; else failed++;
        }

        return (sent, failed);
    }

    public static async Task<(int Sent, int Failed)> SendBroadcastEmailAsync(
        IEnumerable<string> recipientEmails,
        string subject,
        string body,
        string apiKey,
        string senderEmail,
        string senderName)
    {
        var sent = 0;
        var failed = 0;
        var htmlBody = body.Replace("\n", "<br>");

        foreach (var email in recipientEmails.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var ok = await SendSingleEmail(apiKey, senderEmail, senderName, email, null, subject.Trim(), body.Trim(), htmlBody);
            if (ok) sent++; else failed++;
        }

        return (sent, failed);
    }

    static (string Plain, string Html) BuildProductUpdateBodies(ProductUpdate update, string appBaseUrl)
    {
        var dashboardUrl = $"{appBaseUrl.TrimEnd('/')}/dashboard";
        var updated = AppPromoGenerator.ParseLines(update.UpdatedItems);
        var created = AppPromoGenerator.ParseLines(update.CreatedItems);
        var added = AppPromoGenerator.ParseLines(update.AddedItems);

        var plain = new System.Text.StringBuilder();
        plain.AppendLine("Hi,");
        plain.AppendLine();
        plain.AppendLine(string.IsNullOrWhiteSpace(update.Title)
            ? $"We've released BookPromoter AI v{update.Version}."
            : update.Title);
        plain.AppendLine();

        AppendSection(plain, "Updated", updated);
        AppendSection(plain, "New", created);
        AppendSection(plain, "Added", added);

        plain.AppendLine($"Open your dashboard: {dashboardUrl}");
        plain.AppendLine();
        plain.AppendLine("— The BookPromoter AI Team");

        var html = new System.Text.StringBuilder();
        html.Append("<p>Hi,</p>");
        html.Append("<p>");
        html.Append(HtmlEncode(string.IsNullOrWhiteSpace(update.Title)
            ? $"We've released BookPromoter AI v{update.Version}."
            : update.Title));
        html.Append("</p>");
        AppendHtmlSection(html, "Updated", updated);
        AppendHtmlSection(html, "New", created);
        AppendHtmlSection(html, "Added", added);
        html.Append($"""<p style="margin:24px 0"><a href="{HtmlEncode(dashboardUrl)}" style="background:#0f766e;color:white;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold">Open Dashboard</a></p>""");
        html.Append("<p>— The BookPromoter AI Team</p>");

        return (plain.ToString(), html.ToString());
    }

    static void AppendSection(System.Text.StringBuilder sb, string heading, List<string> items)
    {
        if (items.Count == 0) return;
        sb.AppendLine($"{heading.ToUpperInvariant()}:");
        foreach (var item in items)
            sb.AppendLine($"• {item}");
        sb.AppendLine();
    }

    static void AppendHtmlSection(System.Text.StringBuilder sb, string heading, List<string> items)
    {
        if (items.Count == 0) return;
        sb.Append($"<p><strong>{HtmlEncode(heading)}</strong></p><ul>");
        foreach (var item in items)
            sb.Append($"<li>{HtmlEncode(item)}</li>");
        sb.Append("</ul>");
    }

    static string HtmlEncode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}
