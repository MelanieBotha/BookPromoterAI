using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace BookPromoterAI;

sealed class XOAuthPending
{
    public int UserId { get; init; }
    public string ReturnUrl { get; init; } = "/my-account";
    public string Kind { get; init; } = SocialAccountKinds.Author;
    public string CodeVerifier { get; init; } = "";
}

static class XOAuthStateStore
{
    const string Prefix = "x_oauth:";
    static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task SaveAsync(
        IDistributedCache cache, string state, XOAuthPending pending, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(pending, JsonOptions);
        await cache.SetStringAsync(Prefix + state, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl
        }, cancellationToken);
    }

    public static async Task<XOAuthPending?> TakeAsync(
        IDistributedCache cache, string state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state)) return null;
        var key = Prefix + state;
        var json = await cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return null;
        await cache.RemoveAsync(key, cancellationToken);
        return JsonSerializer.Deserialize<XOAuthPending>(json, JsonOptions);
    }

    public static string BuildReturnUrl(string returnUrl, string kind)
    {
        if (!SocialConnectHelper.IsAllowedReturnUrl(returnUrl))
            return SocialAccountKinds.IsBrand(kind) ? "/owner-promos?section=owner-social" : "/my-account";

        if (SocialAccountKinds.IsBrand(kind) && returnUrl == SocialConnectHelper.OwnerReturnPath)
            return "/owner-promos?section=owner-social";

        return returnUrl;
    }
}
