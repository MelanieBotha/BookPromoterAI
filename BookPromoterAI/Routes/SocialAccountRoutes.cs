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

        app.MapGet("/social-accounts/connect/{platform}", (string platform, HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var returnUrl = SocialConnectHelper.ResolveReturnUrl(request);
            var kind = SocialConnectHelper.ResolveAccountKind(returnUrl);
            if (SocialAccountKinds.IsBrand(kind) && !store.IsOwner) return Results.Redirect("/my-account");
            if (store.CheckSocialAccountLimit(kind) is not null) return Results.Redirect(returnUrl);
            var platformName = Uri.UnescapeDataString(platform);
            if (SocialConnectHelper.IsPlatformDisabled(platformName))
                return Results.Redirect(returnUrl);
            var notice = request.Query["notice"].ToString();
            return Results.Content(
                H.RenderPage(http, $"Connect {platformName}", SocialConnectHelper.OAuthAuthorizePage(platformName, returnUrl, notice), store),
                "text/html");
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
