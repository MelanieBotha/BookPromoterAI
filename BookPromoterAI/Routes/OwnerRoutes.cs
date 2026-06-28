namespace BookPromoterAI;

static class OwnerRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/owner-promos", (HttpContext http, AppStoreDb store, AppSettings settings, ReleaseNotesCatalog releaseNotes) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            var section = http.Request.Query["section"].ToString();
            return RenderOwner(http, store, settings, releaseNotes, activeSection: section);
        });

        app.MapGet("/owner/promos", () => Results.Redirect("/owner-promos"));
        app.MapGet("/owner_promos", () => Results.Redirect("/owner-promos"));

        app.MapPost("/owner/generate-access-code", (AppStoreDb store) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            return Results.Redirect("/owner-promos");
        });

        app.MapPost("/owner/generate-lifetime-code", (HttpContext http, AppStoreDb store, AppSettings settings, ReleaseNotesCatalog releaseNotes) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            var code = store.GenerateLifetimeCode();
            var notice = $"""
                <div class="notice success">
                    New lifetime code: <strong>{H.Encode(code.Code)}</strong> — copy and send to your beta author.
                    They enter it on Billing to unlock lifetime Publisher access.
                </div>
                """;
            return RenderOwner(http, store, settings, releaseNotes, notice, "lifetime");
        });

        app.MapPost("/owner/plan-price", async (HttpRequest request, AppStoreDb store) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            var form = await request.ReadFormAsync();
            if (decimal.TryParse(form["monthlyFee"].ToString(), out var fee))
                store.UpdatePlanPrice(form["planId"].ToString(), fee);
            return Results.Redirect("/owner-promos");
        });

        app.MapPost("/owner/plan-payment-ids", (HttpRequest request, AppStoreDb store) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            var form = request.Form;
            store.UpdatePlanPaymentIds(form["planId"].ToString(), form["stripePriceId"].ToString());
            return Results.Redirect("/owner-promos");
        });

        app.MapPost("/owner/payout-settings", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings, ReleaseNotesCatalog releaseNotes) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
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
            return RenderOwner(http, store, settings, releaseNotes, notice);
        });

        app.MapPost("/owner/app-promo/email", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings, ReleaseNotesCatalog releaseNotes) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            var form = await request.ReadFormAsync();
            var baseUrl = PublicUrl.Base(http.Request, settings);
            var (_, _, message) = await store.BroadcastAppEmailAsync(
                form["subject"].ToString(),
                form["body"].ToString(),
                settings.SendGridApiKey,
                settings.SendGridSenderEmail,
                settings.SendGridSenderName,
                baseUrl);
            var cls = message.Contains("sent to", StringComparison.OrdinalIgnoreCase) ? "success" : "error";
            return RenderOwner(http, store, settings, releaseNotes, $"""<div class="notice {cls}">{H.Encode(message)}</div>""");
        });

        app.MapPost("/owner/app-promo/post-social", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings, SocialPostingService posting, ReleaseNotesCatalog releaseNotes) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            var form = await request.ReadFormAsync();
            var platform = form["platform"].ToString();
            var baseUrl = PublicUrl.Base(http.Request, settings);
            var (_, _, message) = await store.PostOwnerAppPromoAsync(
                posting,
                baseUrl,
                string.IsNullOrWhiteSpace(platform) ? null : platform);
            var cls = message.Contains("Posted", StringComparison.OrdinalIgnoreCase) ? "success" : "error";
            return RenderOwner(http, store, settings, releaseNotes, $"""<div class="notice {cls}">{H.Encode(message)}</div>""");
        });

        app.MapPost("/owner/product-update/publish", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings, SocialPostingService posting, ReleaseNotesCatalog releaseNotes) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            var form = await request.ReadFormAsync();
            var baseUrl = PublicUrl.Base(http.Request, settings);
            var (success, message, _) = await store.PublishProductUpdateAsync(
                form["version"].ToString(),
                form["title"].ToString(),
                form["updatedItems"].ToString(),
                form["createdItems"].ToString(),
                form["addedItems"].ToString(),
                form["sendEmail"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase),
                form["postToSocial"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase),
                baseUrl,
                posting,
                settings.SendGridApiKey,
                settings.SendGridSenderEmail,
                settings.SendGridSenderName);
            var cls = success ? "success" : "error";
            return RenderOwner(http, store, settings, releaseNotes, $"""<div class="notice {cls}">{H.Encode(message)}</div>""");
        });

        app.MapPost("/owner/feedback/investigate/{id:int}", (int id, AppStoreDb store) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            store.ToggleFeedbackInvestigated(id);
            return Results.Redirect("/owner-promos");
        });

        app.MapGet("/owner-login", (AppStoreDb store) => OwnerGuard(store) ?? Results.Redirect("/owner-promos"));
        app.MapPost("/owner-login", (AppStoreDb store) => OwnerGuard(store) ?? Results.Redirect("/owner-promos"));
    }

    static IResult RenderOwner(HttpContext http, AppStoreDb store, AppSettings settings, ReleaseNotesCatalog releaseNotes, string notice = "", string? activeSection = null)
    {
        try
        {
            return Results.Content(
                H.RenderPage(http, "Owner", OwnerPage.Render(store, notice, PublicUrl.Base(http.Request, settings), releaseNotes, activeSection), store),
                "text/html");
        }
        catch (Exception ex)
        {
            return Results.Content(
                H.RenderPage(http, "Owner", $"""
                    <section class="panel">
                        <h1>Owner</h1>
                        <p class="notice error">Could not load owner page: {H.Encode(ex.Message)}</p>
                        <p class="muted">Try logging out and back in, or use <a href="/owner-promos">/owner-promos</a> (with a hyphen).</p>
                    </section>
                    """, store),
                "text/html",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    static IResult? OwnerGuard(AppStoreDb store)
    {
        if (!store.IsLoggedIn) return Results.Redirect("/start");
        if (!store.IsOwner) return Results.Redirect("/dashboard");
        return null;
    }
}
