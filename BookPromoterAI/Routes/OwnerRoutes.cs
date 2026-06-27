namespace BookPromoterAI;

static class OwnerRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/owner-login", (HttpContext http, AppStoreDb store) =>
            Results.Content(H.RenderPage(http, "Owner Login", AuthPages.OwnerLogin(""), store), "text/html"));

        app.MapPost("/owner-login", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            var form = await request.ReadFormAsync();
            var unlocked = store.UnlockOwner(form["pin"].ToString());
            if (unlocked) return Results.Redirect("/owner-promos");
            return Results.Content(H.RenderPage(http, "Owner Login", AuthPages.OwnerLogin("""<div class="notice error">Wrong owner PIN.</div>"""), store), "text/html");
        });

        app.MapGet("/owner-promos", (HttpContext http, AppStoreDb store) =>
        {
            if (!store.OwnerUnlocked) return Results.Redirect("/owner-login");
            return Results.Content(H.RenderPage(http, "Owner", OwnerPage.Render(store), store), "text/html");
        });

        app.MapPost("/owner/generate-access-code", (AppStoreDb store) =>
        {
            if (!store.OwnerUnlocked) return Results.Redirect("/owner-login");
            store.GenerateAccessCode();
            return Results.Redirect("/owner-promos");
        });

        app.MapPost("/owner/generate-lifetime-code", (AppStoreDb store) =>
        {
            if (!store.OwnerUnlocked) return Results.Redirect("/owner-login");
            store.GenerateLifetimeCode();
            return Results.Redirect("/owner-promos");
        });

        app.MapPost("/owner/plan-price", async (HttpRequest request, AppStoreDb store) =>
        {
            if (!store.OwnerUnlocked) return Results.Redirect("/owner-login");
            var form = await request.ReadFormAsync();
            if (decimal.TryParse(form["monthlyFee"].ToString(), out var fee))
                store.UpdatePlanPrice(form["planId"].ToString(), fee);
            return Results.Redirect("/owner-promos");
        });

        app.MapPost("/owner/payout-settings", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.OwnerUnlocked) return Results.Redirect("/owner-login");
            var form = await request.ReadFormAsync();
            var message = store.SaveOwnerPayoutSettings(new OwnerPayoutSettings
            {
                AccountHolderName = form["accountHolderName"].ToString(),
                BankName = form["bankName"].ToString(),
                AccountType = form["accountType"].ToString(),
                RoutingOrSortCode = form["routingOrSortCode"].ToString(),
                AccountNumber = form["accountNumber"].ToString(),
                Iban = form["iban"].ToString(),
                Notes = form["notes"].ToString()
            });
            var cls = message.EndsWith('.') && !message.Contains("Enter") ? "success" : "error";
            var notice = $"""<div class="notice {cls}">{H.Encode(message)}</div>""";
            return Results.Content(H.RenderPage(http, "Owner", OwnerPage.Render(store, notice), store), "text/html");
        });

        app.MapPost("/owner/feedback/investigate/{id:int}", (int id, AppStoreDb store) =>
        {
            if (!store.OwnerUnlocked) return Results.Redirect("/owner-login");
            store.ToggleFeedbackInvestigated(id);
            return Results.Redirect("/owner-promos");
        });
    }
}
