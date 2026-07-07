using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace BookPromoterAI;

sealed class TumblrOAuthPending
{
    public int UserId { get; init; }
    public string ReturnUrl { get; init; } = "/my-account";
    public string Kind { get; init; } = SocialAccountKinds.Author;
    public string RequestToken { get; init; } = "";
    public string RequestTokenSecret { get; init; } = "";
}

sealed class TumblrBlogPickPending
{
    public int UserId { get; init; }
    public string ReturnUrl { get; init; } = "/my-account";
    public string Kind { get; init; } = SocialAccountKinds.Author;
    public string AccessToken { get; init; } = "";
    public string AccessTokenSecret { get; init; } = "";
    public string Username { get; init; } = "";
    public List<TumblrBlogPickOption> Blogs { get; init; } = [];
}

sealed class TumblrBlogPickOption
{
    public string Identifier { get; init; } = "";
    public string Title { get; init; } = "";
    public bool Primary { get; init; }
}

static class TumblrOAuthStateStore
{
    const string Prefix = "tumblr_oauth:";
    static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task SaveAsync(
        IDistributedCache cache, string requestToken, TumblrOAuthPending pending,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(pending, JsonOptions);
        await cache.SetStringAsync(Prefix + requestToken, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl
        }, cancellationToken);
    }

    public static async Task<TumblrOAuthPending?> TakeAsync(
        IDistributedCache cache, string requestToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestToken)) return null;
        var key = Prefix + requestToken;
        var json = await cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return null;
        await cache.RemoveAsync(key, cancellationToken);
        return JsonSerializer.Deserialize<TumblrOAuthPending>(json, JsonOptions);
    }
}

static class TumblrBlogPickStateStore
{
    const string Prefix = "tumblr_blog_pick:";
    static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task SaveAsync(
        IDistributedCache cache, string token, TumblrBlogPickPending pending,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(pending, JsonOptions);
        await cache.SetStringAsync(Prefix + token, json, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl
        }, cancellationToken);
    }

    public static async Task<TumblrBlogPickPending?> PeekAsync(
        IDistributedCache cache, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var json = await cache.GetStringAsync(Prefix + token, cancellationToken);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<TumblrBlogPickPending>(json, JsonOptions);
    }

    public static async Task<TumblrBlogPickPending?> TakeAsync(
        IDistributedCache cache, string token, CancellationToken cancellationToken = default)
    {
        var pending = await PeekAsync(cache, token, cancellationToken);
        if (pending is not null)
            await cache.RemoveAsync(Prefix + token, cancellationToken);
        return pending;
    }
}
