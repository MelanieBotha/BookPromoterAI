using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace BookPromoterAI;

sealed class FlickrOAuthPending
{
    public int UserId { get; init; }
    public string ReturnUrl { get; init; } = "";
    public string Kind { get; init; } = SocialAccountKinds.Author;
    public string RequestToken { get; init; } = "";
    public string RequestTokenSecret { get; init; } = "";
}

static class FlickrOAuthStateStore
{
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    static string Key(string requestToken) => $"flickr-oauth:{requestToken}";

    public static async Task SaveAsync(
        IDistributedCache cache, string requestToken, FlickrOAuthPending pending,
        CancellationToken cancellationToken = default) =>
        await cache.SetStringAsync(
            Key(requestToken),
            JsonSerializer.Serialize(pending, JsonOptions),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) },
            cancellationToken);

    public static async Task<FlickrOAuthPending?> TakeAsync(
        IDistributedCache cache, string requestToken,
        CancellationToken cancellationToken = default)
    {
        var key = Key(requestToken);
        var json = await cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return null;
        await cache.RemoveAsync(key, cancellationToken);
        return JsonSerializer.Deserialize<FlickrOAuthPending>(json, JsonOptions);
    }
}
