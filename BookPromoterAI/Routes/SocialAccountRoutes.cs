using Microsoft.Extensions.Caching.Distributed;

namespace BookPromoterAI;

static class SocialAccountRoutes
{
    public static void Map(WebApplication app)
    {
        // Legacy underscore paths (old bookmarks) → canonical hyphen routes.
        app.MapGet("/social_accounts/connect/{platform}", (HttpRequest request, string platform) =>
            Results.Redirect($"/social-accounts/connect/{platform}{request.QueryString}"));
        app.MapGet("/social_accounts/oauth_callback/facebook", (HttpRequest request) =>
            Results.Redirect($"/social-accounts/oauth-callback/facebook{request.QueryString}"));
        app.MapGet("/social_accounts/oauth_callback/instagram", (HttpRequest request) =>
            Results.Redirect($"/social-accounts/oauth-callback/instagram{request.QueryString}"));

        app.MapGet("/social-accounts", () => Results.Redirect("/my-account"));

        app.MapGet("/social-accounts/edit/{id:int}", (int id, HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var returnUrl = SocialConnectHelper.ResolveReturnUrl(request);
            var kind = SocialConnectHelper.ResolveAccountKind(returnUrl);
            if (SocialAccountKinds.IsBrand(kind) && !store.IsOwner) return Results.Redirect("/my-account");
            var account = store.FindSocialAccount(id, kind);
            if (account is null) return Results.Redirect(returnUrl);
            if (SocialAccountKinds.IsBrand(kind))
                return Results.Content(H.RenderPage(http, "Owner Social Accounts", OwnerSocialEditPage.Render(store, account, returnUrl), store), "text/html");
            return Results.Content(H.RenderPage(http, "My Account", MyAccountPage.Render(store, "", account), store), "text/html");
        });

        app.MapPost("/social-accounts", async (HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var returnUrl = SocialConnectHelper.ResolveReturnUrl(request, form["return"].ToString());
            var kind = SocialConnectHelper.ResolveAccountKind(returnUrl);
            if (SocialAccountKinds.IsBrand(kind) && !store.IsOwner) return Results.Redirect("/my-account");
            if (store.CheckSocialAccountLimit(kind) is not null) return Results.Redirect(returnUrl);
            var platform = form["platform"].ToString();
            var customPlatform = form["customPlatform"].ToString().Trim();
            var finalPlatform = platform == "__custom__" && !string.IsNullOrWhiteSpace(customPlatform) ? customPlatform : platform;
            if (SocialConnectHelper.IsPlatformDisabled(finalPlatform))
                return Results.Redirect(returnUrl);
            store.AddSocialAccount(new SocialAccount
            {
                Platform = finalPlatform,
                DisplayName = form["displayName"].ToString(),
                Handle = form["handle"].ToString(),
                IsConnected = true,
                AccountKind = kind
            }, kind);
            if (SocialAccountKinds.IsAuthor(kind))
                store.AddSchedule(new SocialSchedule { Platform = finalPlatform, PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect(returnUrl);
        });

        app.MapPost("/social-accounts/edit/{id:int}", async (int id, HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var returnUrl = SocialConnectHelper.ResolveReturnUrl(request, form["return"].ToString());
            var kind = SocialConnectHelper.ResolveAccountKind(returnUrl);
            if (SocialAccountKinds.IsBrand(kind) && !store.IsOwner) return Results.Redirect("/my-account");
            var account = store.FindSocialAccount(id, kind);
            if (account is null) return Results.Redirect(returnUrl);
            var platform = form["platform"].ToString();
            var customPlatform = form["customPlatform"].ToString().Trim();
            var finalPlatform = platform == "__custom__" && !string.IsNullOrWhiteSpace(customPlatform) ? customPlatform : platform;
            if (SocialConnectHelper.IsPlatformDisabled(finalPlatform))
                return Results.Redirect(returnUrl);
            account.Platform = finalPlatform;
            account.DisplayName = form["displayName"].ToString();
            account.Handle = form["handle"].ToString();
            store.UpdateSocialAccount(account, kind);
            if (SocialAccountKinds.IsAuthor(kind))
                store.AddSchedule(new SocialSchedule { Platform = finalPlatform, PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect(returnUrl);
        });

        app.MapPost("/social-accounts/delete/{id:int}", async (int id, HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var returnUrl = SocialConnectHelper.ResolveReturnUrl(request, form["return"].ToString());
            var kind = SocialConnectHelper.ResolveAccountKind(returnUrl);
            if (SocialAccountKinds.IsBrand(kind) && !store.IsOwner) return Results.Redirect("/my-account");
            store.RemoveSocialAccount(id, kind);
            return Results.Redirect(returnUrl);
        });

        app.MapGet("/social-accounts/connect/{platform}", async (
            string platform,
            HttpRequest request,
            HttpContext http,
            AppStoreDb store,
            AppSettings settings,
            XService xService,
            LinkedInService linkedInService,
            FacebookService facebookService,
            InstagramService instagramService,
            IDistributedCache cache) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var userId = store.GetCurrentDbUser()?.Id ?? 0;
            if (userId == 0) return Results.Redirect("/start");
            var returnUrl = SocialConnectHelper.ResolveReturnUrl(request);
            var kind = SocialConnectHelper.ResolveAccountKind(returnUrl);
            if (SocialAccountKinds.IsBrand(kind) && !store.IsOwner) return Results.Redirect("/my-account");
            if (store.CheckSocialAccountLimit(kind) is not null) return Results.Redirect(returnUrl);
            var saveUserId = SocialAccountKinds.IsBrand(kind) ? store.PrimaryOwnerUserId() : userId;
            if (saveUserId == 0) return Results.Redirect("/start");
            var platformName = Uri.UnescapeDataString(platform);
            if (SocialConnectHelper.IsPlatformDisabled(platformName))
                return Results.Redirect(returnUrl);

            if (PostLimits.IsX(platformName))
            {
                var notice = request.Query["notice"].ToString();
                if (!settings.IsXConfigured)
                {
                    return Results.Content(
                        H.RenderPage(http, "Connect X", SocialConnectHelper.XSetupPage(returnUrl, notice, settings), store),
                        "text/html");
                }

                var appBaseUrl = PublicUrl.Base(request, settings);
                var callbackUrl = XService.CallbackUrl(appBaseUrl);
                var (authorizeUrl, state, verifier) = xService.BuildAuthorizationUrl(callbackUrl);
                await XOAuthStateStore.SaveAsync(cache, state, new XOAuthPending
                {
                    UserId = saveUserId,
                    ReturnUrl = returnUrl,
                    Kind = kind,
                    CodeVerifier = verifier
                });
                return Results.Redirect(authorizeUrl);
            }

            if (PostLimits.IsLinkedIn(platformName))
            {
                var notice = request.Query["notice"].ToString();
                if (!settings.IsLinkedInConfigured)
                {
                    return Results.Content(
                        H.RenderPage(http, "Connect LinkedIn", SocialConnectHelper.LinkedInSetupPage(returnUrl, notice, settings), store),
                        "text/html");
                }

                var appBaseUrl = PublicUrl.Base(request, settings);
                var callbackUrl = LinkedInService.CallbackUrl(appBaseUrl);
                var (authorizeUrl, state) = linkedInService.BuildAuthorizationUrl(callbackUrl);
                await LinkedInOAuthStateStore.SaveAsync(cache, state, new LinkedInOAuthPending
                {
                    UserId = saveUserId,
                    ReturnUrl = returnUrl,
                    Kind = kind
                });
                return Results.Redirect(authorizeUrl);
            }

            if (PostLimits.IsFacebook(platformName))
            {
                var notice = request.Query["notice"].ToString();
                if (!settings.IsFacebookConfigured)
                {
                    return Results.Content(
                        H.RenderPage(http, "Connect Facebook", SocialConnectHelper.FacebookSetupPage(returnUrl, notice, settings, request), store),
                        "text/html");
                }

                if (!settings.IsFacebookOAuthReady)
                {
                    return Results.Content(
                        H.RenderPage(http, "Connect Facebook", SocialConnectHelper.FacebookSetupPage(returnUrl,
                            "Facebook Login Configuration ID is missing. Owner: add Facebook__LoginConfigId in Railway (see Owner → Facebook API).", settings, request), store),
                        "text/html");
                }

                var callbackUrl = PublicUrl.FacebookCallbackUrl(request, settings);
                var brandOAuth = SocialAccountKinds.IsBrand(kind);
                var (authorizeUrl, state) = facebookService.BuildAuthorizationUrl(callbackUrl, brandOAuth);
                await FacebookOAuthStateStore.SaveAsync(cache, state, new FacebookOAuthPending
                {
                    UserId = saveUserId,
                    ReturnUrl = returnUrl,
                    Kind = kind
                });
                return Results.Redirect(authorizeUrl);
            }

            if (PostLimits.IsInstagram(platformName))
            {
                var notice = request.Query["notice"].ToString();
                if (!settings.IsFacebookConfigured)
                {
                    return Results.Content(
                        H.RenderPage(http, "Connect Instagram", SocialConnectHelper.InstagramSetupPage(returnUrl, notice, settings, request), store),
                        "text/html");
                }

                if (!settings.IsFacebookOAuthReady)
                {
                    return Results.Content(
                        H.RenderPage(http, "Connect Instagram", SocialConnectHelper.InstagramSetupPage(returnUrl,
                            "Meta App ID and secret are missing. Owner: add Facebook__AppId and Facebook__AppSecret in Railway (see Owner → Facebook API).", settings, request), store),
                        "text/html");
                }

                var callbackUrl = PublicUrl.InstagramCallbackUrl(request, settings);
                var brandOAuth = SocialAccountKinds.IsBrand(kind);
                var (authorizeUrl, state) = instagramService.BuildAuthorizationUrl(callbackUrl, brandOAuth);
                await InstagramOAuthStateStore.SaveAsync(cache, state, new InstagramOAuthPending
                {
                    UserId = saveUserId,
                    ReturnUrl = returnUrl,
                    Kind = kind
                });
                return Results.Redirect(authorizeUrl);
            }

            var connectNotice = request.Query["notice"].ToString();
            return Results.Content(
                H.RenderPage(http, $"Connect {platformName}", SocialConnectHelper.OAuthAuthorizePage(platformName, returnUrl, connectNotice), store),
                "text/html");
        });

        app.MapGet(XService.CallbackPath, async (
            HttpRequest request,
            HttpContext http,
            AppStoreDb store,
            AppSettings settings,
            XService xService,
            IDistributedCache cache) =>
        {
            var error = request.Query["error"].ToString();
            var code = request.Query["code"].ToString();
            var state = request.Query["state"].ToString();
            var pending = await XOAuthStateStore.TakeAsync(cache, state);
            var returnUrl = XOAuthStateStore.BuildReturnUrl(
                pending?.ReturnUrl ?? "/my-account",
                pending?.Kind ?? SocialAccountKinds.Author);

            if (pending is null)
            {
                return Results.Redirect($"/social-accounts/connect/X?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("X login expired. Try connecting again.")}");
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                var notice = error.Equals("access_denied", StringComparison.OrdinalIgnoreCase)
                    ? "X authorization was cancelled."
                    : "X authorization failed. Try again.";
                return Results.Redirect($"/social-accounts/connect/X?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(notice)}");
            }

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(pending.CodeVerifier))
            {
                return Results.Redirect($"/social-accounts/connect/X?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Invalid X login response. Try again.")}");
            }

            if (SocialAccountKinds.IsBrand(pending.Kind) && !OwnerAccount.IsOwnerEmail(
                    store.GetUserEmailById(pending.UserId)))
            {
                return Results.Redirect($"/social-accounts/connect/X?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Only the owner can connect brand accounts.")}");
            }

            var callbackUrl = XService.CallbackUrl(PublicUrl.Base(request, settings));
            var (ok, connectError, tokens, user) = await xService.CompleteAuthorizationAsync(
                code, callbackUrl, pending.CodeVerifier);
            if (!ok || tokens is null || user is null)
            {
                return Results.Redirect($"/social-accounts/connect/X?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(connectError)}");
            }

            store.AddSocialAccountForUser(pending.UserId, new SocialAccount
            {
                Platform = "X",
                DisplayName = string.IsNullOrWhiteSpace(user.Name)
                    ? (SocialAccountKinds.IsBrand(pending.Kind) ? "BookPromoter AI" : "X Account")
                    : user.Name.Trim(),
                Handle = user.Username,
                IsConnected = true,
                ConnectedViaOAuth = true,
                AccountKind = pending.Kind,
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExternalAccountId = user.Id
            }, pending.Kind);
            if (SocialAccountKinds.IsAuthor(pending.Kind))
                store.AddScheduleForUser(pending.UserId, new SocialSchedule { Platform = "X", PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect(returnUrl);
        });

        app.MapGet(LinkedInService.CallbackPath, async (
            HttpRequest request,
            HttpContext http,
            AppStoreDb store,
            AppSettings settings,
            LinkedInService linkedInService,
            IDistributedCache cache) =>
        {
            var error = request.Query["error"].ToString();
            var code = request.Query["code"].ToString();
            var state = request.Query["state"].ToString();
            var pending = await LinkedInOAuthStateStore.TakeAsync(cache, state);
            var returnUrl = LinkedInOAuthStateStore.BuildReturnUrl(
                pending?.ReturnUrl ?? "/my-account",
                pending?.Kind ?? SocialAccountKinds.Author);

            if (pending is null)
            {
                return Results.Redirect($"/social-accounts/connect/LinkedIn?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("LinkedIn login expired. Try connecting again.")}");
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                var notice = error.Equals("user_cancelled_authorize", StringComparison.OrdinalIgnoreCase) ||
                             error.Equals("access_denied", StringComparison.OrdinalIgnoreCase)
                    ? "LinkedIn authorization was cancelled."
                    : "LinkedIn authorization failed. Try again.";
                return Results.Redirect($"/social-accounts/connect/LinkedIn?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(notice)}");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return Results.Redirect($"/social-accounts/connect/LinkedIn?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Invalid LinkedIn login response. Try again.")}");
            }

            if (SocialAccountKinds.IsBrand(pending.Kind) && !OwnerAccount.IsOwnerEmail(
                    store.GetUserEmailById(pending.UserId)))
            {
                return Results.Redirect($"/social-accounts/connect/LinkedIn?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Only the owner can connect brand accounts.")}");
            }

            var callbackUrl = LinkedInService.CallbackUrl(PublicUrl.Base(request, settings));
            var (ok, connectError, tokens, user) = await linkedInService.CompleteAuthorizationAsync(code, callbackUrl);
            if (!ok || tokens is null || user is null)
            {
                return Results.Redirect($"/social-accounts/connect/LinkedIn?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(connectError)}");
            }

            store.AddSocialAccountForUser(pending.UserId, new SocialAccount
            {
                Platform = "LinkedIn",
                DisplayName = string.IsNullOrWhiteSpace(user.Name)
                    ? (SocialAccountKinds.IsBrand(pending.Kind) ? "BookPromoter AI" : "LinkedIn Account")
                    : user.Name.Trim(),
                Handle = user.Handle,
                IsConnected = true,
                ConnectedViaOAuth = true,
                AccountKind = pending.Kind,
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExternalAccountId = user.Id
            }, pending.Kind);
            if (SocialAccountKinds.IsAuthor(pending.Kind))
                store.AddScheduleForUser(pending.UserId, new SocialSchedule { Platform = "LinkedIn", PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect(returnUrl);
        });

        app.MapGet(FacebookService.CallbackPath, async (
            HttpRequest request,
            HttpContext http,
            AppStoreDb store,
            AppSettings settings,
            FacebookService facebookService,
            IDistributedCache cache) =>
        {
            var error = request.Query["error"].ToString();
            var errorDescription = request.Query["error_description"].ToString();
            var code = request.Query["code"].ToString();
            var state = request.Query["state"].ToString();
            var pending = await FacebookOAuthStateStore.TakeAsync(cache, state);
            var returnUrl = FacebookOAuthStateStore.BuildReturnUrl(
                pending?.ReturnUrl ?? "/my-account",
                pending?.Kind ?? SocialAccountKinds.Author);

            if (pending is null)
            {
                return Results.Redirect($"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Facebook login expired. Try connecting again.")}");
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                var notice = error.Equals("access_denied", StringComparison.OrdinalIgnoreCase)
                    ? "Facebook authorization was cancelled."
                    : string.IsNullOrWhiteSpace(errorDescription)
                        ? "Facebook authorization failed. Try again."
                        : errorDescription;
                return Results.Redirect($"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(notice)}");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return Results.Redirect($"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Invalid Facebook login response. Try again.")}");
            }

            if (SocialAccountKinds.IsBrand(pending.Kind) && !OwnerAccount.IsOwnerEmail(
                    store.GetUserEmailById(pending.UserId)))
            {
                return Results.Redirect($"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Only the owner can connect brand accounts.")}");
            }

            var callbackUrl = PublicUrl.FacebookCallbackUrl(request, settings);
            var brandContext = SocialAccountKinds.IsBrand(pending.Kind);
            var outcome = await facebookService.CompleteAuthorizationAsync(
                code, callbackUrl, brandContext);
            if (outcome.Status == FacebookAuthStatus.NeedsPageSelection &&
                outcome.PagesToSelect is not null &&
                !string.IsNullOrWhiteSpace(outcome.UserAccessToken))
            {
                var pickToken = Guid.NewGuid().ToString("N");
                await FacebookPagePickStateStore.SaveAsync(cache, pickToken, new FacebookPagePickPending
                {
                    UserId = pending.UserId,
                    ReturnUrl = returnUrl,
                    Kind = pending.Kind,
                    UserAccessToken = outcome.UserAccessToken,
                    Pages = outcome.PagesToSelect.Select(p => new FacebookPageOption
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Handle = p.Handle,
                        AccessToken = p.AccessToken
                    }).ToList()
                });
                return Results.Redirect($"/social-accounts/connect/Facebook/select-page?token={Uri.EscapeDataString(pickToken)}");
            }

            if (outcome.Status != FacebookAuthStatus.Connected || outcome.Connection is null)
            {
                return Results.Redirect($"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(outcome.Error ?? "Facebook authorization failed. Try again.")}");
            }

            var connection = outcome.Connection;
            store.AddSocialAccountForUser(pending.UserId, new SocialAccount
            {
                Platform = "Facebook",
                DisplayName = connection.Page.Name,
                Handle = connection.Page.Handle,
                IsConnected = true,
                ConnectedViaOAuth = true,
                AccountKind = pending.Kind,
                AccessToken = connection.Page.AccessToken,
                RefreshToken = connection.UserAccessToken,
                ExternalAccountId = connection.Page.Id
            }, pending.Kind);
            if (SocialAccountKinds.IsAuthor(pending.Kind))
                store.AddScheduleForUser(pending.UserId, new SocialSchedule { Platform = "Facebook", PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect(returnUrl);
        });

        app.MapGet("/social-accounts/connect/Facebook/select-page", async (
            HttpRequest request,
            HttpContext http,
            AppStoreDb store,
            IDistributedCache cache) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var token = request.Query["token"].ToString();
            var notice = request.Query["notice"].ToString();
            var pending = await FacebookPagePickStateStore.PeekAsync(cache, token);
            if (pending is null)
            {
                return Results.Redirect($"/social-accounts/connect/Facebook?return=/my-account&notice={Uri.EscapeDataString("Page selection expired. Try connecting again.")}");
            }

            return Results.Content(
                H.RenderPage(http, "Choose Facebook Page", SocialConnectHelper.FacebookPagePickPage(pending, token, notice), store),
                "text/html");
        });

        app.MapPost("/social-accounts/connect/Facebook/select-page", async (
            HttpRequest request,
            AppStoreDb store,
            IDistributedCache cache) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var token = form["token"].ToString();
            var pageId = form["pageId"].ToString();
            var pending = await FacebookPagePickStateStore.PeekAsync(cache, token);
            var returnUrl = pending?.ReturnUrl ?? "/my-account";
            if (pending is null)
            {
                return Results.Redirect($"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Page selection expired. Try connecting again.")}");
            }

            var page = pending.Pages.FirstOrDefault(p => p.Id == pageId);
            if (page is null)
            {
                return Results.Redirect($"/social-accounts/connect/Facebook/select-page?token={Uri.EscapeDataString(token)}&notice={Uri.EscapeDataString("Choose a Facebook Page.")}");
            }

            await FacebookPagePickStateStore.TakeAsync(cache, token);
            store.AddSocialAccountForUser(pending.UserId, new SocialAccount
            {
                Platform = "Facebook",
                DisplayName = page.Name,
                Handle = page.Handle,
                IsConnected = true,
                ConnectedViaOAuth = true,
                AccountKind = pending.Kind,
                AccessToken = page.AccessToken,
                RefreshToken = pending.UserAccessToken,
                ExternalAccountId = page.Id
            }, pending.Kind);
            if (SocialAccountKinds.IsAuthor(pending.Kind))
                store.AddScheduleForUser(pending.UserId, new SocialSchedule { Platform = "Facebook", PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect(returnUrl);
        });

        app.MapGet(InstagramService.CallbackPath, async (
            HttpRequest request,
            HttpContext http,
            AppStoreDb store,
            AppSettings settings,
            FacebookService facebookService,
            InstagramService instagramService,
            IDistributedCache cache) =>
        {
            var error = request.Query["error"].ToString();
            var errorDescription = request.Query["error_description"].ToString();
            var code = request.Query["code"].ToString();
            var state = request.Query["state"].ToString();
            var pending = await InstagramOAuthStateStore.TakeAsync(cache, state);
            var returnUrl = InstagramOAuthStateStore.BuildReturnUrl(
                pending?.ReturnUrl ?? "/my-account",
                pending?.Kind ?? SocialAccountKinds.Author);

            if (pending is null)
            {
                return Results.Redirect($"/social-accounts/connect/Instagram?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Instagram login expired. Try connecting again.")}");
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                var notice = error.Equals("access_denied", StringComparison.OrdinalIgnoreCase)
                    ? "Instagram authorization was cancelled."
                    : string.IsNullOrWhiteSpace(errorDescription)
                        ? "Instagram authorization failed. Try again."
                        : errorDescription;
                return Results.Redirect($"/social-accounts/connect/Instagram?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(notice)}");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return Results.Redirect($"/social-accounts/connect/Instagram?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Invalid Instagram login response. Try again.")}");
            }

            if (SocialAccountKinds.IsBrand(pending.Kind) && !OwnerAccount.IsOwnerEmail(
                    store.GetUserEmailById(pending.UserId)))
            {
                return Results.Redirect($"/social-accounts/connect/Instagram?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Only the owner can connect brand accounts.")}");
            }

            var callbackUrl = PublicUrl.InstagramCallbackUrl(request, settings);
            var brandContext = SocialAccountKinds.IsBrand(pending.Kind);
            var (userToken, tokenError) = await facebookService.ObtainUserAccessTokenAsync(code, callbackUrl);
            if (userToken is null)
            {
                return Results.Redirect($"/social-accounts/connect/Instagram?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(tokenError ?? "Instagram authorization failed. Try again.")}");
            }

            var outcome = await instagramService.CompleteAuthorizationAsync(userToken, brandContext);
            if (outcome.Status == InstagramAuthStatus.NeedsAccountSelection &&
                outcome.LinksToSelect is not null &&
                !string.IsNullOrWhiteSpace(outcome.UserAccessToken))
            {
                var pickToken = Guid.NewGuid().ToString("N");
                await InstagramPagePickStateStore.SaveAsync(cache, pickToken, new InstagramPagePickPending
                {
                    UserId = pending.UserId,
                    ReturnUrl = returnUrl,
                    Kind = pending.Kind,
                    UserAccessToken = outcome.UserAccessToken,
                    Accounts = outcome.LinksToSelect.Select(l => new InstagramAccountOption
                    {
                        PageId = l.Page.Id,
                        PageName = l.Page.Name,
                        PageAccessToken = l.Page.AccessToken,
                        IgUserId = l.Instagram.Id,
                        IgUsername = l.Instagram.Username,
                        IgDisplayName = l.Instagram.Name ?? l.Instagram.Username
                    }).ToList()
                });
                return Results.Redirect($"/social-accounts/connect/Instagram/select-account?token={Uri.EscapeDataString(pickToken)}");
            }

            if (outcome.Status != InstagramAuthStatus.Connected || outcome.Connection is null)
            {
                return Results.Redirect($"/social-accounts/connect/Instagram?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(outcome.Error ?? "Instagram authorization failed. Try again.")}");
            }

            var connection = outcome.Connection;
            store.AddSocialAccountForUser(pending.UserId, new SocialAccount
            {
                Platform = "Instagram",
                DisplayName = connection.Link.Instagram.Name ?? connection.Link.Instagram.Username,
                Handle = connection.Link.Instagram.Username,
                IsConnected = true,
                ConnectedViaOAuth = true,
                AccountKind = pending.Kind,
                AccessToken = connection.Link.Page.AccessToken,
                RefreshToken = connection.UserAccessToken,
                ExternalAccountId = connection.Link.Instagram.Id
            }, pending.Kind);
            if (SocialAccountKinds.IsAuthor(pending.Kind))
                store.AddScheduleForUser(pending.UserId, new SocialSchedule { Platform = "Instagram", PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect(returnUrl);
        });

        app.MapGet("/social-accounts/connect/Instagram/select-account", async (
            HttpRequest request,
            HttpContext http,
            AppStoreDb store,
            IDistributedCache cache) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var token = request.Query["token"].ToString();
            var notice = request.Query["notice"].ToString();
            var pending = await InstagramPagePickStateStore.PeekAsync(cache, token);
            if (pending is null)
            {
                return Results.Redirect($"/social-accounts/connect/Instagram?return=/my-account&notice={Uri.EscapeDataString("Account selection expired. Try connecting again.")}");
            }

            return Results.Content(
                H.RenderPage(http, "Choose Instagram Account", SocialConnectHelper.InstagramPagePickPage(pending, token, notice), store),
                "text/html");
        });

        app.MapPost("/social-accounts/connect/Instagram/select-account", async (
            HttpRequest request,
            AppStoreDb store,
            IDistributedCache cache) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var token = form["token"].ToString();
            var igUserId = form["igUserId"].ToString();
            var pending = await InstagramPagePickStateStore.PeekAsync(cache, token);
            var returnUrl = pending?.ReturnUrl ?? "/my-account";
            if (pending is null)
            {
                return Results.Redirect($"/social-accounts/connect/Instagram?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Account selection expired. Try connecting again.")}");
            }

            var account = pending.Accounts.FirstOrDefault(a => a.IgUserId == igUserId);
            if (account is null)
            {
                return Results.Redirect($"/social-accounts/connect/Instagram/select-account?token={Uri.EscapeDataString(token)}&notice={Uri.EscapeDataString("Choose an Instagram account.")}");
            }

            await InstagramPagePickStateStore.TakeAsync(cache, token);
            store.AddSocialAccountForUser(pending.UserId, new SocialAccount
            {
                Platform = "Instagram",
                DisplayName = account.IgDisplayName,
                Handle = account.IgUsername,
                IsConnected = true,
                ConnectedViaOAuth = true,
                AccountKind = pending.Kind,
                AccessToken = account.PageAccessToken,
                RefreshToken = pending.UserAccessToken,
                ExternalAccountId = account.IgUserId
            }, pending.Kind);
            if (SocialAccountKinds.IsAuthor(pending.Kind))
                store.AddScheduleForUser(pending.UserId, new SocialSchedule { Platform = "Instagram", PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect(returnUrl);
        });

        app.MapPost("/social-accounts/oauth-callback/{platform}", async (string platform, HttpRequest request, HttpContext http, AppStoreDb store, BlueskyService bluesky) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var returnUrl = SocialConnectHelper.ResolveReturnUrl(request, form["return"].ToString());
            var kind = SocialConnectHelper.ResolveAccountKind(returnUrl);
            if (SocialAccountKinds.IsBrand(kind) && !store.IsOwner) return Results.Redirect("/my-account");
            if (store.CheckSocialAccountLimit(kind) is not null) return Results.Redirect(returnUrl);
            var platformName = Uri.UnescapeDataString(platform);
            if (SocialConnectHelper.IsPlatformDisabled(platformName))
                return Results.Redirect(returnUrl);

            if (PostLimits.IsBluesky(platformName))
            {
                var handle = form["handle"].ToString();
                var appPassword = form["appPassword"].ToString();
                var displayName = form["displayName"].ToString();
                var (ok, error, session) = await bluesky.CreateSessionAsync(handle, appPassword);
                if (!ok || session is null)
                {
                    var connectUrl = $"/social-accounts/connect/{Uri.EscapeDataString(platformName)}?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(error)}";
                    return Results.Redirect(connectUrl);
                }

                store.AddSocialAccount(new SocialAccount
                {
                    Platform = platformName,
                    DisplayName = string.IsNullOrWhiteSpace(displayName)
                        ? (SocialAccountKinds.IsBrand(kind) ? "BookPromoter AI" : "Bluesky Account")
                        : displayName.Trim(),
                    Handle = session.Handle,
                    IsConnected = true,
                    ConnectedViaOAuth = true,
                    AccountKind = kind,
                    AccessToken = session.AccessJwt,
                    RefreshToken = session.RefreshJwt,
                    ExternalAccountId = session.Did
                }, kind);
                if (SocialAccountKinds.IsAuthor(kind))
                    store.AddSchedule(new SocialSchedule { Platform = platformName, PostsPerWeek = 1, RequiresApproval = true });
                return Results.Redirect(returnUrl);
            }

            if (PostLimits.IsX(platformName))
            {
                var connectUrl = $"/social-accounts/connect/X?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Use the Connect X button to sign in with X.")}";
                return Results.Redirect(connectUrl);
            }

            if (PostLimits.IsLinkedIn(platformName))
            {
                var connectUrl = $"/social-accounts/connect/LinkedIn?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Use the Connect LinkedIn button to sign in with LinkedIn.")}";
                return Results.Redirect(connectUrl);
            }

            if (PostLimits.IsFacebook(platformName))
            {
                var connectUrl = $"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Use the Connect Facebook button to sign in with Facebook.")}";
                return Results.Redirect(connectUrl);
            }

            var simulatedToken = $"SIMULATED-{platformName.ToUpperInvariant()}-{Guid.NewGuid():N}";
            store.AddSocialAccount(new SocialAccount
            {
                Platform = platformName,
                DisplayName = string.IsNullOrWhiteSpace(form["displayName"].ToString())
                    ? (SocialAccountKinds.IsBrand(kind) ? "BookPromoter AI" : platformName + " Account")
                    : form["displayName"].ToString(),
                Handle = form["handle"].ToString(),
                IsConnected = true,
                ConnectedViaOAuth = true,
                AccountKind = kind,
                SimulatedAccessToken = simulatedToken
            }, kind);
            if (SocialAccountKinds.IsAuthor(kind))
                store.AddSchedule(new SocialSchedule { Platform = platformName, PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect(returnUrl);
        });
    }
}
