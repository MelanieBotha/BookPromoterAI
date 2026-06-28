using System.Text;

namespace BookPromoterAI;

static class SocialConnectHelper
{
    public const string OwnerReturnPath = "/owner-promos";

    public static readonly string[] DefaultPlatforms =
        ["Facebook", "X", "Instagram", "LinkedIn", "Pinterest", "TikTok", "Bluesky"];

    static readonly Dictionary<string, string> PlatformColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Facebook"] = "#1877F2",
        ["X"] = "#000000",
        ["Instagram"] = "#E4405F",
        ["LinkedIn"] = "#0A66C2",
        ["Pinterest"] = "#E60023",
        ["TikTok"] = "#000000",
        ["Bluesky"] = "#0085FF"
    };

    public static string ResolveReturnUrl(HttpRequest request, string? formReturn = null)
    {
        var value = !string.IsNullOrWhiteSpace(formReturn)
            ? formReturn
            : request.Query["return"].ToString();
        return IsAllowedReturnUrl(value) ? value : "/my-account";
    }

    public static bool IsAllowedReturnUrl(string? url) =>
        url == OwnerReturnPath || url == "/my-account";

    public static string ConnectButtons(string returnUrl)
    {
        var buttons = new StringBuilder();
        foreach (var platform in DefaultPlatforms)
        {
            var color = PlatformColors.TryGetValue(platform, out var c) ? c : "#0f766e";
            var href = $"/social-accounts/connect/{Uri.EscapeDataString(platform)}?return={Uri.EscapeDataString(returnUrl)}";
            buttons.Append($"""
                <a class="button" href="{href}" style="background:{color}">
                    Connect {H.Encode(platform)}
                </a>
                """);
        }
        return buttons.ToString();
    }

    public static string OAuthAuthorizePage(string platformName, string returnUrl)
    {
        var brands = new Dictionary<string, (string Color, string Initial)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Facebook"] = ("#1877F2", "f"), ["X"] = ("#000000", "X"), ["Instagram"] = ("#E4405F", "IG"),
            ["LinkedIn"] = ("#0A66C2", "in"), ["Pinterest"] = ("#E60023", "P"), ["TikTok"] = ("#000000", "T"),
            ["Bluesky"] = ("#0085FF", "B"),
        };
        var brand = brands.TryGetValue(platformName, out var b) ? b : ("#0f766e", platformName.Length > 0 ? platformName[0].ToString() : "?");
        var cancelHref = returnUrl;
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>Connect your {H.Encode(platformName)} account.</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:{brand.Item1}">{H.Encode(brand.Item2)}</div>
                <h2>Authorize BookPromoter AI</h2>
                <p class="muted">In a live deployment, this redirects you to {H.Encode(platformName)}'s login screen. Real API credentials are not yet configured — enter details below to simulate a connection.</p>
                <form method="post" action="/social-accounts/oauth-callback/{Uri.EscapeDataString(platformName)}" class="form">
                    <input type="hidden" name="return" value="{H.Encode(returnUrl)}">
                    <label>Display Name <input name="displayName" value="{H.Encode(platformName)} Account"></label>
                    <label>Handle <input name="handle" placeholder="yourauthorname" required></label>
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:{brand.Item1}">Simulate &amp; Connect</button>
                        <a class="button secondary" href="{H.Encode(cancelHref)}">Cancel</a>
                    </div>
                </form>
            </section>
            """;
    }
}
