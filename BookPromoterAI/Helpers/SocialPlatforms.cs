namespace BookPromoterAI;

/// <summary>
/// Registry of social platforms. UI only shows platforms that are ready to connect
/// (API keys configured in Owner, or no keys needed e.g. Bluesky / Discord / Telegram).
/// </summary>
static class SocialPlatforms
{
    public enum Integration
    {
        Live,
        PendingCredentials,
        AppPassword,
        WebhookOrToken,
        VideoRequired,
        InProgress,
        Researching
    }

    public sealed class Definition
    {
        public required string Name { get; init; }
        public required string Group { get; init; }
        public required Integration Integration { get; init; }
        public string Color { get; init; } = "#0f766e";
        public string BadgeInitial { get; init; } = "?";
        public bool ShowOnConnectBar { get; init; } = true;
        public bool AllowBrandConnect { get; init; } = true;
        public string DisabledReason { get; init; } = "coming soon";
        public Func<AppSettings, bool>? IsConfigured { get; init; }
    }

    static readonly Definition[] All =
    [
        D("Facebook", "Major Platforms", Integration.Live, "#1877F2", "f"),
        D("X (Twitter)", "Major Platforms", Integration.Live, "#000000", "X", s => s.IsXConfigured),
        D("LinkedIn", "Major Platforms", Integration.Live, "#0A66C2", "in", s => s.IsLinkedInConfigured),
        D("Bluesky", "Major Platforms", Integration.AppPassword, "#0085FF", "B"),
        D("Tumblr", "Major Platforms", Integration.Live, "#36465D", "Tu", s => s.IsTumblrConfigured, disabledReason: "add Tumblr API keys in Owner"),
        D("Discord", "Messaging & Community", Integration.WebhookOrToken, "#5865F2", "D"),
        D("Telegram", "Messaging & Community", Integration.WebhookOrToken, "#26A5E4", "TG"),
    ];

    static Definition D(
        string name, string group, Integration integration, string color, string badge,
        Func<AppSettings, bool>? configured = null, bool showOnBar = true, string? disabledReason = null,
        bool allowBrandConnect = true) =>
        new()
        {
            Name = name,
            Group = group,
            Integration = integration,
            Color = color,
            BadgeInitial = badge,
            ShowOnConnectBar = showOnBar,
            AllowBrandConnect = allowBrandConnect,
            IsConfigured = configured,
            DisabledReason = disabledReason ?? "coming soon"
        };

    static readonly Dictionary<string, Definition> ByName =
        All.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<Definition> Catalog => All;

    public static IReadOnlyList<string> ConnectBarNames { get; } =
        All.Where(p => p.ShowOnConnectBar).Select(p => p.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<Definition> ReadyCatalog(AppSettings? settings, bool brandContext = false) =>
        All.Where(p => p.ShowOnConnectBar && IsReadyToConnect(p.Name, settings, brandContext)).ToArray();

    public static IReadOnlyList<string> ReadyConnectBarNames(AppSettings? settings, bool brandContext = false) =>
        ReadyCatalog(settings, brandContext)
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string NormalizeName(string platform)
    {
        if (platform.StartsWith("X (", StringComparison.OrdinalIgnoreCase) ||
            platform.Equals("Twitter", StringComparison.OrdinalIgnoreCase) ||
            platform.Equals("X (Twitter)", StringComparison.OrdinalIgnoreCase))
            return "X (Twitter)";
        return platform.Trim();
    }

    static bool TryGet(string? platform, out Definition definition)
    {
        definition = null!;
        if (string.IsNullOrWhiteSpace(platform)) return false;
        var normalized = NormalizeName(platform);
        if (ByName.TryGetValue(normalized, out definition!)) return true;
        if (normalized.Equals("X", StringComparison.OrdinalIgnoreCase) &&
            ByName.TryGetValue("X (Twitter)", out definition!)) return true;
        return false;
    }

    public static bool IsConnectPlatform(string? platform) => TryGet(platform, out _);

    public static bool AllowsBrandConnect(string? platform) =>
        TryGet(platform, out var def) && def.AllowBrandConnect;

    /// <summary>True when this platform should appear as a connect button or schedule option.</summary>
    public static bool IsReadyToConnect(string? platform, AppSettings? settings, bool brandContext = false)
    {
        if (!TryGet(platform, out var def)) return false;
        settings ??= new AppSettings();

        if (brandContext && !def.AllowBrandConnect) return false;

        return def.Integration switch
        {
            Integration.AppPassword => true,
            Integration.WebhookOrToken => true,
            Integration.Live => IsLiveOAuthReady(def.Name, settings, brandContext),
            Integration.PendingCredentials => def.IsConfigured?.Invoke(settings) == true,
            _ => false
        };
    }

    static bool IsLiveOAuthReady(string name, AppSettings settings, bool brandContext)
    {
        if (name.Equals("Facebook", StringComparison.OrdinalIgnoreCase))
            return brandContext ? settings.IsBrandFacebookOAuthReady : settings.IsFacebookOAuthReady;
        if (PostLimits.IsX(name))
            return settings.IsXConfigured;
        if (PostLimits.IsLinkedIn(name))
            return settings.IsLinkedInConfigured;
        if (PostLimits.IsTumblr(name))
            return settings.IsTumblrConfigured;
        return TryGet(name, out var def) && def.IsConfigured?.Invoke(settings) == true;
    }

    public static bool IsDisabled(string? platform, AppSettings? settings = null) =>
        !IsReadyToConnect(platform, settings);

    public static bool IsLive(string? platform, AppSettings? settings = null) =>
        IsReadyToConnect(platform, settings);

    public static Integration GetIntegration(string? platform) =>
        TryGet(platform, out var def) ? def.Integration : Integration.Researching;

    public static string DisabledReason(string? platform)
    {
        if (!TryGet(platform, out var def)) return "coming soon";
        return def.DisabledReason;
    }

    public static string Color(string? platform) =>
        TryGet(platform, out var def) ? def.Color : "#0f766e";

    public static (string Color, string Initial) Brand(string? platform) =>
        TryGet(platform, out var def) ? (def.Color, def.BadgeInitial) : ("#0f766e", "?");

    public static string? NextPlatformName(AppSettings? settings = null) => null;

    public static string? NextPlatformAfter(string platformName, AppSettings? settings = null) => null;
}
