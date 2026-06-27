namespace BookPromoterAI;

static class MailingListRoutes
{
    public static void Map(WebApplication app, MailingListEmailGenerator emailGenerator)
    {
        app.MapGet("/mailing-list", (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.HasCustomerAccess || !store.IsLoggedIn) return Results.Redirect("/start");
            var baseUrl = PublicUrl.Base(request, settings);
            MailingListCampaign? viewed = null;
            if (int.TryParse(request.Query["view"].ToString(), out var viewId))
                viewed = store.GetMailingListCampaign(viewId);
            return Results.Content(H.RenderPage(http, "Mailing List", MailingListPage.Render(store, "", baseUrl, viewedCampaign: viewed), store), "text/html");
        });

        app.MapPost("/mailing-list/generate", (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.HasCustomerAccess || !store.IsLoggedIn) return Results.Redirect("/start");
            var baseUrl = PublicUrl.Base(request, settings);
            var (subject, body, bookId, error) = store.BuildMailingListDraft(emailGenerator, baseUrl);
            var notice = error is not null
                ? $"""<div class="notice error">{H.Encode(error)}</div>"""
                : """<div class="notice success">Email draft generated from your books. Review and send when ready.</div>""";
            return Results.Content(H.RenderPage(http, "Mailing List", MailingListPage.Render(store, notice, baseUrl, subject, body, bookId), store), "text/html");
        });

        app.MapPost("/mailing-list/regenerate", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.HasCustomerAccess || !store.IsLoggedIn) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var bookId = int.TryParse(form["bookId"].ToString(), out var id) ? id : (int?)null;
            var baseUrl = PublicUrl.Base(request, settings);
            var (subject, body, newBookId, error) = store.BuildMailingListDraft(emailGenerator, baseUrl, bookId, regenerate: true);
            var notice = error is not null
                ? $"""<div class="notice error">{H.Encode(error)}</div>"""
                : """<div class="notice success">Email draft regenerated.</div>""";
            return Results.Content(H.RenderPage(http, "Mailing List", MailingListPage.Render(store, notice, baseUrl, subject, body, newBookId), store), "text/html");
        });

        app.MapPost("/mailing-list/subscribers", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.HasCustomerAccess || !store.IsLoggedIn) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var (success, message) = store.AddMailingListSubscriber(form["email"].ToString(), form["name"].ToString());
            var cls = success ? "success" : "error";
            var notice = $"""<div class="notice {cls}">{H.Encode(message)}</div>""";
            var baseUrl = PublicUrl.Base(request, settings);
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
            var (sent, failed, message) = await store.SendMailingListCampaignAsync(
                form["subject"].ToString(),
                form["body"].ToString(),
                settings.SendGridApiKey,
                settings.SendGridSenderEmail,
                settings.SendGridSenderName,
                fromName);

            var cls = sent > 0 ? "success" : "error";
            var devNote = !settings.IsSendGridConfigured && sent > 0
                ? " <strong>Dev mode:</strong> SendGrid is not configured — emails were logged but not actually delivered."
                : "";
            var notice = $"""<div class="notice {cls}">{H.Encode(message)}{devNote}</div>""";
            var baseUrl = PublicUrl.Base(request, settings);
            return Results.Content(H.RenderPage(http, "Mailing List", MailingListPage.Render(store, notice, baseUrl), store), "text/html");
        });

        app.MapGet("/readers/signup/{userCode}", (string userCode, HttpContext http, AppStoreDb store) =>
        {
            var authorEmail = store.GetAuthorEmailByUserCode(userCode);
            if (authorEmail is null)
                return Results.Content(H.RenderPage(http, "Signup", """<div class="notice error">This signup link is not valid.</div>""", store), "text/html");
            return Results.Content(H.RenderPage(http, "Join Mailing List", MailingListPage.SignupPage(userCode, authorEmail, ""), store), "text/html");
        });

        app.MapPost("/readers/signup/{userCode}", async (string userCode, HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            var form = await request.ReadFormAsync();
            var (success, message, authorEmail) = store.SubscribeToMailingListByUserCode(userCode, form["email"].ToString(), form["name"].ToString());
            var cls = success ? "success" : "error";
            var notice = $"""<div class="notice {cls}">{H.Encode(message)}</div>""";
            return Results.Content(H.RenderPage(http, "Join Mailing List", MailingListPage.SignupPage(userCode, authorEmail ?? "", notice), store), "text/html");
        });
    }
}
