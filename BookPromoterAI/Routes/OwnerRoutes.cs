namespace BookPromoterAI;

static class OwnerRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/owner-promos", (HttpContext http, AppStoreDb store) =>
        {
            var guard = OwnerGuard(store);
            if (guard is not null) return guard;
            return Results.Content(H.RenderPage(http, "Owner", OwnerPage.Render(store), store), "text/html");
        });

        app.MapPost("/owner/generate-access-code", (AppStoreDb store) =>
        {
            var guard = OwnerGuard(store);
            if (guard is not null) return guard;
            store.GenerateAccessCode();
            return Results.Redirect("/owner-promos");
        });

        app.MapPost("/owner/generate-lifetime-code", (AppStoreDb store) =>
        {
            var guard = OwnerGuard(store);
            if (guard is not null) return guard;
            store.GenerateLifetimeCode();
            return Results.Redirect("/owner-promos");
        });

        app.MapPost("/owner/plan-price", async (HttpRequest request, AppStoreDb store) =>
        {
            var guard = OwnerGuard(store);
            if (guard is not null) return guard;
            var form = await request.ReadFormAsync();
            if (decimal.TryParse(form["monthlyFee"].ToString(), out var fee))
                store.UpdatePlanPrice(form["planId"].ToString(), fee);
            return Results.Redirect("/owner-promos");
        });

        app.MapPost("/owner/plan-payment-ids", (HttpRequest request, AppStoreDb store) =>
        {
            var guard = OwnerGuard(store);
            if (guard is not null) return guard;
            var form = request.Form;
            store.UpdatePlanPaymentIds(form["planId"].ToString(), form["stripePriceId"].ToString());
            return Results.Redirect("/owner-promos");
        });

        app.MapPost("/owner/payout-settings", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            var guard = OwnerGuard(store);
            if (guard is not null) return guard;
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
            var guard = OwnerGuard(store);
            if (guard is not null) return guard;
            store.ToggleFeedbackInvestigated(id);
            return Results.Redirect("/owner-promos");
        });

        // Legacy URL — send to owner page (login required).
        app.MapGet("/owner-login", (AppStoreDb store) => OwnerGuard(store) ?? Results.Redirect("/owner-promos"));
        app.MapPost("/owner-login", (AppStoreDb store) => OwnerGuard(store) ?? Results.Redirect("/owner-promos"));
    }

    static IResult? OwnerGuard(AppStoreDb store)
    {
        if (!store.IsLoggedIn) return Results.Redirect("/start");
        if (!store.IsOwner) return Results.Redirect("/dashboard");
        return null;
    }
}
