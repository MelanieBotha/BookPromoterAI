namespace BookPromoterAI;

static class PublicFeedbackRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/app-feedback", (HttpContext http, AppStoreDb store) =>
        {
            var tab = http.Request.Query["tab"].ToString();
            return Results.Content(
                H.RenderMarketingPage(
                    http,
                    "App Feedback & Forum",
                    PublicFeedbackPage.Render(store, tab),
                    store,
                    metaDescription: "See public feedback for BookPromoter AI and join the community forum — browse freely before you sign up."),
                "text/html");
        });

        app.MapGet("/app-feedback/thread/{id:int}", (int id, HttpContext http, AppStoreDb store) =>
        {
            var (thread, posts) = store.GetForumThread(id);
            if (thread is null)
            {
                return Results.Content(
                    H.RenderMarketingPage(
                        http,
                        "App Feedback & Forum",
                        PublicFeedbackPage.Render(store, "forum", """<div class="notice error">Thread not found.</div>"""),
                        store),
                    "text/html");
            }

            return Results.Content(
                H.RenderMarketingPage(
                    http,
                    thread.Title,
                    PublicFeedbackPage.Render(store, "forum", "", thread, posts),
                    store),
                "text/html");
        });

        app.MapPost("/app-feedback/submit", async (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.IsLoggedIn)
                return Results.Redirect("/start");

            var form = await request.ReadFormAsync();
            var email = form["email"].ToString();
            var message = form["message"].ToString();
            if (string.IsNullOrWhiteSpace(message))
            {
                return Results.Content(
                    H.RenderMarketingPage(
                        http,
                        "App Feedback & Forum",
                        PublicFeedbackPage.Render(store, "feedback", """<div class="notice error">Please enter a message.</div>"""),
                        store),
                    "text/html");
            }

            var entry = store.AddFeedback(email, "General Feedback", message);
            var baseUrl = PublicUrl.Base(http.Request, settings);
            await EmailService.SendThankYouEmail(email, entry.ThankYouEmail, settings.SendGridApiKey, settings.SendGridSenderEmail, settings.SendGridSenderName, baseUrl);
            await EmailService.SendOwnerFeedbackNotificationEmail(entry, settings.SendGridApiKey, settings.SendGridSenderEmail, settings.SendGridSenderName, baseUrl);

            return Results.Content(
                H.RenderMarketingPage(
                    http,
                    "App Feedback & Forum",
                    PublicFeedbackPage.Render(store, "feedback", """<div class="notice success">Thanks — your general feedback is now on this page.</div>"""),
                    store),
                "text/html");
        });

        app.MapPost("/app-feedback/forum/thread", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn)
                return Results.Redirect("/start");

            var form = await request.ReadFormAsync();
            var (thread, error) = store.CreateForumThread(form["title"].ToString(), form["body"].ToString());
            if (error is not null || thread is null)
            {
                return Results.Content(
                    H.RenderMarketingPage(
                        http,
                        "App Feedback & Forum",
                        PublicFeedbackPage.Render(store, "forum", $"""<div class="notice error">{H.Encode(error ?? "Could not create thread.")}</div>"""),
                        store),
                    "text/html");
            }

            return Results.Redirect($"/app-feedback/thread/{thread.Id}");
        });

        app.MapPost("/app-feedback/forum/thread/{id:int}/reply", async (int id, HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn)
                return Results.Redirect("/start");

            var form = await request.ReadFormAsync();
            var (_, error) = store.ReplyToForumThread(id, form["body"].ToString());
            if (error is not null)
            {
                var (thread, posts) = store.GetForumThread(id);
                return Results.Content(
                    H.RenderMarketingPage(
                        http,
                        "App Feedback & Forum",
                        PublicFeedbackPage.Render(store, "forum", $"""<div class="notice error">{H.Encode(error)}</div>""", thread, posts),
                        store),
                    "text/html");
            }

            return Results.Redirect($"/app-feedback/thread/{id}");
        });

        app.MapPost("/app-feedback/forum/thread/{id:int}/remove", (int id, AppStoreDb store) =>
        {
            if (!store.IsOwner) return Results.Redirect("/app-feedback?tab=forum");
            store.RemoveForumThread(id);
            return Results.Redirect("/app-feedback?tab=forum");
        });

        app.MapPost("/app-feedback/forum/post/{id:int}/remove", (int id, HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsOwner) return Results.Redirect("/app-feedback?tab=forum");
            store.RemoveForumPost(id);
            var referer = request.Headers.Referer.ToString();
            if (!string.IsNullOrWhiteSpace(referer) && referer.Contains("/app-feedback/thread/", StringComparison.OrdinalIgnoreCase))
                return Results.Redirect(referer);
            return Results.Redirect("/app-feedback?tab=forum");
        });
    }
}
