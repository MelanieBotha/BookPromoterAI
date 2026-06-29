namespace BookPromoterAI;

static class MailingListRoutes
{
    public static void Map(WebApplication app, MailingListEmailGenerator emailGenerator)
    {
        app.MapGet("/mailing-list", (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.HasCustomerAccess || !store.IsLoggedIn) return Results.Redirect("/start");
            var baseUrl = PublicUrl.Base(request, settings);
            store.EnsureWeeklyMailingDraft(emailGenerator, baseUrl);
            MailingListCampaign? viewed = null;
            if (int.TryParse(request.Query["view"].ToString(), out var viewId))
                viewed = store.GetMailingListCampaign(viewId);
            return Results.Content(H.RenderPage(http, "Mailing List", MailingListPage.Render(store, "", baseUrl, viewedCampaign: viewed), store), "text/html");
        });

        app.MapPost("/mailing-list/generate", (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.HasCustomerAccess || !store.IsLoggedIn) return Results.Redirect("/start");
            var baseUrl = PublicUrl.Base(request, settings);
            store.EnsureWeeklyMailingDraft(emailGenerator, baseUrl);
            var draft = store.MailingListSettings;
            var notice = """<div class="notice success">This week's featured book draft is ready. Review and send, or leave auto-send on.</div>""";
            return Results.Content(H.RenderPage(http, "Mailing List", MailingListPage.Render(
                store, notice, baseUrl, draft.PendingSubject, draft.PendingBody, draft.PendingBookId ?? 0), store), "text/html");
        });

        app.MapPost("/mailing-list/regenerate", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.HasCustomerAccess || !store.IsLoggedIn) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var bookId = int.TryParse(form["bookId"].ToString(), out var id) ? id : store.MailingListSettings.PendingBookId;
            var baseUrl = PublicUrl.Base(request, settings);
            var (subject, body, newBookId, error) = store.GenerateAndStoreMailingListDraft(
                emailGenerator, baseUrl, bookId, regenerate: true, advanceBook: false);
            var notice = error is not null
                ? $"""<div class="notice error">{H.Encode(error)}</div>"""
                : """<div class="notice success">Draft refreshed for this week's featured book.</div>""";
            return Results.Content(H.RenderPage(http, "Mailing List", MailingListPage.Render(store, notice, baseUrl, subject, body, newBookId), store), "text/html");
        });

        app.MapPost("/mailing-list/schedule", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.HasCustomerAccess || !store.IsLoggedIn) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var emailsPerWeek = int.TryParse(form["emailsPerWeek"].ToString(), out var n) ? n : 0;
            var autoSend = form.ContainsKey("autoSendEnabled");
            var requiresApproval = form.ContainsKey("requiresApproval");
            store.SaveMailingListSettings(emailsPerWeek, autoSend, requiresApproval);
            var baseUrl = PublicUrl.Base(request, settings);
            store.EnsureWeeklyMailingDraft(emailGenerator, baseUrl);
            var notice = """<div class="notice success">Email schedule saved.</div>""";
            return Results.Content(H.RenderPage(http, "Mailing List", MailingListPage.Render(store, notice, baseUrl), store), "text/html");
        });

        app.MapPost("/mailing-list/approve", (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.HasCustomerAccess || !store.IsLoggedIn) return Results.Redirect("/start");
            store.ApprovePendingMailingDraft();
            var baseUrl = PublicUrl.Base(request, settings);
            var notice = """<div class="notice success">Draft approved for auto-send.</div>""";
            return Results.Content(H.RenderPage(http, "Mailing List", MailingListPage.Render(store, notice, baseUrl), store), "text/html");
        });

        app.MapPost("/mailing-list/subscribers", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.HasCustomerAccess || !store.IsLoggedIn) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var baseUrl = PublicUrl.Base(request, settings);
            var (success, message, token, authorUserId) = store.AddMailingListSubscriber(form["email"].ToString(), form["name"].ToString());
            if (success && token is not null && authorUserId > 0)
                await store.SendSubscriberWelcomeEmailAsync(authorUserId, form["email"].ToString(), form["name"].ToString(), token, baseUrl);
            var cls = success ? "success" : "error";
            var notice = $"""<div class="notice {cls}">{H.Encode(message)}</div>""";
            return Results.Content(H.RenderPage(http, "Mailing List", MailingListPage.Render(store, notice, baseUrl), store), "text/html");
        });

        app.MapPost("/mailing-list/delete/{id:int}", (int id, AppStoreDb store) =>
        {
            if (!store.HasCustomerAccess || !store.IsLoggedIn) return Results.Redirect("/start");
            store.RemoveMailingListSubscriber(id);
            return Results.Redirect("/mailing-list");
        });

        app.MapPost("/mailing-list/send", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.HasCustomerAccess || !store.IsLoggedIn) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var fromName = store.LoggedInEmail ?? settings.SendGridSenderName;
            var baseUrl = PublicUrl.Base(request, settings);
            var bookId = int.TryParse(form["bookId"].ToString(), out var id) ? id : (int?)null;
            var (sent, failed, message) = await store.SendMailingListCampaignAsync(
                form["subject"].ToString(),
                form["body"].ToString(),
                settings.SendGridApiKey,
                settings.SendGridSenderEmail,
                settings.SendGridSenderName,
                fromName,
                baseUrl,
                bookId);

            var cls = sent > 0 ? "success" : "error";
            var devNote = !settings.IsSendGridConfigured && sent > 0
                ? " <strong>Dev mode:</strong> SendGrid is not configured — emails were logged but not actually delivered."
                : "";
            var notice = $"""<div class="notice {cls}">{H.Encode(message)}{devNote}</div>""";
            return Results.Content(H.RenderPage(http, "Mailing List", MailingListPage.Render(store, notice, baseUrl), store), "text/html");
        });

        app.MapGet("/readers/signup/{userCode}", (string userCode, HttpContext http, AppStoreDb store) =>
        {
            var authorEmail = store.GetAuthorEmailByUserCode(userCode);
            if (authorEmail is null)
                return Results.Content(H.RenderPage(http, "Signup", """<div class="notice error">This signup link is not valid.</div>""", store), "text/html");
            return Results.Content(H.RenderPage(http, "Join Mailing List", MailingListPage.SignupPage(userCode, authorEmail, ""), store), "text/html");
        });

        app.MapPost("/readers/signup/{userCode}", async (string userCode, HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            var form = await request.ReadFormAsync();
            var baseUrl = PublicUrl.Base(request, settings);
            var (success, message, authorEmail, token, authorUserId) = store.SubscribeToMailingListByUserCode(userCode, form["email"].ToString(), form["name"].ToString());
            if (success && token is not null && authorUserId > 0)
                await store.SendSubscriberWelcomeEmailAsync(authorUserId, form["email"].ToString(), form["name"].ToString(), token, baseUrl);
            var cls = success ? "success" : "error";
            var notice = $"""<div class="notice {cls}">{H.Encode(message)}</div>""";
            return Results.Content(H.RenderPage(http, "Join Mailing List", MailingListPage.SignupPage(userCode, authorEmail ?? "", notice), store), "text/html");
        });

        app.MapGet("/readers/unsubscribe/{token}", (string token, HttpContext http, AppStoreDb store) =>
            Results.Content(H.RenderPage(http, "Unsubscribe", MailingListPage.UnsubscribePage(token, "", store), store), "text/html"));

        app.MapPost("/readers/unsubscribe/{token}", (string token, HttpContext http, AppStoreDb store) =>
        {
            var (success, message, _) = store.UnsubscribeByToken(token);
            var cls = success ? "success" : "error";
            var notice = $"""<div class="notice {cls}">{H.Encode(message)}</div>""";
            return Results.Content(H.RenderPage(http, "Unsubscribe", MailingListPage.UnsubscribePage(token, notice, store, unsubscribed: success), store), "text/html");
        });

        app.MapPost("/mailing-list/unsubscribe/{id:int}", async (int id, HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.IsLoggedIn) return Results.Redirect("/start");
            var (success, message) = store.UnsubscribeFromMailingList(id);
            var cls = success ? "success" : "error";
            var notice = $"""<div class="notice {cls}">{H.Encode(message)}</div>""";
            var baseUrl = PublicUrl.Base(request, settings);
            return Results.Content(H.RenderPage(http, "Mailing List", MailingListPage.Render(store, notice, baseUrl), store), "text/html");
        });
    }
}
