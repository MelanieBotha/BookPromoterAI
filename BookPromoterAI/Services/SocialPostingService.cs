namespace BookPromoterAI;

// =====================================================================
// SOCIAL MEDIA POSTING SERVICE
//
// Bluesky uses live AT Protocol posting; X, LinkedIn, Facebook, and Reddit use OAuth. Other platforms
// remain simulated until OAuth is wired up for each one.
// =====================================================================
class SocialPostingService
{
    readonly BlueskyService _bluesky;
    readonly XService _x;
    readonly LinkedInService _linkedIn;
    readonly FacebookService _facebook;
    readonly RedditService _reddit;
    readonly HttpClient _http;
    readonly UploadPaths _uploads;

    public SocialPostingService(
        BlueskyService bluesky,
        XService x,
        LinkedInService linkedIn,
        FacebookService facebook,
        RedditService reddit,
        IHttpClientFactory httpFactory,
        UploadPaths uploads)
    {
        _bluesky = bluesky;
        _x = x;
        _linkedIn = linkedIn;
        _facebook = facebook;
        _reddit = reddit;
        _uploads = uploads;
        _http = httpFactory.CreateClient(nameof(SocialPostingService));
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<PostingOutcome> PostAsync(
        SocialAccount account,
        string postText,
        BookPostMedia? media = null,
        BrandPostMedia? brandMedia = null,
        CancellationToken cancellationToken = default)
    {
        if (PostLimits.IsBluesky(account.Platform) && account.IsLiveConnection)
            return await PostToBlueskyLive(account, postText, media, brandMedia, cancellationToken);

        if (PostLimits.IsX(account.Platform) && account.IsLiveConnection)
            return await PostToXLive(account, postText, brandMedia, cancellationToken);

        if (PostLimits.IsLinkedIn(account.Platform) && account.IsLiveConnection)
            return await PostToLinkedInLive(account, postText, brandMedia, cancellationToken);

        if (PostLimits.IsFacebook(account.Platform) && account.IsLiveConnection)
            return await PostToFacebookLive(account, postText, media, brandMedia, cancellationToken);

        if (PostLimits.IsReddit(account.Platform) && account.IsLiveConnection)
            return await PostToRedditLive(account, postText, cancellationToken);

        var result = account.Platform.ToLowerInvariant() switch
        {
            "facebook" => await PostToFacebook(account, postText),
            "reddit" => await PostToReddit(account, postText),
            "x" or "x (twitter)" or "twitter" => await PostToX(account, postText),
            "bluesky" => await PostToBluesky(account, postText),
            "linkedin" => await PostToLinkedIn(account, postText),
            "pinterest" => await PostToPinterest(account, postText),
            "tiktok" => await PostToTikTok(account, postText),
            _ => await PostGeneric(account, postText)
        };
        return new PostingOutcome { Result = result };
    }

    async Task<PostingOutcome> PostToBlueskyLive(
        SocialAccount account,
        string postText,
        BookPostMedia? media,
        BrandPostMedia? brandMedia,
        CancellationToken cancellationToken)
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

        BlueskyImageAttachment? image = null;
        if (media is not null)
        {
            var baseUrl = ResolveBaseUrl(media.AppBaseUrl);
            image = await BookCoverLoader.TryLoadAsync(
                _http,
                _uploads.Path,
                baseUrl,
                media.BookTitle,
                media.CoverImageUrl,
                media.TrackingCode,
                cancellationToken);
        }
        else if (brandMedia is not null)
        {
            image = await BrandLogoLoader.TryLoadAsync(
                _http,
                ResolveBaseUrl(brandMedia.AppBaseUrl),
                cancellationToken);
        }

        var session = new BlueskySession(
            account.AccessToken,
            account.RefreshToken ?? "",
            account.ExternalAccountId,
            account.Handle);

        var (result, updated) = await _bluesky.PostAsync(session, postText, image, cancellationToken);
        return new PostingOutcome
        {
            Result = result,
            AccessToken = updated?.AccessJwt,
            RefreshToken = updated?.RefreshJwt
        };
    }

    async Task<PostingOutcome> PostToXLive(
        SocialAccount account,
        string postText,
        BrandPostMedia? brandMedia,
        CancellationToken cancellationToken)
    {
        if (!PostLimits.IsWithinLimit(postText, account.Platform))
        {
            return new PostingOutcome
            {
                Result = PostingResult.Failure(
                    $"Post exceeds X's {PostLimits.XMaxGraphemes}-character limit ({PostLimits.GraphemeLength(postText)} graphemes). Regenerate or shorten the post.")
            };
        }

        if (string.IsNullOrWhiteSpace(account.AccessToken) || string.IsNullOrWhiteSpace(account.ExternalAccountId))
            return new PostingOutcome { Result = PostingResult.Failure("X is not connected. Reconnect your account in My Account.") };

        byte[]? imageBytes = null;
        string? imageMime = null;
        if (brandMedia is not null)
        {
            var logo = await BrandLogoLoader.TryLoadAsync(
                _http, ResolveBaseUrl(brandMedia.AppBaseUrl), cancellationToken);
            if (logo is not null)
            {
                imageBytes = logo.Data;
                imageMime = logo.MimeType;
            }
        }

        var tokens = new XTokenSet(account.AccessToken, account.RefreshToken ?? "", 0);
        var (result, updated) = await _x.PostAsync(tokens, postText, imageBytes, imageMime, cancellationToken);
        return new PostingOutcome
        {
            Result = result,
            AccessToken = updated?.AccessToken,
            RefreshToken = updated?.RefreshToken
        };
    }

    async Task<PostingOutcome> PostToLinkedInLive(
        SocialAccount account,
        string postText,
        BrandPostMedia? brandMedia,
        CancellationToken cancellationToken)
    {
        if (!PostLimits.IsWithinLimit(postText, account.Platform))
        {
            return new PostingOutcome
            {
                Result = PostingResult.Failure(
                    $"Post exceeds LinkedIn's {PostLimits.LinkedInMaxGraphemes}-character limit ({PostLimits.GraphemeLength(postText)} graphemes). Regenerate or shorten the post.")
            };
        }

        if (string.IsNullOrWhiteSpace(account.AccessToken) || string.IsNullOrWhiteSpace(account.ExternalAccountId))
            return new PostingOutcome { Result = PostingResult.Failure("LinkedIn is not connected. Reconnect your account in My Account.") };

        byte[]? imageBytes = null;
        string? imageMime = null;
        if (brandMedia is not null)
        {
            var logo = await BrandLogoLoader.TryLoadAsync(
                _http, ResolveBaseUrl(brandMedia.AppBaseUrl), cancellationToken);
            if (logo is not null)
            {
                imageBytes = logo.Data;
                imageMime = logo.MimeType;
            }
        }

        var tokens = new LinkedInTokenSet(account.AccessToken, account.RefreshToken ?? "", 0);
        var (result, updated) = await _linkedIn.PostAsync(
            tokens, account.ExternalAccountId, postText, imageBytes, imageMime, cancellationToken);
        return new PostingOutcome
        {
            Result = result,
            AccessToken = updated?.AccessToken,
            RefreshToken = updated?.RefreshToken
        };
    }

    async Task<PostingOutcome> PostToFacebookLive(
        SocialAccount account,
        string postText,
        BookPostMedia? media,
        BrandPostMedia? brandMedia,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account.AccessToken) || string.IsNullOrWhiteSpace(account.ExternalAccountId))
            return new PostingOutcome { Result = PostingResult.Failure("Facebook is not connected. Connect your author Page in My Account (personal Facebook — not business portfolio).") };

        string? photoUrl = null;
        byte[]? photoBytes = null;
        string? photoMime = null;
        if (media is not null)
        {
            var baseUrl = ResolveBaseUrl(media.AppBaseUrl);
            if (!string.IsNullOrWhiteSpace(media.TrackingCode))
                photoUrl = PostBranding.BookCoverShareUrl(baseUrl, media.TrackingCode);
            else if (!string.IsNullOrWhiteSpace(media.CoverImageUrl))
                photoUrl = PostBranding.AbsoluteImageUrl(baseUrl, media.CoverImageUrl);

            var image = await BookCoverLoader.TryLoadAsync(
                _http,
                _uploads.Path,
                baseUrl,
                media.BookTitle,
                media.CoverImageUrl,
                media.TrackingCode,
                cancellationToken);
            if (image is not null)
            {
                photoBytes = image.Data;
                photoMime = image.MimeType;
            }
        }
        else if (brandMedia is not null)
        {
            var baseUrl = ResolveBaseUrl(brandMedia.AppBaseUrl);
            photoUrl = BrandLogoLoader.PublicLogoUrl(baseUrl);
            var logo = await BrandLogoLoader.TryLoadAsync(_http, baseUrl, cancellationToken);
            if (logo is not null)
            {
                photoBytes = logo.Data;
                photoMime = logo.MimeType;
            }
        }

        var connection = new FacebookPageConnection(
            new FacebookPage(account.ExternalAccountId, account.DisplayName, account.Handle, account.AccessToken),
            account.RefreshToken ?? "");

        var (result, updated) = await _facebook.PostAsync(
            connection, postText, photoUrl, photoBytes, photoMime, cancellationToken);
        return new PostingOutcome
        {
            Result = result,
            AccessToken = updated?.PageAccessToken,
            RefreshToken = updated?.UserAccessToken
        };
    }

    async Task<PostingOutcome> PostToRedditLive(
        SocialAccount account,
        string postText,
        CancellationToken cancellationToken)
    {
        if (!PostLimits.IsWithinLimit(postText, account.Platform))
        {
            return new PostingOutcome
            {
                Result = PostingResult.Failure(
                    $"Post exceeds Reddit's {PostLimits.RedditMaxGraphemes}-character limit ({PostLimits.GraphemeLength(postText)} graphemes). Regenerate or shorten the post.")
            };
        }

        if (string.IsNullOrWhiteSpace(account.AccessToken))
            return new PostingOutcome { Result = PostingResult.Failure("Reddit is not connected. Reconnect your account in My Account.") };

        var tokens = new RedditTokenSet(account.AccessToken, account.RefreshToken ?? "");
        var result = await _reddit.PostAsync(tokens, account.Handle, postText, cancellationToken);
        return new PostingOutcome { Result = result };
    }

    static string ResolveBaseUrl(string? appBaseUrl)
    {
        var baseUrl = appBaseUrl?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? "https://bookpromoterai.us" : baseUrl;
    }

    async Task<PostingResult> PostToFacebook(SocialAccount account, string postText)
    {
        await Task.CompletedTask;
        return PostingResult.Failure("Connect Facebook with OAuth in My Account for live Page posting.");
    }

    async Task<PostingResult> PostToReddit(SocialAccount account, string postText)
    {
        if (!PostLimits.IsWithinLimit(postText, account.Platform))
            return PostingResult.Failure($"Post exceeds Reddit's {PostLimits.RedditMaxGraphemes}-character limit ({PostLimits.GraphemeLength(postText)} graphemes). Regenerate or shorten the post.");

        await Task.CompletedTask;
        return PostingResult.Failure("Connect Reddit with OAuth in My Account for live posting.");
    }

    async Task<PostingResult> PostToX(SocialAccount account, string postText)
    {
        if (!PostLimits.IsWithinLimit(postText, account.Platform))
            return PostingResult.Failure($"Post exceeds X's {PostLimits.XMaxGraphemes}-character limit ({PostLimits.GraphemeLength(postText)} graphemes). Regenerate or shorten the post.");

        await Task.CompletedTask;
        return PostingResult.Failure("Connect X with OAuth in My Account for live posting.");
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
        if (!PostLimits.IsWithinLimit(postText, account.Platform))
            return PostingResult.Failure($"Post exceeds LinkedIn's {PostLimits.LinkedInMaxGraphemes}-character limit ({PostLimits.GraphemeLength(postText)} graphemes). Regenerate or shorten the post.");

        await Task.CompletedTask;
        return PostingResult.Failure("Connect LinkedIn with OAuth in My Account for live posting.");
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
