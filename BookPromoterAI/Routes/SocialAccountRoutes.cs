namespace BookPromoterAI;

static class SocialAccountRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/social-accounts", () => Results.Redirect("/my-account"));

        app.MapGet("/social-accounts/edit/{id:int}", (int id, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var account = store.FindSocialAccount(id);
            if (account is null) return Results.Redirect("/my-account");
            return Results.Content(H.RenderPage(http, "My Account", MyAccountPage.Render(store, "", account), store), "text/html");
        });

        app.MapPost("/social-accounts", async (HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            if (store.CheckSocialAccountLimit() is not null) return Results.Redirect("/my-account");
            var form = await request.ReadFormAsync();
            var platform = form["platform"].ToString();
            var customPlatform = form["customPlatform"].ToString().Trim();
            var finalPlatform = platform == "__custom__" && !string.IsNullOrWhiteSpace(customPlatform) ? customPlatform : platform;
            store.AddSocialAccount(new SocialAccount { Platform = finalPlatform, DisplayName = form["displayName"].ToString(), Handle = form["handle"].ToString(), IsConnected = true });
            store.AddSchedule(new SocialSchedule { Platform = finalPlatform, PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect("/my-account");
        });

        app.MapPost("/social-accounts/edit/{id:int}", async (int id, HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var account = store.FindSocialAccount(id);
            if (account is null) return Results.Redirect("/my-account");
            var form = await request.ReadFormAsync();
            var platform = form["platform"].ToString();
            var customPlatform = form["customPlatform"].ToString().Trim();
            var finalPlatform = platform == "__custom__" && !string.IsNullOrWhiteSpace(customPlatform) ? customPlatform : platform;
            account.Platform = finalPlatform;
            account.DisplayName = form["displayName"].ToString();
            account.Handle = form["handle"].ToString();
            store.UpdateSocialAccount(account);
            store.AddSchedule(new SocialSchedule { Platform = finalPlatform, PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect("/my-account");
        });

        app.MapPost("/social-accounts/delete/{id:int}", (int id, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            store.RemoveSocialAccount(id);
            return Results.Redirect("/my-account");
        });

        app.MapGet("/social-accounts/connect/{platform}", (string platform, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            if (store.CheckSocialAccountLimit() is not null) return Results.Redirect("/my-account");
            var platformName = Uri.UnescapeDataString(platform);
            return Results.Content(H.RenderPage(http, $"Connect {platformName}", OAuthAuthorizePage(platformName), store), "text/html");
        });

        app.MapPost("/social-accounts/oauth-callback/{platform}", async (string platform, HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            if (store.CheckSocialAccountLimit() is not null) return Results.Redirect("/my-account");
            var platformName = Uri.UnescapeDataString(platform);
            var form = await request.ReadFormAsync();
            var simulatedToken = $"SIMULATED-{platformName.ToUpperInvariant()}-{Guid.NewGuid():N}";
            store.AddSocialAccount(new SocialAccount
            {
                Platform = platformName,
                DisplayName = string.IsNullOrWhiteSpace(form["displayName"].ToString()) ? platformName + " Account" : form["displayName"].ToString(),
                Handle = form["handle"].ToString(),
                IsConnected = true, ConnectedViaOAuth = true, SimulatedAccessToken = simulatedToken
            });
            store.AddSchedule(new SocialSchedule { Platform = platformName, PostsPerWeek = 1, RequiresApproval = true });
            return Results.Redirect("/my-account");
        });
    }

    static string OAuthAuthorizePage(string platformName)
    {
        var brands = new Dictionary<string, (string Color, string Initial)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Facebook"] = ("#1877F2", "f"), ["X"] = ("#000000", "X"), ["Instagram"] = ("#E4405F", "IG"),
            ["LinkedIn"] = ("#0A66C2", "in"), ["Pinterest"] = ("#E60023", "P"), ["TikTok"] = ("#000000", "T"),
        };
        var brand = brands.TryGetValue(platformName, out var b) ? b : ("#0f766e", platformName.Length > 0 ? platformName[0].ToString() : "?");
        return $"""
            <section class="hero"><div><p class="eyebrow">Connect Account</p><h1>Connect your {H.Encode(platformName)} account.</h1></div></section>
            <section class="panel oauth-panel">
                <div class="oauth-platform-badge" style="background:{brand.Item1}">{H.Encode(brand.Item2)}</div>
                <h2>Authorize BookPromoter AI</h2>
                <p class="muted">In a live deployment, this redirects you to {H.Encode(platformName)}'s login screen. Real API credentials are not yet configured — enter details below to simulate a connection.</p>
                <form method="post" action="/social-accounts/oauth-callback/{Uri.EscapeDataString(platformName)}" class="form">
                    <label>Display Name <input name="displayName" value="{H.Encode(platformName)} Account"></label>
                    <label>Handle <input name="handle" placeholder="yourauthorname" required></label>
                    <div class="form-actions">
                        <button class="button" type="submit" style="background:{brand.Item1}">Simulate &amp; Connect</button>
                        <a class="button secondary" href="/my-account">Cancel</a>
                    </div>
                </form>
            </section>
            """;
    }
}
