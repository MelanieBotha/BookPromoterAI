namespace BookPromoterAI;

/// <summary>
/// Single registry for OAuth connect-button platforms.
/// Add a new entry to <see cref="All"/> and a connect button appears automatically (A–Z).
/// </summary>
static class SocialPlatforms
{
    enum Rollout
    {
        /// <summary>Connect button is always active.</summary>
        Live,
        /// <summary>OAuth code exists; grey button until server credentials are configured.</summary>
        PendingCredentials,
        /// <summary>Not built yet; grey button.</summary>
        ComingSoon
    }

    sealed class Definition
    {
        public required string Name { get; init; }
        public required string Color { get; init; }
        public required string BadgeInitial { get; init; }
        public required Rollout Rollout { get; init; }
        public string DisabledReason { get; init; } = "coming soon";
        public Func<AppSettings, bool>? IsConfigured { get; init; }
        public bool SupportsLivePosting { get; init; }
    }

    // ── Add new connect platforms here (one row) ──────────────────────────
    static readonly Definition[] All =
    [
        new() { Name = "Bluesky", Color = "#0085FF", BadgeInitial = "B", Rollout = Rollout.Live, SupportsLivePosting = true },
        new() { Name = "Facebook", Color = "#1877F2", BadgeInitial = "f", Rollout = Rollout.Live, SupportsLivePosting = true },
        new() { Name = "LinkedIn", Color = "#0A66C2", BadgeInitial = "in", Rollout = Rollout.Live, SupportsLivePosting = true },
        new() { Name = "Pinterest", Color = "#E60023", BadgeInitial = "P", Rollout = Rollout.ComingSoon },
        new()
        {
            Name = "Reddit", Color = "#FF4500", BadgeInitial = "R", Rollout = Rollout.PendingCredentials,
            DisabledReason = "pending Reddit API approval", IsConfigured = s => s.IsRedditConfigured, SupportsLivePosting = true
        },
        new() { Name = "TikTok", Color = "#000000", BadgeInitial = "T", Rollout = Rollout.ComingSoon, DisabledReason = "video only — coming soon" },
        new() { Name = "X", Color = "#000000", BadgeInitial = "X", Rollout = Rollout.Live, SupportsLivePosting = true },
    ];

    static readonly Dictionary<string, Definition> ByName =
        All.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ConnectNames { get; } =
        All.Select(p => p.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

    public static string NormalizeName(string platform)
    {
        if (platform.StartsWith("X (", StringComparison.OrdinalIgnoreCase) ||
            platform.Equals("Twitter", StringComparison.OrdinalIgnoreCase) ||
            platform.Equals("X (Twitter)", StringComparison.OrdinalIgnoreCase))
            return "X";
        return platform.Trim();
    }

    static bool TryGet(string? platform, out Definition definition)
    {
        definition = null!;
        if (string.IsNullOrWhiteSpace(platform)) return false;
        return ByName.TryGetValue(NormalizeName(platform), out definition!);
    }

    public static bool IsConnectPlatform(string? platform) => TryGet(platform, out _);

    public static bool IsDisabled(string? platform, AppSettings? settings = null)
    {
        if (!TryGet(platform, out var def)) return false;
        return def.Rollout switch
        {
            Rollout.ComingSoon => true,
            Rollout.PendingCredentials => def.IsConfigured?.Invoke(settings ?? new AppSettings()) != true,
            _ => false
        };
    }

    public static bool IsLive(string? platform, AppSettings? settings = null)
    {
        if (!TryGet(platform, out var def) || !def.SupportsLivePosting) return false;
        if (def.Rollout == Rollout.PendingCredentials)
            return def.IsConfigured?.Invoke(settings ?? new AppSettings()) == true;
        return def.Rollout == Rollout.Live;
    }

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
        foreach (var def in All.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (IsDisabled(def.Name, settings))
                return def.Name;
        }
        return null;
    }

    public static string? NextPlatformAfter(string platformName, AppSettings? settings = null)
    {
        var found = false;
        foreach (var def in All.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (found && IsDisabled(def.Name, settings))
                return def.Name;
            if (def.Name.Equals(platformName, StringComparison.OrdinalIgnoreCase))
                found = true;
        }
        return null;
    }
}
