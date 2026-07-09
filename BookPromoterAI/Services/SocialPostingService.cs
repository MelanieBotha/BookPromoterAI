namespace BookPromoterAI;

// =====================================================================
// SOCIAL MEDIA POSTING SERVICE
//
// Bluesky, X, LinkedIn, Facebook, Reddit, Mastodon, Discord, and Telegram use live posting.
// Other platforms remain simulated until each integration is wired up.
// =====================================================================
class SocialPostingService
{
    readonly BlueskyService _bluesky;
    readonly XService _x;
    readonly LinkedInService _linkedIn;
    readonly FacebookService _facebook;
    readonly RedditService _reddit;
    readonly MastodonService _mastodon;
    readonly DiscordTelegramPostingService _messaging;
    readonly TumblrService _tumblr;
    readonly WordPressService _wordpress;
    readonly MediumService _medium;
    readonly FlickrService _flickr;
    readonly HttpClient _http;
    readonly UploadPaths _uploads;

    public SocialPostingService(
        BlueskyService bluesky,
        XService x,
        LinkedInService linkedIn,
        FacebookService facebook,
        RedditService reddit,
        MastodonService mastodon,
        DiscordTelegramPostingService messaging,
        TumblrService tumblr,
        WordPressService wordpress,
        MediumService medium,
        FlickrService flickr,
        IHttpClientFactory httpFactory,
        UploadPaths uploads)
    {
        _bluesky = bluesky;
        _x = x;
        _linkedIn = linkedIn;
        _facebook = facebook;
        _reddit = reddit;
        _mastodon = mastodon;
        _messaging = messaging;
        _tumblr = tumblr;
        _wordpress = wordpress;
        _medium = medium;
        _flickr = flickr;
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

        if (PostLimits.IsMastodon(account.Platform) && account.IsLiveConnection)
            return await PostToMastodonLive(account, postText, media, brandMedia, cancellationToken);

        if (PostLimits.IsDiscord(account.Platform) && account.IsLiveConnection)
            return await PostToDiscordLive(account, postText, media, brandMedia, cancellationToken);

        if (PostLimits.IsTelegram(account.Platform) && account.IsLiveConnection)
            return await PostToTelegramLive(account, postText, media, brandMedia, cancellationToken);

        if (PostLimits.IsTumblr(account.Platform) && account.IsLiveConnection)
            return await PostToTumblrLive(account, postText, media, brandMedia, cancellationToken);

        if (PostLimits.IsWordPress(account.Platform) && account.IsLiveConnection)
            return await PostToWordPressLive(account, postText, media, brandMedia, cancellationToken);

        if (PostLimits.IsMedium(account.Platform) && account.IsLiveConnection)
            return await PostToMediumLive(account, postText, media, brandMedia, cancellationToken);

        if (PostLimits.IsFlickr(account.Platform) && account.IsLiveConnection)
            return await PostToFlickrLive(account, postText, media, brandMedia, cancellationToken);

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

    async Task<PostingOutcome> PostToMastodonLive(
        SocialAccount account,
        string postText,
        BookPostMedia? media,
        BrandPostMedia? brandMedia,
        CancellationToken cancellationToken)
    {
        if (!PostLimits.IsWithinLimit(postText, account.Platform))
        {
            return new PostingOutcome
            {
                Result = PostingResult.Failure(
                    $"Post exceeds Mastodon's {PostLimits.MastodonMaxGraphemes}-character limit ({PostLimits.GraphemeLength(postText)} graphemes). Regenerate or shorten the post.")
            };
        }

        if (string.IsNullOrWhiteSpace(account.AccessToken))
            return new PostingOutcome { Result = PostingResult.Failure("Mastodon is not connected. Reconnect your account in My Account.") };

        var instance = MastodonService.InstanceFromAcct(account.Handle);
        if (string.IsNullOrWhiteSpace(instance))
            return new PostingOutcome { Result = PostingResult.Failure("Mastodon server is missing. Reconnect your account in My Account.") };

        byte[]? imageBytes = null;
        string? imageMime = null;
        if (media is not null)
        {
            var baseUrl = ResolveBaseUrl(media.AppBaseUrl);
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
                imageBytes = image.Data;
                imageMime = image.MimeType;
            }
        }
        else if (brandMedia is not null)
        {
            var logo = await BrandLogoLoader.TryLoadAsync(
                _http, ResolveBaseUrl(brandMedia.AppBaseUrl), cancellationToken);
            if (logo is not null)
            {
                imageBytes = logo.Data;
                imageMime = logo.MimeType;
            }
        }

        var result = await _mastodon.PostAsync(
            instance, account.AccessToken, postText, imageBytes, imageMime, cancellationToken);
        return new PostingOutcome { Result = result };
    }

    async Task<PostingOutcome> PostToDiscordLive(
        SocialAccount account,
        string postText,
        BookPostMedia? media,
        BrandPostMedia? brandMedia,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account.AccessToken))
            return new PostingOutcome { Result = PostingResult.Failure("Discord is not connected. Reconnect your webhook in My Account.") };

        byte[]? imageBytes = null;
        string? imageMime = null;
        string? fileName = null;
        if (media is not null)
        {
            var baseUrl = ResolveBaseUrl(media.AppBaseUrl);
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
                imageBytes = image.Data;
                imageMime = image.MimeType;
                fileName = BuildMessagingFileName(media.BookTitle, imageMime);
            }
        }
        else if (brandMedia is not null)
        {
            var logo = await BrandLogoLoader.TryLoadAsync(
                _http,
                ResolveBaseUrl(brandMedia.AppBaseUrl),
                cancellationToken);
            if (logo is not null)
            {
                imageBytes = logo.Data;
                imageMime = logo.MimeType;
                fileName = BuildMessagingFileName("bookpromoter-ai-logo", imageMime);
            }
        }

        var result = await _messaging.PostDiscordWebhookAsync(
            account.AccessToken,
            postText,
            imageBytes,
            imageMime,
            fileName,
            cancellationToken);
        return new PostingOutcome { Result = result };
    }

    async Task<PostingOutcome> PostToTelegramLive(
        SocialAccount account,
        string postText,
        BookPostMedia? media,
        BrandPostMedia? brandMedia,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account.AccessToken) || string.IsNullOrWhiteSpace(account.ExternalAccountId))
            return new PostingOutcome { Result = PostingResult.Failure("Telegram is not connected. Reconnect your bot in My Account.") };

        if (!PostLimits.IsWithinLimit(postText, account.Platform))
        {
            return new PostingOutcome
            {
                Result = PostingResult.Failure(
                    $"Post exceeds Telegram's {PostLimits.TelegramMaxGraphemes}-character limit ({PostLimits.GraphemeLength(postText)} characters). Regenerate or shorten the post.")
            };
        }

        byte[]? imageBytes = null;
        string? imageMime = null;
        string? fileName = null;
        if (media is not null)
        {
            var baseUrl = ResolveBaseUrl(media.AppBaseUrl);
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
                imageBytes = image.Data;
                imageMime = image.MimeType;
                fileName = BuildMessagingFileName(media.BookTitle, imageMime);
            }
        }
        else if (brandMedia is not null)
        {
            var logo = await BrandLogoLoader.TryLoadAsync(
                _http,
                ResolveBaseUrl(brandMedia.AppBaseUrl),
                cancellationToken);
            if (logo is not null)
            {
                imageBytes = logo.Data;
                imageMime = logo.MimeType;
                fileName = BuildMessagingFileName("bookpromoter-ai-logo", imageMime);
            }
        }

        var result = await _messaging.PostTelegramAsync(
            account.AccessToken,
            account.ExternalAccountId,
            postText,
            imageBytes,
            imageMime,
            fileName,
            cancellationToken);
        return new PostingOutcome { Result = result };
    }

    async Task<PostingOutcome> PostToTumblrLive(
        SocialAccount account,
        string postText,
        BookPostMedia? media,
        BrandPostMedia? brandMedia,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account.AccessToken) ||
            string.IsNullOrWhiteSpace(account.RefreshToken) ||
            string.IsNullOrWhiteSpace(account.ExternalAccountId))
            return new PostingOutcome { Result = PostingResult.Failure("Tumblr is not connected. Reconnect your account in My Account or Owner.") };

        var baseUrl = ResolveBaseUrl(media?.AppBaseUrl ?? brandMedia?.AppBaseUrl);
        string? imageUrl = null;
        string? clickThruUrl = null;
        string? tags = null;
        var isBrand = brandMedia is not null;

        if (media is not null)
        {
            if (!string.IsNullOrWhiteSpace(media.TrackingCode))
            {
                imageUrl = PostBranding.BookCoverShareUrl(baseUrl, media.TrackingCode);
                clickThruUrl = PostBranding.BookShareUrl(baseUrl, media.TrackingCode, "Tumblr");
            }
            else if (!string.IsNullOrWhiteSpace(media.CoverImageUrl))
            {
                imageUrl = PostBranding.AbsoluteImageUrl(baseUrl, media.CoverImageUrl);
            }

            tags = TumblrPostFormatter.BuildTags(media.BookTitle, media.AuthorName, media.Genre);
        }
        else if (brandMedia is not null)
        {
            imageUrl = PostBranding.AbsoluteLogoUrl(baseUrl);
            clickThruUrl = $"{baseUrl}/start";
            tags = TumblrPostFormatter.BuildBrandTags();
        }

        var htmlBody = TumblrPostFormatter.ToHtmlCaption(postText, baseUrl, includeAppCta: !isBrand);
        var tokens = new TumblrTokenSet(account.AccessToken, account.RefreshToken);
        var result = await _tumblr.PostAsync(
            tokens, account.ExternalAccountId, htmlBody, imageUrl, clickThruUrl, tags, cancellationToken);
        return new PostingOutcome { Result = result };
    }

    async Task<PostingOutcome> PostToWordPressLive(
        SocialAccount account,
        string postText,
        BookPostMedia? media,
        BrandPostMedia? brandMedia,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account.AccessToken) ||
            string.IsNullOrWhiteSpace(account.ExternalAccountId) ||
            string.IsNullOrWhiteSpace(account.Handle))
            return new PostingOutcome { Result = PostingResult.Failure("WordPress is not connected. Reconnect your site in My Account or Owner.") };

        var baseUrl = ResolveBaseUrl(media?.AppBaseUrl ?? brandMedia?.AppBaseUrl);
        var isBrand = brandMedia is not null;
        var connection = new WordPressConnection(
            account.ExternalAccountId,
            account.Handle,
            account.AccessToken,
            account.DisplayName);

        byte[]? imageBytes = null;
        string? imageMime = null;
        string? imageFileName = null;
        if (media is not null)
        {
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
                imageBytes = image.Data;
                imageMime = image.MimeType;
                imageFileName = "book-cover.jpg";
            }
        }
        else if (brandMedia is not null)
        {
            var logo = await BrandLogoLoader.TryLoadAsync(_http, baseUrl, cancellationToken);
            if (logo is not null)
            {
                imageBytes = logo.Data;
                imageMime = logo.MimeType;
                imageFileName = "bookpromoter-ai-logo.png";
            }
        }

        var title = WordPressPostFormatter.BuildTitle(postText, media?.BookTitle, isBrand);
        var html = WordPressPostFormatter.ToHtmlContent(postText, baseUrl, isBrand);
        var result = await _wordpress.PostAsync(
            connection, title, html, imageBytes, imageMime, imageFileName, cancellationToken);
        return new PostingOutcome { Result = result };
    }

    async Task<PostingOutcome> PostToMediumLive(
        SocialAccount account,
        string postText,
        BookPostMedia? media,
        BrandPostMedia? brandMedia,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account.AccessToken) || string.IsNullOrWhiteSpace(account.ExternalAccountId))
            return new PostingOutcome { Result = PostingResult.Failure("Medium is not connected. Reconnect your account in My Account or Owner.") };

        var baseUrl = ResolveBaseUrl(media?.AppBaseUrl ?? brandMedia?.AppBaseUrl);
        var isBrand = brandMedia is not null;
        var connection = new MediumConnection(
            account.ExternalAccountId,
            account.Handle ?? "",
            account.DisplayName,
            account.AccessToken);

        string? heroImageUrl = null;
        string? heroAlt = null;
        if (media is not null)
        {
            if (!string.IsNullOrWhiteSpace(media.TrackingCode))
            {
                heroImageUrl = PostBranding.BookCoverShareUrl(baseUrl, media.TrackingCode);
                heroAlt = $"{media.BookTitle} cover";
            }
            else if (!string.IsNullOrWhiteSpace(media.CoverImageUrl))
            {
                heroImageUrl = PostBranding.AbsoluteImageUrl(baseUrl, media.CoverImageUrl);
                heroAlt = $"{media.BookTitle} cover";
            }
        }
        else if (brandMedia is not null)
        {
            heroImageUrl = BrandLogoLoader.PublicLogoUrl(baseUrl);
            heroAlt = "BookPromoter AI";
        }

        if (heroImageUrl is null)
        {
            byte[]? imageBytes = null;
            string? imageMime = null;
            string? imageFileName = null;
            if (media is not null)
            {
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
                    imageBytes = image.Data;
                    imageMime = image.MimeType;
                    imageFileName = "book-cover.jpg";
                    heroAlt = $"{media.BookTitle} cover";
                }
            }
            else if (brandMedia is not null)
            {
                var logo = await BrandLogoLoader.TryLoadAsync(_http, baseUrl, cancellationToken);
                if (logo is not null)
                {
                    imageBytes = logo.Data;
                    imageMime = logo.MimeType;
                    imageFileName = "bookpromoter-ai-logo.png";
                    heroAlt = "BookPromoter AI";
                }
            }

            if (imageBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(imageMime))
            {
                var upload = await _medium.UploadImageAsync(
                    connection, imageBytes, imageMime, imageFileName, cancellationToken);
                if (upload.Ok)
                    heroImageUrl = upload.Url;
            }
        }

        var title = MediumPostFormatter.BuildTitle(postText, media?.BookTitle, isBrand);
        var html = MediumPostFormatter.ToHtmlContent(postText, baseUrl, isBrand, heroImageUrl, heroAlt);
        var tags = MediumPostFormatter.BuildTags(isBrand, media?.Genre);
        var result = await _medium.PostAsync(connection, title, html, tags, cancellationToken);
        return new PostingOutcome { Result = result };
    }

    async Task<PostingOutcome> PostToFlickrLive(
        SocialAccount account,
        string postText,
        BookPostMedia? media,
        BrandPostMedia? brandMedia,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(account.AccessToken) ||
            string.IsNullOrWhiteSpace(account.RefreshToken) ||
            string.IsNullOrWhiteSpace(account.ExternalAccountId))
            return new PostingOutcome { Result = PostingResult.Failure("Flickr is not connected. Reconnect your account in My Account or Owner.") };

        var baseUrl = ResolveBaseUrl(media?.AppBaseUrl ?? brandMedia?.AppBaseUrl);
        var isBrand = brandMedia is not null;
        var tokens = new FlickrTokenSet(
            account.AccessToken,
            account.RefreshToken,
            account.ExternalAccountId,
            account.Handle ?? "",
            account.DisplayName);

        byte[]? imageBytes = null;
        string? imageMime = null;
        if (media is not null)
        {
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
                imageBytes = image.Data;
                imageMime = image.MimeType;
            }
        }
        else if (brandMedia is not null)
        {
            var logo = await BrandLogoLoader.TryLoadAsync(_http, baseUrl, cancellationToken);
            if (logo is not null)
            {
                imageBytes = logo.Data;
                imageMime = logo.MimeType;
            }
        }

        if (imageBytes is null || imageBytes.Length == 0 || string.IsNullOrWhiteSpace(imageMime))
            return new PostingOutcome { Result = PostingResult.Failure("Flickr requires a photo. Add a book cover or brand logo before posting.") };

        var title = FlickrPostFormatter.BuildTitle(postText, media?.BookTitle, isBrand);
        var description = FlickrPostFormatter.BuildDescription(postText, baseUrl, isBrand);
        var tags = FlickrPostFormatter.BuildTags(isBrand, media?.Genre);
        var result = await _flickr.UploadPhotoAsync(
            tokens, imageBytes, imageMime, title, description, tags, cancellationToken);
        return new PostingOutcome { Result = result };
    }

    static string ResolveBaseUrl(string? appBaseUrl)
    {
        var baseUrl = appBaseUrl?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? "https://bookpromoterai.us" : baseUrl;
    }

    static string BuildMessagingFileName(string? seed, string? imageMime)
    {
        var baseName = string.IsNullOrWhiteSpace(seed)
            ? "image"
            : new string(seed.Trim().ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray())
                .Trim('-');
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "image";

        var ext = imageMime?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => ".jpg"
        };
        return baseName + ext;
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
    public string? ExternalPostId { get; init; }

    /// <summary>True only when content was sent to a real platform API (not simulated).</summary>
    public bool PostedToFeed => Success && !IsSimulated;

    public static PostingResult LiveOk(string message = "Posted successfully.", string? externalPostId = null) =>
        new() { Success = true, IsSimulated = false, Message = message, ExternalPostId = externalPostId };

    public static PostingResult SimulatedOk(string message) =>
        new() { Success = true, IsSimulated = true, Message = message };

    public static PostingResult Ok(string message = "Posted successfully.") => LiveOk(message);

    public static PostingResult Failure(string message) => new() { Success = false, Message = message };
}
