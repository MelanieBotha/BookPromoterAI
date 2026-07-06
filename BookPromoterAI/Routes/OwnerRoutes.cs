namespace BookPromoterAI;

static class OwnerRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/owner-promos", (HttpContext http, AppStoreDb store, AppSettings settings, ReleaseNotesCatalog releaseNotes) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            var baseUrl = PublicUrl.Base(http.Request, settings);
            store.EnsureWeeklyOwnerBrandMailingDraft(baseUrl);
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

        app.MapPost("/owner/promo-code/delete/{id:int}", async (int id, HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings, ReleaseNotesCatalog releaseNotes) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            var form = await request.ReadFormAsync();
            var section = form["section"].ToString();
            var (success, message) = store.DeletePromoCode(id);
            var cls = success ? "success" : "error";
            var notice = $"""<div class="notice {cls}">{H.Encode(message)}</div>""";
            return RenderOwner(http, store, settings, releaseNotes, notice, string.IsNullOrWhiteSpace(section) ? null : section);
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
            return RenderOwner(http, store, settings, releaseNotes, $"""<div class="notice {cls}">{H.Encode(message)}</div>""", "promote-app");
        });

        app.MapPost("/owner/brand-email/schedule", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings, ReleaseNotesCatalog releaseNotes) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            var form = await request.ReadFormAsync();
            var emailsPerWeek = int.TryParse(form["emailsPerWeek"].ToString(), out var n) ? n : 0;
            var autoSend = form.ContainsKey("autoSendEnabled");
            var requiresApproval = form.ContainsKey("requiresApproval");
            store.SaveMailingListSettings(emailsPerWeek, autoSend, requiresApproval, MailingListKinds.Brand);
            var baseUrl = PublicUrl.Base(http.Request, settings);
            store.EnsureWeeklyOwnerBrandMailingDraft(baseUrl);
            return RenderOwner(http, store, settings, releaseNotes,
                """<div class="notice success">Brand email schedule saved.</div>""", "promote-app");
        });

        app.MapPost("/owner/brand-email/approve", (HttpContext http, AppStoreDb store, AppSettings settings, ReleaseNotesCatalog releaseNotes) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            store.ApprovePendingMailingDraft(MailingListKinds.Brand);
            return RenderOwner(http, store, settings, releaseNotes,
                """<div class="notice success">Brand email draft approved for auto-send.</div>""", "promote-app");
        });

        app.MapPost("/owner/brand-email/generate", (HttpContext http, AppStoreDb store, AppSettings settings, ReleaseNotesCatalog releaseNotes) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            var baseUrl = PublicUrl.Base(http.Request, settings);
            var (_, _, error) = store.GenerateAndStoreOwnerBrandMailingDraft(baseUrl, regenerate: true);
            var notice = error is not null
                ? $"""<div class="notice error">{H.Encode(error)}</div>"""
                : """<div class="notice success">Brand email draft generated. Review below or approve for auto-send.</div>""";
            return RenderOwner(http, store, settings, releaseNotes, notice, "promote-app");
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

        app.MapPost("/owner/brand-schedule", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings, SocialPostingService posting, ReleaseNotesCatalog releaseNotes) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            var form = await request.ReadFormAsync();
            var platforms = form["platform"].ToList();
            var postsPerWeek = form["postsPerWeek"].ToList();
            var autoPostPlatforms = form["autoPostEnabled"].ToHashSet(StringComparer.OrdinalIgnoreCase);

            var schedules = new List<SocialSchedule>();
            for (var i = 0; i < platforms.Count; i++)
            {
                var platform = platforms[i] ?? "";
                if (string.IsNullOrWhiteSpace(platform)) continue;
                var parsed = int.TryParse(postsPerWeek.ElementAtOrDefault(i), out var count) ? count : 0;
                schedules.Add(new SocialSchedule
                {
                    Platform = platform,
                    PostsPerWeek = Math.Clamp(parsed, 0, 14),
                    AutoPostEnabled = autoPostPlatforms.Contains(platform),
                    ScheduleKind = SocialScheduleKinds.Brand
                });
            }

            store.SaveBrandSchedules(schedules);
            var baseUrl = PublicUrl.Base(http.Request, settings);
            var posted = await store.RunDueOwnerPromosAsync(posting, baseUrl);
            var notice = posted > 0
                ? $"""<div class="notice success">Brand schedule saved. {posted} app promo(s) auto-posted now.</div>"""
                : """<div class="notice success">Brand auto-post schedule saved. Promos will go out on schedule when due (every 5 minutes).</div>""";
            return RenderOwner(http, store, settings, releaseNotes, notice, "owner-social");
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

        app.MapPost("/owner/facebook-diagnostics", async (
            HttpRequest request,
            HttpContext http,
            AppStoreDb store,
            AppSettings settings,
            FacebookService facebook,
            ReleaseNotesCatalog releaseNotes) =>
        {
            if (OwnerGuard(store) is { } guard) return guard;
            var form = await request.ReadFormAsync();
            var runProbePost = form.ContainsKey("runProbePost");
            var includeAuthors = form.ContainsKey("includeAuthors");
            var diagnostics = await store.RunFacebookPostingDiagnosticsAsync(facebook, includeAuthors, runProbePost);
            var html = FacebookDiagnosticsHtml.RenderPanel(
                diagnostics,
                "/owner/facebook-diagnostics",
                sectionAnchor: "facebook-diagnostics",
                showAuthorAccountsOption: true);
            return RenderOwner(http, store, settings, releaseNotes, activeSection: "facebook-api", facebookDiagnosticsHtml: html);
        });

        app.MapGet("/owner-login", (AppStoreDb store) => OwnerGuard(store) ?? Results.Redirect("/owner-promos"));
        app.MapPost("/owner-login", (AppStoreDb store) => OwnerGuard(store) ?? Results.Redirect("/owner-promos"));
    }

    static IResult RenderOwner(HttpContext http, AppStoreDb store, AppSettings settings, ReleaseNotesCatalog releaseNotes, string notice = "", string? activeSection = null, string facebookDiagnosticsHtml = "")
    {
        try
        {
            return Results.Content(
                H.RenderPage(http, "Owner", OwnerPage.Render(store, notice, PublicUrl.Base(http.Request, settings), releaseNotes, activeSection, facebookDiagnosticsHtml), store),
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
