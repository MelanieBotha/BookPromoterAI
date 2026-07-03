using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace BookPromoterAI;

sealed class FacebookPagePickPending
{
    public int UserId { get; init; }
    public string ReturnUrl { get; init; } = "/my-account";
    public string Kind { get; init; } = SocialAccountKinds.Author;
    public string UserAccessToken { get; init; } = "";
    public List<FacebookPageOption> Pages { get; init; } = [];
}

sealed class FacebookPageOption
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Handle { get; init; } = "";
    public string AccessToken { get; init; } = "";
}

static class FacebookPagePickStateStore
{
    const string Prefix = "facebook_page_pick:";
    static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task SaveAsync(
        IDistributedCache cache, string token, FacebookPagePickPending pending, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(pending, JsonOptions);
        await cache.SetStringAsync(Prefix + token, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl
        }, cancellationToken);
    }

    public static async Task<FacebookPagePickPending?> TakeAsync(
        IDistributedCache cache, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var key = Prefix + token;
        var json = await cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return null;
        await cache.RemoveAsync(key, cancellationToken);
        return JsonSerializer.Deserialize<FacebookPagePickPending>(json, JsonOptions);
    }

    public static async Task<FacebookPagePickPending?> PeekAsync(
        IDistributedCache cache, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var json = await cache.GetStringAsync(Prefix + token, cancellationToken);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<FacebookPagePickPending>(json, JsonOptions);
    }
}
