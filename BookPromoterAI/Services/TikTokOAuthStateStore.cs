using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace BookPromoterAI;

sealed class TikTokOAuthPending
{
    public int UserId { get; init; }
    public string ReturnUrl { get; init; } = SocialConnectHelper.VideosReturnPath;
    public string Kind { get; init; } = SocialAccountKinds.Author;
    public string CodeVerifier { get; init; } = "";
}

static class TikTokOAuthStateStore
{
    const string Prefix = "tiktok_oauth:";
    static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task SaveAsync(
        IDistributedCache cache, string state, TikTokOAuthPending pending, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(pending, JsonOptions);
        await cache.SetStringAsync(Prefix + state, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl
        }, cancellationToken);
    }

    public static async Task<TikTokOAuthPending?> TakeAsync(
        IDistributedCache cache, string state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state)) return null;
        var key = Prefix + state;
        var json = await cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return null;
        await cache.RemoveAsync(key, cancellationToken);
        return JsonSerializer.Deserialize<TikTokOAuthPending>(json, JsonOptions);
    }
}
