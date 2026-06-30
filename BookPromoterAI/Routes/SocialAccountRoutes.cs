using Microsoft.Extensions.Caching.Distributed;

namespace BookPromoterAI;

static class SocialAccountRoutes
{
    public static void Map(WebApplication app)
    {
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
            IDistributedCache cache) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var userId = store.GetCurrentDbUser()?.Id ?? 0;
            if (userId == 0) return Results.Redirect("/start");
            var returnUrl = SocialConnectHelper.ResolveReturnUrl(request);
            var kind = SocialConnectHelper.ResolveAccountKind(returnUrl);
            if (SocialAccountKinds.IsBrand(kind) && !store.IsOwner) return Results.Redirect("/my-account");
            if (store.CheckSocialAccountLimit(kind) is not null) return Results.Redirect(returnUrl);
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
                    UserId = userId,
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
                    UserId = userId,
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
                        H.RenderPage(http, "Connect Facebook", SocialConnectHelper.FacebookSetupPage(returnUrl, notice, settings), store),
                        "text/html");
                }

                var appBaseUrl = PublicUrl.Base(request, settings);
                var callbackUrl = FacebookService.CallbackUrl(appBaseUrl);
                var (authorizeUrl, state) = facebookService.BuildAuthorizationUrl(callbackUrl);
                await FacebookOAuthStateStore.SaveAsync(cache, state, new FacebookOAuthPending
                {
                    UserId = userId,
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

            var callbackUrl = FacebookService.CallbackUrl(PublicUrl.Base(request, settings));
            var brandContext = SocialAccountKinds.IsBrand(pending.Kind);
            var (ok, connectError, connection) = await facebookService.CompleteAuthorizationAsync(
                code, callbackUrl, brandContext);
            if (!ok || connection is null)
            {
                return Results.Redirect($"/social-accounts/connect/Facebook?return={Uri.EscapeDataString(returnUrl)}&notice={Uri.EscapeDataString(connectError)}");
            }

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
