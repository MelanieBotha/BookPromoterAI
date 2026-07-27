namespace BookPromoterAI;

static class PublicFeedbackRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/app-feedback", (HttpContext http, AppStoreDb store) =>
        {
            var tab = http.Request.Query["tab"].ToString();
            if (string.Equals(tab, "feedback", StringComparison.OrdinalIgnoreCase))
                tab = "reviews";
            var sort = http.Request.Query["sort"].ToString();
            var category = http.Request.Query["category"].ToString();
            return Results.Content(
                H.RenderMarketingPage(
                    http,
                    "Reviews & Forum",
                    PublicFeedbackPage.Render(store, tab, forumSort: sort, forumCategory: category),
                    store,
                    metaDescription: "Read BookPromoter AI reviews and join the community forum — browse freely before you sign up."),
                "text/html");
        });

        app.MapGet("/app-feedback/thread/{id:int}", (int id, HttpContext http, AppStoreDb store) =>
        {
            var (thread, posts) = store.GetForumThread(id, incrementView: true);
            if (thread is null)
            {
                return Results.Content(
                    H.RenderMarketingPage(
                        http,
                        "Reviews & Forum",
                        PublicFeedbackPage.Render(store, "forum", """<div class="notice error">Topic not found.</div>"""),
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

        app.MapPost("/app-feedback/review", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn)
                return Results.Redirect("/start");

            var form = await request.ReadFormAsync();
            _ = int.TryParse(form["rating"].ToString(), out var rating);
            var (_, error) = store.AddAppReview(rating, form["body"].ToString());
            var notice = error is not null
                ? $"""<div class="notice error">{H.Encode(error)}</div>"""
                : """<div class="notice success">Thanks — your review is live.</div>""";
            return Results.Content(
                H.RenderMarketingPage(
                    http,
                    "Reviews & Forum",
                    PublicFeedbackPage.Render(store, "reviews", notice),
                    store),
                "text/html");
        });

        app.MapPost("/app-feedback/review/{id:int}/remove", (int id, AppStoreDb store) =>
        {
            if (!store.IsOwner) return Results.Redirect("/app-feedback?tab=reviews");
            store.RemoveAppReview(id);
            return Results.Redirect("/app-feedback?tab=reviews");
        });

        // Legacy general-feedback submit → reviews form
        app.MapPost("/app-feedback/submit", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn)
                return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var (_, error) = store.AddAppReview(5, form["message"].ToString());
            var notice = error is not null
                ? $"""<div class="notice error">{H.Encode(error)}</div>"""
                : """<div class="notice success">Thanks — your review is live.</div>""";
            return Results.Content(
                H.RenderMarketingPage(http, "Reviews & Forum", PublicFeedbackPage.Render(store, "reviews", notice), store),
                "text/html");
        });

        app.MapPost("/app-feedback/forum/thread", async (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn)
                return Results.Redirect("/start");

            var form = await request.ReadFormAsync();
            var (thread, error) = store.CreateForumThread(
                form["title"].ToString(),
                form["body"].ToString(),
                form["category"].ToString());
            if (error is not null || thread is null)
            {
                return Results.Content(
                    H.RenderMarketingPage(
                        http,
                        "Reviews & Forum",
                        PublicFeedbackPage.Render(store, "forum", $"""<div class="notice error">{H.Encode(error ?? "Could not create topic.")}</div>"""),
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
                        "Reviews & Forum",
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
