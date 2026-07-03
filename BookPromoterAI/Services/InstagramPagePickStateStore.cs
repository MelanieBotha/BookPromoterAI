using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace BookPromoterAI;

sealed class InstagramPagePickPending
{
    public int UserId { get; init; }
    public string ReturnUrl { get; init; } = "/my-account";
    public string Kind { get; init; } = SocialAccountKinds.Author;
    public string UserAccessToken { get; init; } = "";
    public List<InstagramAccountOption> Accounts { get; init; } = [];
}

sealed class InstagramAccountOption
{
    public string PageId { get; init; } = "";
    public string PageName { get; init; } = "";
    public string PageAccessToken { get; init; } = "";
    public string IgUserId { get; init; } = "";
    public string IgUsername { get; init; } = "";
    public string IgDisplayName { get; init; } = "";
}

static class InstagramPagePickStateStore
{
    const string Prefix = "instagram_page_pick:";
    static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task SaveAsync(
        IDistributedCache cache, string token, InstagramPagePickPending pending, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(pending, JsonOptions);
        await cache.SetStringAsync(Prefix + token, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl
        }, cancellationToken);
    }

    public static async Task<InstagramPagePickPending?> TakeAsync(
        IDistributedCache cache, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var key = Prefix + token;
        var json = await cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return null;
        await cache.RemoveAsync(key, cancellationToken);
        return JsonSerializer.Deserialize<InstagramPagePickPending>(json, JsonOptions);
    }

    public static async Task<InstagramPagePickPending?> PeekAsync(
        IDistributedCache cache, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var json = await cache.GetStringAsync(Prefix + token, cancellationToken);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<InstagramPagePickPending>(json, JsonOptions);
    }
}
