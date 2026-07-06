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
        string htmlBody,
        string? appBaseUrl = null,
        string? heading = null,
        string? footerNote = null)
    {
        if (!UseSendGrid(apiKey, senderEmail))
        {
            await Task.CompletedTask;
            return true;
        }

        var logoSrc = EmailBranding.ResolveLogoSrc(appBaseUrl);
        var wrappedHtml = EmailTemplate.Wrap(appBaseUrl, heading ?? subject, htmlBody, footerNote, logoSrc);
        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(senderEmail, senderName);
        var to = new EmailAddress(toEmail, string.IsNullOrWhiteSpace(toName) ? null : toName);
        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainBody, wrappedHtml);
        EmailBranding.AttachInlineLogo(msg);
        var response = await client.SendEmailAsync(msg);
        return response.IsSuccessStatusCode;
    }

    public static async Task<bool> SendAccessCodeEmail(
        string toEmail,
        string accessCode,
        string apiKey,
        string senderEmail,
        string senderName,
        string? appBaseUrl = null)
    {
        var subject = "Your BookPromoter AI Access Code";
        var plainBody = $"Your access code is: {accessCode}\n\nThis code gives you 30 days of access. Enter it at the Access Code page along with this email address.";
        var htmlBody = $"""
            {EmailTemplate.Paragraph("Use the code below to activate your <strong>30-day access</strong>:")}
            {EmailTemplate.CodeBlock(accessCode)}
            {EmailTemplate.Paragraph("Enter this code along with your email address on the Access Code page.")}
            {EmailTemplate.Paragraph($"This code is assigned to <strong>{HtmlEncode(toEmail)}</strong> and can only be used once.")}
            """;
        return await SendSingleEmail(apiKey, senderEmail, senderName, toEmail, null, subject, plainBody, htmlBody, appBaseUrl, "Your Access Code");
    }

    public static async Task<bool> SendPasswordResetEmail(
        string toEmail,
        string resetLink,
        string apiKey,
        string senderEmail,
        string senderName,
        string? appBaseUrl = null)
    {
        var subject = "Reset your BookPromoter AI password";
        var plainBody = $"Click this link to reset your password (valid for 1 hour):\n{resetLink}\n\nIf you didn't request this, ignore this email.";
        var htmlBody = $"""
            {EmailTemplate.Paragraph("Click the button below to reset your password. This link expires in <strong>1 hour</strong>.")}
            {EmailTemplate.PrimaryButton(resetLink, "Reset Password")}
            {EmailTemplate.Paragraph($"""Or copy and paste this link: <span style="word-break:break-all;color:#667085">{HtmlEncode(resetLink)}</span>""")}
            {EmailTemplate.MutedParagraph("If you didn't request this, ignore this email — your password won't change.")}
            """;
        return await SendSingleEmail(apiKey, senderEmail, senderName, toEmail, null, subject, plainBody, htmlBody, appBaseUrl, "Reset Your Password");
    }

    public static async Task<bool> SendTeamInviteEmail(
        string toEmail,
        string inviteCode,
        string role,
        string apiKey,
        string senderEmail,
        string senderName,
        string? appBaseUrl = null)
    {
        var baseUrl = EmailTemplate.ResolveBaseUrl(appBaseUrl);
        var subject = $"You've been invited to join BookPromoter AI as {role}";
        var plainBody = $"You've been invited to join BookPromoter AI as {role}.\nYour invite code is: {inviteCode}\nUse this code when creating your account.";
        var htmlBody = $"""
            {EmailTemplate.Paragraph($"You've been invited to join as a <strong>{HtmlEncode(role)}</strong>.")}
            {EmailTemplate.Paragraph("Your invite code:")}
            {EmailTemplate.CodeBlock(inviteCode)}
            {EmailTemplate.Paragraph($"""Create your account at <a href="{HtmlEncode(baseUrl)}/start" style="color:#0f766e;font-weight:600;">BookPromoter AI</a> and enter this code to join the team.""")}
            """;
        return await SendSingleEmail(apiKey, senderEmail, senderName, toEmail, null, subject, plainBody, htmlBody, appBaseUrl, "You're Invited");
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
        string senderName,
        string? appBaseUrl = null)
    {
        var subject = "Thank you for your feedback — BookPromoter AI";
        var htmlBody = FormatPlainTextAsHtml(emailBody);
        return await SendSingleEmail(apiKey, senderEmail, senderName, toEmail, null, subject, emailBody, htmlBody, appBaseUrl, "Thank You for Your Feedback");
    }

    public static async Task<bool> SendOwnerFeedbackNotificationEmail(
        FeedbackEntry entry,
        string apiKey,
        string senderEmail,
        string senderName,
        string? appBaseUrl = null)
    {
        var ownerEmail = OwnerAccount.Email;
        var baseUrl = EmailTemplate.ResolveBaseUrl(appBaseUrl);
        var ownerUrl = $"{baseUrl}/owner-promos";
        var category = string.IsNullOrWhiteSpace(entry.Category) ? "Suggestion" : entry.Category.Trim();
        var fromEmail = string.IsNullOrWhiteSpace(entry.Email) ? "(not provided)" : entry.Email.Trim();
        var submitted = AppTimeZone.FormatWithZone(entry.SubmittedAt, "MMMM d, yyyy 'at' h:mm tt");

        var subject = $"New {category} — BookPromoter AI feedback";
        var plainBody = $"""
            A user submitted feedback on BookPromoter AI.

            Category: {category}
            From: {fromEmail}
            Submitted: {submitted}

            Message:
            {entry.Message}

            Review all feedback in the Owner panel:
            {ownerUrl}
            """;

        var htmlBody = $"""
            {EmailTemplate.Paragraph("A user just submitted <strong>feedback or a suggestion</strong> on BookPromoter AI.")}
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:16px 0;width:100%;border-collapse:collapse;">
                <tr>
                    <td style="padding:10px 12px;background:#f4f7fb;border:1px solid #d7dde8;font-weight:700;width:120px;">Category</td>
                    <td style="padding:10px 12px;border:1px solid #d7dde8;">{HtmlEncode(category)}</td>
                </tr>
                <tr>
                    <td style="padding:10px 12px;background:#f4f7fb;border:1px solid #d7dde8;font-weight:700;">From</td>
                    <td style="padding:10px 12px;border:1px solid #d7dde8;">{HtmlEncode(fromEmail)}</td>
                </tr>
                <tr>
                    <td style="padding:10px 12px;background:#f4f7fb;border:1px solid #d7dde8;font-weight:700;">Submitted</td>
                    <td style="padding:10px 12px;border:1px solid #d7dde8;">{HtmlEncode(submitted)}</td>
                </tr>
            </table>
            {EmailTemplate.Paragraph("<strong>Message</strong>")}
            <p style="margin:0 0 16px;padding:16px;background:#f4f7fb;border:1px solid #d7dde8;border-radius:8px;white-space:pre-wrap;line-height:1.6;">{HtmlEncode(entry.Message)}</p>
            {EmailTemplate.PrimaryButton(ownerUrl, "View in Owner Panel")}
            {EmailTemplate.MutedParagraph("You receive this because you are the BookPromoter AI site owner.")}
            """;

        return await SendSingleEmail(
            apiKey, senderEmail, senderName, ownerEmail, "Melanie",
            subject, plainBody, htmlBody, appBaseUrl, "New Feedback Received");
    }

    public static async Task<bool> SendMailingListEmail(
        string toEmail,
        string toName,
        string subject,
        string body,
        string fromDisplayName,
        string apiKey,
        string senderEmail,
        string senderName,
        string? appBaseUrl = null,
        string? unsubscribeUrl = null,
        string? coverImageUrl = null,
        string? coverLinkUrl = null,
        string? coverTitle = null)
    {
        var greeting = string.IsNullOrWhiteSpace(toName)
            ? ""
            : EmailTemplate.Paragraph($"Hi {HtmlEncode(toName.Trim())},");
        var coverHtml = EmailTemplate.BookCoverImage(coverImageUrl ?? "", coverTitle ?? "", coverLinkUrl);
        var htmlBody = greeting + coverHtml + FormatPlainTextAsHtml(body) + UnsubscribeHtml(unsubscribeUrl);
        var plainBody = body.Trim() + UnsubscribePlain(unsubscribeUrl);
        var footer = string.IsNullOrWhiteSpace(unsubscribeUrl)
            ? "You received this because you subscribed to this author's mailing list via BookPromoter AI."
            : "You received this because you subscribed to this author's mailing list via BookPromoter AI. Use the unsubscribe link below to stop receiving emails.";
        return await SendSingleEmail(
            apiKey, senderEmail, fromDisplayName, toEmail, toName, subject, plainBody, htmlBody, appBaseUrl, subject,
            footerNote: footer);
    }

    public static async Task<bool> SendMailingListWelcomeEmail(
        string toEmail,
        string toName,
        string authorName,
        string fromDisplayName,
        string apiKey,
        string senderEmail,
        string senderName,
        string? appBaseUrl = null,
        string? unsubscribeUrl = null)
    {
        var greetingName = string.IsNullOrWhiteSpace(toName) ? "there" : toName.Trim();
        var subject = $"Welcome to {authorName}'s reader list";
        var plainBody = $"""
            Hi {greetingName},

            Thank you for joining my reader mailing list!

            You'll hear from me about new releases, featured books, and updates for readers. I'm glad you're here.

            — {authorName}
            """ + UnsubscribePlain(unsubscribeUrl);

        var htmlBody = EmailTemplate.Paragraph($"Hi {HtmlEncode(greetingName)},")
            + EmailTemplate.Paragraph("Thank you for joining my reader mailing list!")
            + EmailTemplate.Paragraph("You'll hear from me about <strong>new releases</strong>, <strong>featured books</strong>, and updates for readers. I'm glad you're here.")
            + EmailTemplate.Paragraph($"— {HtmlEncode(authorName)}")
            + UnsubscribeHtml(unsubscribeUrl);

        var footer = string.IsNullOrWhiteSpace(unsubscribeUrl)
            ? "You received this because you subscribed to this author's mailing list via BookPromoter AI."
            : "You received this because you subscribed to this author's mailing list via BookPromoter AI. Use the unsubscribe link below to stop receiving emails.";

        return await SendSingleEmail(
            apiKey, senderEmail, fromDisplayName, toEmail, toName, subject, plainBody, htmlBody, appBaseUrl,
            "Welcome to the list", footerNote: footer);
    }

    public static async Task<(int Sent, int Failed)> SendProductUpdateEmailAsync(
        IEnumerable<(string Email, string UnsubscribeToken)> recipients,
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

        foreach (var (email, token) in recipients.DistinctBy(r => r.Email, StringComparer.OrdinalIgnoreCase))
        {
            var unsubUrl = string.IsNullOrWhiteSpace(token)
                ? null
                : $"{appBaseUrl.TrimEnd('/')}/readers/unsubscribe/{Uri.EscapeDataString(token)}";
            var (plain, html) = BuildProductUpdateBodies(update, appBaseUrl, unsubUrl);
            var ok = await SendSingleEmail(
                apiKey, senderEmail, senderName, email, null, subject, plain, html, appBaseUrl,
                string.IsNullOrWhiteSpace(update.Title) ? $"What's New in v{update.Version}" : update.Title,
                footerNote: unsubUrl is null
                    ? null
                    : "You received this because you subscribed to BookPromoter AI updates. Use the unsubscribe link below to stop receiving emails.");
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
        string senderName,
        string? appBaseUrl = null)
    {
        var sent = 0;
        var failed = 0;
        var htmlBody = FormatPlainTextAsHtml(body.Trim());

        foreach (var email in recipientEmails.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var ok = await SendSingleEmail(
                apiKey, senderEmail, senderName, email, null, subject.Trim(), body.Trim(), htmlBody, appBaseUrl, subject.Trim());
            if (ok) sent++; else failed++;
        }

        return (sent, failed);
    }

    static (string Plain, string Html) BuildProductUpdateBodies(ProductUpdate update, string appBaseUrl, string? unsubscribeUrl = null)
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
        plain.Append(UnsubscribePlain(unsubscribeUrl));
        plain.AppendLine();
        plain.AppendLine("— The BookPromoter AI Team");

        var html = new System.Text.StringBuilder();
        html.Append(EmailTemplate.Paragraph("Hi,"));
        html.Append(EmailTemplate.Paragraph(HtmlEncode(string.IsNullOrWhiteSpace(update.Title)
            ? $"We've released BookPromoter AI v{update.Version}."
            : update.Title)));
        AppendHtmlSection(html, "Updated", updated);
        AppendHtmlSection(html, "New", created);
        AppendHtmlSection(html, "Added", added);
        html.Append(EmailTemplate.PrimaryButton(dashboardUrl, "Open Dashboard"));
        html.Append(UnsubscribeHtml(unsubscribeUrl));

        return (plain.ToString(), html.ToString());
    }

    static string UnsubscribePlain(string? unsubscribeUrl)
    {
        if (string.IsNullOrWhiteSpace(unsubscribeUrl)) return "";
        return $"\n\nUnsubscribe from these emails: {unsubscribeUrl}\n";
    }

    static string UnsubscribeHtml(string? unsubscribeUrl)
    {
        if (string.IsNullOrWhiteSpace(unsubscribeUrl)) return "";
        return EmailTemplate.MutedParagraph($"""Don't want these updates? <a href="{HtmlEncode(unsubscribeUrl)}">Unsubscribe</a>.""");
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
        sb.Append($"""<p style="margin:20px 0 8px;font-weight:700;color:#172033;">{HtmlEncode(heading)}</p><ul style="margin:0 0 16px;padding-left:20px;color:#172033;">""");
        foreach (var item in items)
            sb.Append($"""<li style="margin-bottom:6px;">{HtmlEncode(item)}</li>""");
        sb.Append("</ul>");
    }

    static string FormatPlainTextAsHtml(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var html = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                html.Append("""<p style="margin:0 0 16px;">&nbsp;</p>""");
            else
                html.Append(EmailTemplate.Paragraph(HtmlEncode(line)));
        }

        return html.ToString();
    }

    static string HtmlEncode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}
