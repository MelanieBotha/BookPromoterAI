namespace BookPromoterAI;

static class MyAccountRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/my-account", (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            if (!store.HasCustomerAccess) return Results.Redirect("/start");
            var notice = request.Query["saved"] == "1"
                ? """<div class="notice success">Posting schedule saved. This week's posts have been generated — check the Ad Library.</div>"""
                : request.Query["community"] == "1"
                    ? """<div class="notice success">Reader community links saved. New posts will include your Discord, Telegram, and other invite links where helpful.</div>"""
                    : "";
            return Results.Content(H.RenderPage(http, "My Account", MyAccountPage.Render(store, notice), store), "text/html");
        });

        app.MapPost("/my-account/community", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var message = store.SaveAuthorCommunitySettings(new AuthorCommunitySettings
            {
                DiscordUrl = form["discordUrl"].ToString(),
                TelegramUrl = form["telegramUrl"].ToString(),
                BlogUrl = form["blogUrl"].ToString(),
                TikTokUrl = form["tiktokUrl"].ToString(),
                MastodonUrl = form["mastodonUrl"].ToString()
            });
            var cls = message.EndsWith('.') && !message.Contains("Not ") ? "success" : "error";
            var notice = $"""<div class="notice {cls}">{H.Encode(message)}</div>""";
            return Results.Content(H.RenderPage(http, "My Account", MyAccountPage.Render(store, notice), store), "text/html");
        });

        app.MapPost("/my-account/delete", (AppStoreDb store) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            store.DeleteAccount();
            return Results.Redirect("/start");
        });
    }
}
