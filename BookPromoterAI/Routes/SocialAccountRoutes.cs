namespace BookPromoterAI;

static class SocialAccountRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/social-accounts", () => Results.Redirect("/my-account"));

        app.MapGet("/social-accounts/edit/{id:int}", (int id, HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var account = store.FindSocialAccount(id);
            if (account is null) return Results.Redirect(SocialConnectHelper.ResolveReturnUrl(request));
            return Results.Content(H.RenderPage(http, "My Account", MyAccountPage.Render(store, "", account), store), "text/html");
        });

        app.MapPost("/social-accounts", async (HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            if (store.CheckSocialAccountLimit() is not null) return Results.Redirect(SocialConnectHelper.ResolveReturnUrl(request));
            var form = await request.ReadFormAsync();
            var platform = form["platform"].ToString();
            var customPlatform = form["customPlatform"].ToString().Trim();
            var finalPlatform = platform == "__custom__" && !string.IsNullOrWhiteSpace(customPlatform) ? customPlatform : platform;
            if (SocialConnectHelper.IsPlatformDisabled(finalPlatform))
                return Results.Redirect(SocialConnectHelper.ResolveReturnUrl(request, form["return"].ToString()));
            store.AddSocialAccount(new SocialAccount { Platform = finalPlatform, DisplayName = form["displayName"].ToString(), Handle = form["handle"].ToString(), IsConnected = true });
            store.AddSchedule(new SocialSchedule { Platform = finalPlatform, PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect(SocialConnectHelper.ResolveReturnUrl(request, form["return"].ToString()));
        });

        app.MapPost("/social-accounts/edit/{id:int}", async (int id, HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var account = store.FindSocialAccount(id);
            if (account is null) return Results.Redirect(SocialConnectHelper.ResolveReturnUrl(request));
            var form = await request.ReadFormAsync();
            var platform = form["platform"].ToString();
            var customPlatform = form["customPlatform"].ToString().Trim();
            var finalPlatform = platform == "__custom__" && !string.IsNullOrWhiteSpace(customPlatform) ? customPlatform : platform;
            if (SocialConnectHelper.IsPlatformDisabled(finalPlatform))
                return Results.Redirect(SocialConnectHelper.ResolveReturnUrl(request, form["return"].ToString()));
            account.Platform = finalPlatform;
            account.DisplayName = form["displayName"].ToString();
            account.Handle = form["handle"].ToString();
            store.UpdateSocialAccount(account);
            store.AddSchedule(new SocialSchedule { Platform = finalPlatform, PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect(SocialConnectHelper.ResolveReturnUrl(request, form["return"].ToString()));
        });

        app.MapPost("/social-accounts/delete/{id:int}", async (int id, HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            store.RemoveSocialAccount(id);
            return Results.Redirect(SocialConnectHelper.ResolveReturnUrl(request, form["return"].ToString()));
        });

        app.MapGet("/social-accounts/connect/{platform}", (string platform, HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            if (store.CheckSocialAccountLimit() is not null) return Results.Redirect(SocialConnectHelper.ResolveReturnUrl(request));
            var platformName = Uri.UnescapeDataString(platform);
            if (SocialConnectHelper.IsPlatformDisabled(platformName))
                return Results.Redirect(SocialConnectHelper.ResolveReturnUrl(request));
            var returnUrl = SocialConnectHelper.ResolveReturnUrl(request);
            return Results.Content(
                H.RenderPage(http, $"Connect {platformName}", SocialConnectHelper.OAuthAuthorizePage(platformName, returnUrl), store),
                "text/html");
        });

        app.MapPost("/social-accounts/oauth-callback/{platform}", async (string platform, HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            if (store.CheckSocialAccountLimit() is not null) return Results.Redirect(SocialConnectHelper.ResolveReturnUrl(request));
            var platformName = Uri.UnescapeDataString(platform);
            var form = await request.ReadFormAsync();
            if (SocialConnectHelper.IsPlatformDisabled(platformName))
                return Results.Redirect(SocialConnectHelper.ResolveReturnUrl(request, form["return"].ToString()));
            var simulatedToken = $"SIMULATED-{platformName.ToUpperInvariant()}-{Guid.NewGuid():N}";
            store.AddSocialAccount(new SocialAccount
            {
                Platform = platformName,
                DisplayName = string.IsNullOrWhiteSpace(form["displayName"].ToString()) ? platformName + " Account" : form["displayName"].ToString(),
                Handle = form["handle"].ToString(),
                IsConnected = true, ConnectedViaOAuth = true, SimulatedAccessToken = simulatedToken
            });
            store.AddSchedule(new SocialSchedule { Platform = platformName, PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect(SocialConnectHelper.ResolveReturnUrl(request, form["return"].ToString()));
        });
    }
}
