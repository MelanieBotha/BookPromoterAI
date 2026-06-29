namespace BookPromoterAI;

// =====================================================================
// SOCIAL MEDIA POSTING SERVICE
//
// Bluesky uses live AT Protocol posting when the account was connected
// with an app password. Other platforms remain simulated until OAuth is
// wired up for each one.
// =====================================================================
class SocialPostingService
{
    readonly BlueskyService _bluesky;

    public SocialPostingService(BlueskyService bluesky) => _bluesky = bluesky;

    public async Task<PostingOutcome> PostAsync(SocialAccount account, string postText)
    {
        if (PostLimits.IsBluesky(account.Platform) && account.IsLiveConnection)
            return await PostToBlueskyLive(account, postText);

        var result = account.Platform.ToLowerInvariant() switch
        {
            "facebook" => await PostToFacebook(account, postText),
            "instagram" => await PostToInstagram(account, postText),
            "x" or "x (twitter)" or "twitter" => await PostToX(account, postText),
            "bluesky" => await PostToBluesky(account, postText),
            "linkedin" => await PostToLinkedIn(account, postText),
            "pinterest" => await PostToPinterest(account, postText),
            "tiktok" => await PostToTikTok(account, postText),
            _ => await PostGeneric(account, postText)
        };
        return new PostingOutcome { Result = result };
    }

    async Task<PostingOutcome> PostToBlueskyLive(SocialAccount account, string postText)
    {
        if (!PostLimits.IsWithinLimit(postText, "Bluesky"))
        {
            return new PostingOutcome
            {
                Result = PostingResult.Failure(
                    $"Post exceeds Bluesky's {PostLimits.BlueskyMaxGraphemes}-character limit ({PostLimits.GraphemeLength(postText)} graphemes). Regenerate or shorten the post.")
            };
        }

        if (string.IsNullOrWhiteSpace(account.AccessToken) || string.IsNullOrWhiteSpace(account.ExternalAccountId))
            return new PostingOutcome { Result = PostingResult.Failure("Bluesky is not connected. Reconnect your account in My Account.") };

        var session = new BlueskySession(
            account.AccessToken,
            account.RefreshToken ?? "",
            account.ExternalAccountId,
            account.Handle);

        var (result, updated) = await _bluesky.PostAsync(session, postText);
        return new PostingOutcome
        {
            Result = result,
            AccessToken = updated?.AccessJwt,
            RefreshToken = updated?.RefreshJwt
        };
    }

    async Task<PostingResult> PostToFacebook(SocialAccount account, string postText)
    {
        await Task.CompletedTask;
        return PostingResult.SimulatedOk("(Simulated) Posted to Facebook Page.");
    }

    async Task<PostingResult> PostToInstagram(SocialAccount account, string postText)
    {
        await Task.CompletedTask;
        return PostingResult.SimulatedOk("(Simulated) Posted to Instagram.");
    }

    async Task<PostingResult> PostToX(SocialAccount account, string postText)
    {
        await Task.CompletedTask;
        return PostingResult.SimulatedOk("(Simulated) Posted to X.");
    }

    async Task<PostingResult> PostToBluesky(SocialAccount account, string postText)
    {
        if (!PostLimits.IsWithinLimit(postText, "Bluesky"))
            return PostingResult.Failure($"Post exceeds Bluesky's {PostLimits.BlueskyMaxGraphemes}-character limit ({PostLimits.GraphemeLength(postText)} graphemes). Regenerate or shorten the post.");

        await Task.CompletedTask;
        return PostingResult.Failure("Connect Bluesky with an app password in My Account for live posting.");
    }

    async Task<PostingResult> PostToLinkedIn(SocialAccount account, string postText)
    {
        await Task.CompletedTask;
        return PostingResult.SimulatedOk("(Simulated) Posted to LinkedIn.");
    }

    async Task<PostingResult> PostToPinterest(SocialAccount account, string postText)
    {
        await Task.CompletedTask;
        return PostingResult.SimulatedOk("(Simulated) Posted to Pinterest.");
    }

    async Task<PostingResult> PostToTikTok(SocialAccount account, string postText)
    {
        await Task.CompletedTask;
        return PostingResult.SimulatedOk("(Simulated) Posted to TikTok. Note: TikTok requires video content for real posts.");
    }

    async Task<PostingResult> PostGeneric(SocialAccount account, string postText)
    {
        await Task.CompletedTask;
        return PostingResult.SimulatedOk($"(Simulated) Posted to {account.Platform}. No dedicated API integration configured for this platform yet.");
    }
}

class PostingResult
{
    public bool Success { get; init; }
    public bool IsSimulated { get; init; }
    public string Message { get; init; } = "";

    /// <summary>True only when content was sent to a real platform API (not simulated).</summary>
    public bool PostedToFeed => Success && !IsSimulated;

    public static PostingResult LiveOk(string message = "Posted successfully.") =>
        new() { Success = true, IsSimulated = false, Message = message };

    public static PostingResult SimulatedOk(string message) =>
        new() { Success = true, IsSimulated = true, Message = message };

    public static PostingResult Ok(string message = "Posted successfully.") => LiveOk(message);

    public static PostingResult Failure(string message) => new() { Success = false, Message = message };
}
