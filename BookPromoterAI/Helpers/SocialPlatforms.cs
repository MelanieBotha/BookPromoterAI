namespace BookPromoterAI;

/// <summary>
/// Master registry for every platform in the schedule/connect UI.
/// Mission: wire live auto-posting for each integration kind over time.
/// </summary>
static class SocialPlatforms
{
    public enum Integration
    {
        /// <summary>OAuth or token connect + live posting works today.</summary>
        Live,
        /// <summary>Code ready; needs app credentials in Railway/env.</summary>
        PendingCredentials,
        /// <summary>App password form (Bluesky).</summary>
        AppPassword,
        /// <summary>Webhook URL or bot token (Discord, Telegram).</summary>
        WebhookOrToken,
        /// <summary>Requires video upload (TikTok, YouTube, …).</summary>
        VideoRequired,
        /// <summary>Actively being built — connect flow available.</summary>
        InProgress,
        /// <summary>No public posting API; schedule + copy assist until partner API exists.</summary>
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
        /// <summary>If false, hidden from Owner → Brand Social connect (author My Account only).</summary>
        public bool AllowBrandConnect { get; init; } = true;
        public string DisabledReason { get; init; } = "coming soon";
        public Func<AppSettings, bool>? IsConfigured { get; init; }
    }

    static readonly Definition[] All =
    [
        // Major Platforms
        D("Facebook", "Major Platforms", Integration.Live, "#1877F2", "f"),
        D("Reddit", "Major Platforms", Integration.PendingCredentials, "#FF4500", "R", s => s.IsRedditConfigured, disabledReason: "pending Reddit API approval"),
        D("X (Twitter)", "Major Platforms", Integration.Live, "#000000", "X"),
        D("TikTok", "Major Platforms", Integration.VideoRequired, "#000000", "T", showOnBar: false, disabledReason: "use Videos tab"),
        D("YouTube", "Major Platforms", Integration.VideoRequired, "#FF0000", "YT"),
        D("LinkedIn", "Major Platforms", Integration.Live, "#0A66C2", "in"),
        D("Pinterest", "Major Platforms", Integration.PendingCredentials, "#E60023", "P", s => s.IsPinterestConfigured),
        D("Snapchat", "Major Platforms", Integration.VideoRequired, "#333333", "S"),
        // Emerging
        D("Threads", "Emerging Platforms", Integration.InProgress, "#000000", "@"),
        D("Bluesky", "Emerging Platforms", Integration.AppPassword, "#0085FF", "B"),
        D("Mastodon", "Emerging Platforms", Integration.Live, "#6364FF", "M"),
        D("BeReal", "Emerging Platforms", Integration.Researching, "#000000", "Be"),
        D("Lemon8", "Emerging Platforms", Integration.Researching, "#333333", "L8"),
        D("Nostr", "Emerging Platforms", Integration.Researching, "#8B5CF6", "N"),
        // Messaging
        D("Telegram", "Messaging & Community", Integration.WebhookOrToken, "#26A5E4", "TG"),
        D("WhatsApp", "Messaging & Community", Integration.Researching, "#25D366", "WA"),
        D("Discord", "Messaging & Community", Integration.WebhookOrToken, "#5865F2", "D"),
        D("Quora", "Messaging & Community", Integration.Researching, "#B92B27", "Q"),
        D("Clubhouse", "Messaging & Community", Integration.Researching, "#333333", "CH"),
        // Books
        D("Goodreads", "Books & Reading", Integration.Researching, "#753D15", "GR"),
        D("BookTok", "Books & Reading", Integration.VideoRequired, "#000000", "BT"),
        D("Bookstagram", "Books & Reading", Integration.InProgress, "#E4405F", "Bi"),
        D("Wattpad", "Books & Reading", Integration.Researching, "#FF500A", "W"),
        D("Royal Road", "Books & Reading", Integration.Researching, "#2E5C8A", "RR"),
        D("Scribble Hub", "Books & Reading", Integration.Researching, "#4A6741", "SH"),
        // Content
        D("Substack", "Content & Blogging", Integration.InProgress, "#FF6719", "SS"),
        D("Medium", "Content & Blogging", Integration.AppPassword, "#000000", "Me"),
        D("Tumblr", "Content & Blogging", Integration.Live, "#36465D", "Tu", s => s.IsTumblrConfigured, disabledReason: "add Tumblr API keys in Owner"),
        D("WordPress", "Content & Blogging", Integration.AppPassword, "#21759B", "WP"),
        D("Patreon", "Content & Blogging", Integration.Researching, "#FF424D", "Pa"),
        D("Ko-fi", "Content & Blogging", Integration.Researching, "#29ABE0", "K"),
        // Other
        D("Twitch", "Other", Integration.Researching, "#9146FF", "Tw"),
        D("Rumble", "Other", Integration.Researching, "#85C742", "Ru"),
        D("Kick", "Other", Integration.Researching, "#111111", "K"),
        D("Vimeo", "Other", Integration.VideoRequired, "#1AB7EA", "Vi"),
        D("Flickr", "Other", Integration.PendingCredentials, "#FF0084", "Fl", s => s.IsFlickrConfigured, disabledReason: "Flickr Pro + API keys in Owner"),
        D("MeWe", "Other", Integration.Researching, "#007DA1", "MW"),
        D("VK", "Other", Integration.Researching, "#0077FF", "VK"),
        D("Weibo", "Other", Integration.Researching, "#E6162D", "WB"),
        D("Line", "Other", Integration.Researching, "#00C300", "LN"),
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

    public static IReadOnlyList<string> ConnectNames { get; } =
        All.Select(p => p.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<string> ConnectBarNames { get; } =
        All.Where(p => p.ShowOnConnectBar).Select(p => p.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

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

    /// <summary>Grey out platforms without a live connect + auto-post path yet.</summary>
    public static bool IsDisabled(string? platform, AppSettings? settings = null)
    {
        if (!TryGet(platform, out var def)) return false;
        return def.Integration switch
        {
            Integration.Live => false,
            Integration.AppPassword => false,
            Integration.WebhookOrToken => false,
            Integration.PendingCredentials => def.IsConfigured?.Invoke(settings ?? new AppSettings()) != true,
            Integration.VideoRequired => true,
            Integration.InProgress => true,
            Integration.Researching => true,
            _ => true
        };
    }

    public static bool IsLive(string? platform, AppSettings? settings = null)
    {
        if (!TryGet(platform, out var def)) return false;
        return def.Integration switch
        {
            Integration.Live => true,
            Integration.AppPassword => true,
            Integration.WebhookOrToken => true,
            Integration.PendingCredentials => def.IsConfigured?.Invoke(settings ?? new AppSettings()) == true,
            Integration.VideoRequired => def.IsConfigured?.Invoke(settings ?? new AppSettings()) == true,
            _ => false
        };
    }

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

    public static string? NextPlatformName(AppSettings? settings = null)
    {
        foreach (var def in All.Where(p => p.ShowOnConnectBar).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (IsDisabled(def.Name, settings))
                return def.Name;
        }
        return null;
    }

    public static string? NextPlatformAfter(string platformName, AppSettings? settings = null)
    {
        var found = false;
        foreach (var def in All.Where(p => p.ShowOnConnectBar).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (found && IsDisabled(def.Name, settings))
                return def.Name;
            if (def.Name.Equals(platformName, StringComparison.OrdinalIgnoreCase))
                found = true;
        }
        return null;
    }
}
