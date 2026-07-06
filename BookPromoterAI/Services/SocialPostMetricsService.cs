namespace BookPromoterAI;

class SocialPostMetricsService
{
    readonly FacebookService _facebook;
    readonly XService _x;
    readonly BlueskyService _bluesky;
    readonly LinkedInService _linkedIn;

    public SocialPostMetricsService(
        FacebookService facebook,
        XService x,
        BlueskyService bluesky,
        LinkedInService linkedIn)
    {
        _facebook = facebook;
        _x = x;
        _bluesky = bluesky;
        _linkedIn = linkedIn;
    }

    public async Task<bool> RefreshAsync(
        DbPostingLogEntry log, SocialAccount account, CancellationToken cancellationToken = default)
    {
        if (!log.Success || string.IsNullOrWhiteSpace(log.ExternalPostId))
            return false;

        if (log.MetricsFetchedAt is not null &&
            DateTime.UtcNow - log.MetricsFetchedAt.Value < TimeSpan.FromHours(1))
            return false;

        var metrics = await FetchAsync(log.ExternalPostId, log.Platform, account, cancellationToken);
        if (metrics is null) return false;

        log.LikeCount = metrics.LikeCount;
        log.ClickCount = metrics.ClickCount;
        log.MetricsFetchedAt = DateTime.UtcNow;
        return true;
    }

    async Task<SocialPostMetrics?> FetchAsync(
        string externalPostId, string platform, SocialAccount account, CancellationToken cancellationToken)
    {
        if (PostLimits.IsFacebook(platform) && account.IsLiveConnection)
            return await _facebook.TryGetPostMetricsAsync(externalPostId, account.AccessToken ?? "", cancellationToken);

        if (PostLimits.IsX(platform) && account.IsLiveConnection)
            return await _x.TryGetPostMetricsAsync(externalPostId, account.AccessToken ?? "", cancellationToken);

        if (PostLimits.IsBluesky(platform) && account.IsLiveConnection
            && !string.IsNullOrWhiteSpace(account.AccessToken) && !string.IsNullOrWhiteSpace(account.ExternalAccountId))
        {
            var session = new BlueskySession(
                account.AccessToken,
                account.RefreshToken ?? "",
                account.ExternalAccountId,
                account.Handle);
            return await _bluesky.TryGetPostMetricsAsync(externalPostId, session, cancellationToken);
        }

        if (PostLimits.IsLinkedIn(platform) && account.IsLiveConnection)
            return await _linkedIn.TryGetPostMetricsAsync(externalPostId, account.AccessToken ?? "", cancellationToken);

        return null;
    }
}
