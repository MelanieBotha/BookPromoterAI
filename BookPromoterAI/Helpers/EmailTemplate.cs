namespace BookPromoterAI;

static class EmailTemplate
{
    const string DefaultBaseUrl = "https://bookpromoterai.us";
    const string Accent = "#0f766e";
    const string Ink = "#172033";
    const string Muted = "#667085";
    const string Line = "#d7dde8";
    const string Soft = "#f4f7fb";

    public static string Wrap(string? appBaseUrl, string? heading, string bodyHtml, string? footerNote = null, string? logoSrc = null)
    {
        var baseUrl = ResolveBaseUrl(appBaseUrl);
        var logoUrl = string.IsNullOrWhiteSpace(logoSrc) ? PostBranding.AbsoluteLogoUrl(baseUrl) : logoSrc;
        var siteHost = new Uri(baseUrl).Host;
        var headingBlock = string.IsNullOrWhiteSpace(heading)
            ? ""
            : $"""
                <h1 style="margin:0 0 20px;font-size:22px;line-height:1.3;color:{Ink};font-weight:700">{HtmlEncode(heading)}</h1>
                """;
        var footerExtra = string.IsNullOrWhiteSpace(footerNote)
            ? ""
            : $"""<p style="margin:12px 0 0;font-size:12px;color:{Muted}">{HtmlEncode(footerNote)}</p>""";

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <meta http-equiv="X-UA-Compatible" content="IE=edge">
                <title>{HtmlEncode(heading ?? "BookPromoter AI")}</title>
            </head>
            <body style="margin:0;padding:0;background:{Soft};font-family:Arial,Helvetica,sans-serif;-webkit-text-size-adjust:100%;">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{Soft};">
                    <tr>
                        <td align="center" style="padding:32px 16px;">
                            <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" style="max-width:600px;width:100%;background:#ffffff;border:1px solid {Line};border-radius:10px;overflow:hidden;">
                                <tr>
                                    <td align="center" style="padding:28px 32px 20px;background:#ffffff;">
                                        <a href="{HtmlEncode(baseUrl)}" style="text-decoration:none;display:inline-block;">
                                            <img src="{HtmlEncode(logoUrl)}" alt="BookPromoter AI" width="240" style="display:block;max-width:240px;width:100%;height:auto;border:0;">
                                        </a>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="height:4px;background:{Accent};font-size:0;line-height:0;">&nbsp;</td>
                                </tr>
                                <tr>
                                    <td style="padding:32px 36px 28px;color:{Ink};font-size:16px;line-height:1.65;">
                                        {headingBlock}
                                        {bodyHtml}
                                    </td>
                                </tr>
                                <tr>
                                    <td style="padding:24px 36px 28px;background:{Soft};border-top:1px solid {Line};text-align:center;">
                                        <p style="margin:0 0 6px;font-size:14px;font-weight:700;color:{Ink};">Book Promoter AI</p>
                                        <p style="margin:0 0 10px;font-size:13px;color:{Muted};">Promote your books on social media — effortlessly.</p>
                                        <p style="margin:0;font-size:13px;">
                                            <a href="{HtmlEncode(baseUrl)}" style="color:{Accent};text-decoration:none;font-weight:600;">{HtmlEncode(siteHost)}</a>
                                        </p>
                                        {footerExtra}
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;
    }

    public static string PrimaryButton(string href, string label) =>
        $"""
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:24px 0;">
                <tr>
                    <td align="center" style="border-radius:6px;background:{Accent};">
                        <a href="{HtmlEncode(href)}" style="display:inline-block;padding:14px 28px;font-size:16px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:6px;">{HtmlEncode(label)}</a>
                    </td>
                </tr>
            </table>
            """;

    public static string CodeBlock(string code) =>
        $"""
            <p style="margin:16px 0;">
                <span style="display:inline-block;font-size:24px;font-weight:700;letter-spacing:3px;background:{Soft};padding:16px 24px;border-radius:8px;border:1px solid {Line};color:{Ink};">{HtmlEncode(code)}</span>
            </p>
            """;

    public static string MutedParagraph(string text) =>
        $"""<p style="margin:16px 0 0;font-size:13px;color:{Muted};">{text}</p>""";

    public static string Paragraph(string text) =>
        $"""<p style="margin:0 0 16px;">{text}</p>""";

    public static string ResolveBaseUrl(string? appBaseUrl) =>
        string.IsNullOrWhiteSpace(appBaseUrl) ? DefaultBaseUrl : appBaseUrl.TrimEnd('/');

    static string HtmlEncode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}
