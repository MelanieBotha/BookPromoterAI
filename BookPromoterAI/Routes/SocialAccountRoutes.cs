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
            if (SocialConnectHelper.IsPlatformDisabled(finalPlatform, store.Settings))
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
            if (SocialConnectHelper.IsPlatformDisabled(finalPlatform, store.Settings))
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
            RedditService redditService,
            TikTokService tiktokService,
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
            if (SocialConnectHelper.IsPlatformDisabled(platformName, settings))
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
                var brandOAuth = SocialAccountKinds.IsBrand(kind);
                if (!settings.IsFacebookConfigured)
                {
                    return Results.Content(
                        H.RenderPage(http, "Connect Facebook", SocialConnectHelper.FacebookSetupPage(returnUrl, notice, settings, request), store),
                        "text/html");
                }

                if (brandOAuth)
                {
                    if (!settings.IsBrandFacebookOAuthReady)
                    {
                        return Results.Content(
                            H.RenderPage(http, "Connect Facebook", SocialConnectHelper.FacebookSetupPage(returnUrl,
                                settings.FacebookUsesConfigLogin
                                    ? "Brand Facebook connect requires Facebook__LoginConfigId in Railway. See Owner → Facebook API."
                                    : "Facebook API credentials are not configured.", settings, request), store),
                            "text/html");
                    }
                    return Results.Content(
                        H.RenderPage(http, "Connect Facebook", SocialConnectHelper.FacebookSetupPage(returnUrl, notice, settings, request), store),
                        "text/html");
                }

                if (!settings.IsFacebookOAuthReady)
                {
                    return Results.Content(
                        H.RenderPage(http, "Connect Facebook", SocialConnectHelper.FacebookSetupPage(returnUrl,
                            "Facebook is not ready yet. Try again later or contact support.", settings, request), store),
                        "text/html");
                }

                // Author: go straight to Meta unless we need to show a message from a failed attempt.
                if (!brandOAuth)
                {
                    if (string.IsNullOrWhiteSpace(notice))
                    {
                        var callbackUrl = PublicUrl.FacebookCallbackUrl(request, settings);
                        var (authorizeUrl, state, _) = facebookService.BuildAuthorizationUrl(callbackUrl, brandContext: false);
                        await FacebookOAuthStateStore.SaveAsync(cache, state, new FacebookOAuthPending
                        {
                            UserId = saveUserId,
                            ReturnUrl = returnUrl,
                            Kind = kind,
                            RedirectUri = callbackUrl
                        });
                        return Results.Redirect(authorizeUrl);
                    }

                    return Results.Content(
                        H.RenderPage(http, "Connect Facebook", SocialConnectHelper.FacebookSetupPage(returnUrl, notice, settings, request), store),
                        "text/html");
                }

                return Results.Content(
                    H.RenderPage(http, "Connect Facebook", SocialConnectHelper.FacebookSetupPage(returnUrl, notice, settings, request), store),
                    "text/html");
            }

            if (PostLimits.IsReddit(platformName))
            {
                var notice = request.Query["notice"].ToString();
                if (!settings.IsRedditConfigured)
                {
                    return Results.Content(
                        H.RenderPage(http, "Connect Reddit", SocialConnectHelper.RedditSetupPage(returnUrl, notice, settings, request), store),
                        "text/html");
                }

                return Results.Content(
                    H.RenderPage(http, "Connect Reddit", SocialConnectHelper.RedditSetupPage(returnUrl, notice, settings, request), store),
                    "text/html");
            }

            if (PostLimits.IsTikTok(platformName))
            {
                var notice = request.Query["notice"].ToString();
                if (!settings.IsTikTokConfigured)
                {
                    return Results.Content(
                        H.RenderPage(http, "Connect TikTok", SocialConnectHelper.TikTokSetupPage(returnUrl, notice, settings), store),
                        "text/html");
                }

                var appBaseUrl = PublicUrl.Base(request, settings);
                var callbackUrl = TikTokService.CallbackUrl(appBaseUrl);
                var (authorizeUrl, state, verifier) = tiktokService.BuildAuthorizationUrl(callbackUrl);
                await TikTokOAuthStateStore.SaveAsync(cache, state, new TikTokOAuthPending
                {
                    UserId = saveUserId,
                    ReturnUrl = returnUrl,
                    Kind = kind,
                    CodeVerifier = verifier
                });
                return Results.Redirect(authorizeUrl);
            }

            var connectNotice = request.Query["notice"].ToString();
            return Results.Content(
                H.RenderPage(http, $"Connect {platformName}", SocialConnectHelper.OAuthAuthorizePage(platformName, returnUrl, connectNotice, settings), store),
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

        app.MapPost("/social-accounts/connect/Facebook/start", async (
            HttpRequest request,
            AppStoreDb store,
            AppSettings settings,
            FacebookService facebookService,
            IDistributedCache cache) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var returnUrl = SocialConnectHelper.ResolveReturnUrl(request, form["return"].ToString());
            var kind = SocialConnectHelper.ResolveAccountKind(returnUrl);
            if (SocialAccountKinds.IsBrand(kind) && !store.IsOwner) return Results.Redirect("/my-account");
            if (store.CheckSocialAccountLimit(kind) is not null) return Results.Redirect(returnUrl);

            var userId = store.GetCurrentDbUser()?.Id ?? 0;
            if (userId == 0) return Results.Redirect("/start");
            var saveUserId = SocialAccountKinds.IsBrand(kind) ? store.PrimaryOwnerUserId() : userId;
            if (saveUserId == 0) return Results.Redirect("/start");

            if (!settings.IsFacebookConfigured)
                return Results.Redirect($"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Facebook API credentials are not configured.")}");

            var brandOAuth = SocialAccountKinds.IsBrand(kind);
            var mode = form["mode"].ToString();
            var forceScope = string.Equals(mode, "scope", StringComparison.OrdinalIgnoreCase);
            var forceConfig = string.Equals(mode, "config", StringComparison.OrdinalIgnoreCase);
            if (!brandOAuth && forceConfig)
            {
                return Results.Redirect($"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Login for Business is for Owner brand only. Authors use standard Page login.")}");
            }
            if (brandOAuth && !forceScope && (forceConfig || settings.FacebookUsesConfigLogin) && !settings.HasFacebookLoginConfigId)
            {
                return Results.Redirect($"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Add Facebook__LoginConfigId in Railway for Login for Business, or use standard Page login.")}");
            }

            if (!brandOAuth && !settings.IsFacebookOAuthReady)
            {
                return Results.Redirect($"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Facebook OAuth is not ready.")}");
            }

            var callbackUrl = PublicUrl.FacebookCallbackUrl(request, settings);
            var (authorizeUrl, state, _) = facebookService.BuildAuthorizationUrl(callbackUrl, brandOAuth, forceScope, forceConfig);
            await FacebookOAuthStateStore.SaveAsync(cache, state, new FacebookOAuthPending
            {
                UserId = saveUserId,
                ReturnUrl = returnUrl,
                Kind = kind,
                RedirectUri = callbackUrl
            });
            return Results.Redirect(authorizeUrl);
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

            store.RestoreLoginSessionForUserId(pending.UserId);

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
                var noCodeNotice = SocialAccountKinds.IsBrand(pending.Kind)
                    ? FacebookService.MetaBusinessIntegrationHelp
                    : "Invalid Facebook login response. Try again.";
                return Results.Redirect($"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(noCodeNotice)}");
            }

            if (SocialAccountKinds.IsBrand(pending.Kind) && !OwnerAccount.IsOwnerEmail(
                    store.GetUserEmailById(pending.UserId)))
            {
                return Results.Redirect($"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Only the owner can connect brand accounts.")}");
            }

            var callbackUrl = !string.IsNullOrWhiteSpace(pending.RedirectUri)
                ? pending.RedirectUri
                : PublicUrl.FacebookCallbackUrl(request, settings);
            var brandContext = SocialAccountKinds.IsBrand(pending.Kind);
            var outcome = await facebookService.CompleteAuthorizationAsync(
                code, callbackUrl, brandContext);
            if (outcome.Status == FacebookAuthStatus.Failed)
            {
                var legacyCallback = FacebookService.LegacyCallbackUrl(PublicUrl.Base(request, settings));
                if (!string.Equals(callbackUrl, legacyCallback, StringComparison.OrdinalIgnoreCase))
                {
                    outcome = await facebookService.CompleteAuthorizationAsync(
                        code, legacyCallback, brandContext);
                }
            }
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
            store.UpsertOAuthSocialAccountForUser(pending.UserId, new SocialAccount
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
            var token = request.Query["token"].ToString();
            var notice = request.Query["notice"].ToString();
            var pending = await FacebookPagePickStateStore.PeekAsync(cache, token);
            if (pending is null)
            {
                return Results.Redirect($"/social-accounts/connect/Facebook?return=/my-account&notice={Uri.EscapeDataString("Page selection expired. Try connecting again.")}");
            }

            store.RestoreLoginSessionForUserId(pending.UserId);
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");

            return Results.Content(
                H.RenderPage(http, "Choose Facebook Page", SocialConnectHelper.FacebookPagePickPage(pending, token, notice), store),
                "text/html");
        });

        app.MapPost("/social-accounts/connect/Facebook/select-page", async (
            HttpRequest request,
            AppStoreDb store,
            IDistributedCache cache) =>
        {
            var form = await request.ReadFormAsync();
            var token = form["token"].ToString();
            var pageId = form["pageId"].ToString();
            var pending = await FacebookPagePickStateStore.PeekAsync(cache, token);
            var returnUrl = pending?.ReturnUrl ?? "/my-account";
            if (pending is null)
            {
                return Results.Redirect($"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Page selection expired. Try connecting again.")}");
            }

            store.RestoreLoginSessionForUserId(pending.UserId);
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");

            var page = pending.Pages.FirstOrDefault(p => p.Id == pageId);
            if (page is null)
            {
                return Results.Redirect($"/social-accounts/connect/Facebook/select-page?token={Uri.EscapeDataString(token)}&notice={Uri.EscapeDataString("Choose a Facebook Page.")}");
            }

            await FacebookPagePickStateStore.TakeAsync(cache, token);
            store.UpsertOAuthSocialAccountForUser(pending.UserId, new SocialAccount
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

        app.MapPost("/social-accounts/connect/Reddit/start", async (
            HttpRequest request,
            AppStoreDb store,
            AppSettings settings,
            RedditService redditService,
            IDistributedCache cache) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var returnUrl = SocialConnectHelper.ResolveReturnUrl(request, form["return"].ToString());
            var kind = SocialConnectHelper.ResolveAccountKind(returnUrl);
            if (SocialAccountKinds.IsBrand(kind) && !store.IsOwner) return Results.Redirect("/my-account");
            if (store.CheckSocialAccountLimit(kind) is not null) return Results.Redirect(returnUrl);
            if (!settings.IsRedditConfigured)
                return Results.Redirect($"/social-accounts/connect/Reddit?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Reddit API credentials are not configured.")}");

            var subreddit = RedditService.NormalizeSubreddit(form["subreddit"].ToString());
            if (string.IsNullOrWhiteSpace(subreddit))
                return Results.Redirect($"/social-accounts/connect/Reddit?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Enter a subreddit name.")}");

            var userId = store.GetCurrentDbUser()?.Id ?? 0;
            if (userId == 0) return Results.Redirect("/start");
            var saveUserId = SocialAccountKinds.IsBrand(kind) ? store.PrimaryOwnerUserId() : userId;
            if (saveUserId == 0) return Results.Redirect("/start");

            var callbackUrl = RedditService.CallbackUrl(PublicUrl.Base(request, settings));
            var (authorizeUrl, state) = redditService.BuildAuthorizationUrl(callbackUrl);
            await RedditOAuthStateStore.SaveAsync(cache, state, new RedditOAuthPending
            {
                UserId = saveUserId,
                ReturnUrl = returnUrl,
                Kind = kind,
                Subreddit = subreddit
            });
            return Results.Redirect(authorizeUrl);
        });

        app.MapGet(RedditService.CallbackPath, async (
            HttpRequest request,
            AppStoreDb store,
            AppSettings settings,
            RedditService redditService,
            IDistributedCache cache) =>
        {
            var error = request.Query["error"].ToString();
            var code = request.Query["code"].ToString();
            var state = request.Query["state"].ToString();
            var pending = await RedditOAuthStateStore.TakeAsync(cache, state);
            var returnUrl = RedditOAuthStateStore.BuildReturnUrl(
                pending?.ReturnUrl ?? "/my-account",
                pending?.Kind ?? SocialAccountKinds.Author);

            if (pending is null)
            {
                return Results.Redirect($"/social-accounts/connect/Reddit?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Reddit login expired. Try connecting again.")}");
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                var notice = error.Equals("access_denied", StringComparison.OrdinalIgnoreCase)
                    ? "Reddit authorization was cancelled."
                    : "Reddit authorization failed. Try again.";
                return Results.Redirect($"/social-accounts/connect/Reddit?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(notice)}");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return Results.Redirect($"/social-accounts/connect/Reddit?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Invalid Reddit login response. Try again.")}");
            }

            if (SocialAccountKinds.IsBrand(pending.Kind) && !OwnerAccount.IsOwnerEmail(
                    store.GetUserEmailById(pending.UserId)))
            {
                return Results.Redirect($"/social-accounts/connect/Reddit?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Only the owner can connect brand accounts.")}");
            }

            var callbackUrl = RedditService.CallbackUrl(PublicUrl.Base(request, settings));
            var (ok, connectError, tokens, user) = await redditService.CompleteAuthorizationAsync(code, callbackUrl);
            if (!ok || tokens is null || user is null)
            {
                return Results.Redirect($"/social-accounts/connect/Reddit?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(connectError)}");
            }

            var subreddit = RedditService.NormalizeSubreddit(pending.Subreddit);
            store.AddSocialAccountForUser(pending.UserId, new SocialAccount
            {
                Platform = "Reddit",
                DisplayName = $"r/{subreddit}",
                Handle = subreddit,
                IsConnected = true,
                ConnectedViaOAuth = true,
                AccountKind = pending.Kind,
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExternalAccountId = user.Id
            }, pending.Kind);
            if (SocialAccountKinds.IsAuthor(pending.Kind))
                store.AddScheduleForUser(pending.UserId, new SocialSchedule { Platform = "Reddit", PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect(returnUrl);
        });

        app.MapPost("/social-accounts/connect/TikTok/start", async (
            HttpRequest request,
            AppStoreDb store,
            AppSettings settings,
            TikTokService tiktokService,
            IDistributedCache cache) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var returnUrl = SocialConnectHelper.ResolveReturnUrl(request, form["return"].ToString());
            if (store.CheckSocialAccountLimit(SocialAccountKinds.Author) is not null) return Results.Redirect(returnUrl);
            if (!settings.IsTikTokConfigured)
                return Results.Redirect($"/social-accounts/connect/TikTok?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("TikTok API credentials are not configured.")}");

            var userId = store.GetCurrentDbUser()?.Id ?? 0;
            if (userId == 0) return Results.Redirect("/start");

            var appBaseUrl = PublicUrl.Base(request, settings);
            var callbackUrl = TikTokService.CallbackUrl(appBaseUrl);
            var (authorizeUrl, state, verifier) = tiktokService.BuildAuthorizationUrl(callbackUrl);
            await TikTokOAuthStateStore.SaveAsync(cache, state, new TikTokOAuthPending
            {
                UserId = userId,
                ReturnUrl = returnUrl,
                Kind = SocialAccountKinds.Author,
                CodeVerifier = verifier
            });
            return Results.Redirect(authorizeUrl);
        });

        app.MapGet(TikTokService.CallbackPath, async (
            HttpRequest request,
            HttpContext http,
            AppStoreDb store,
            AppSettings settings,
            TikTokService tiktokService,
            IDistributedCache cache) =>
        {
            var error = request.Query["error"].ToString();
            var code = request.Query["code"].ToString();
            var state = request.Query["state"].ToString();
            var pending = await TikTokOAuthStateStore.TakeAsync(cache, state);
            var returnUrl = pending?.ReturnUrl ?? SocialConnectHelper.VideosReturnPath;

            if (pending is null)
            {
                return Results.Redirect($"/social-accounts/connect/TikTok?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("TikTok login expired. Try connecting again.")}");
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                var notice = error.Equals("access_denied", StringComparison.OrdinalIgnoreCase)
                    ? "TikTok authorization was cancelled."
                    : "TikTok authorization failed. Try again.";
                return Results.Redirect($"/social-accounts/connect/TikTok?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(notice)}");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return Results.Redirect($"/social-accounts/connect/TikTok?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Invalid TikTok login response. Try again.")}");
            }

            store.RestoreLoginSessionForUserId(pending.UserId);
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");

            var callbackUrl = TikTokService.CallbackUrl(PublicUrl.Base(request, settings));
            var (ok, connectError, tokens, user) = await tiktokService.CompleteAuthorizationAsync(
                code, callbackUrl, pending.CodeVerifier);
            if (!ok || tokens is null || user is null)
            {
                return Results.Redirect($"/social-accounts/connect/TikTok?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(connectError)}");
            }

            store.AddSocialAccountForUser(pending.UserId, new SocialAccount
            {
                Platform = "TikTok",
                DisplayName = user.DisplayName,
                Handle = user.Username,
                IsConnected = true,
                ConnectedViaOAuth = true,
                AccountKind = SocialAccountKinds.Author,
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExternalAccountId = user.OpenId
            }, SocialAccountKinds.Author);

            var successUrl = returnUrl.Contains('?') ? $"{returnUrl}&connected=1" : $"{returnUrl}?connected=1";
            return Results.Redirect(successUrl);
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
            if (SocialConnectHelper.IsPlatformDisabled(platformName, store.Settings))
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

            if (PostLimits.IsReddit(platformName))
            {
                var connectUrl = $"/social-accounts/connect/Reddit?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString("Use the Connect Reddit button to sign in with Reddit.")}";
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
