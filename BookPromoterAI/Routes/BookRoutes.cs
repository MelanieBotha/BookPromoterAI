namespace BookPromoterAI;

static class BookRoutes
{
    public static void Map(WebApplication app, PostGenerator generator, string uploadsDir)
    {
        app.MapGet("/books", (HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            return Results.Content(H.RenderPage(http, "Books", BooksPage.Render(store, null), store), "text/html");
        });

        app.MapGet("/books/edit/{id:int}", (int id, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var book = store.FindBook(id);
            if (book is null) return Results.Redirect("/books");
            return Results.Content(H.RenderPage(http, "Edit Book", BooksPage.Render(store, book), store), "text/html");
        });

        app.MapPost("/books", async (HttpRequest request, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            if (store.CheckBookLimit() is not null) return Results.Redirect("/books");
            var form = await request.ReadFormAsync();
            var book = new Book
            {
                Title = form["title"].ToString(), AuthorName = form["authorName"].ToString(),
                Genre = BookGenres.ParseFromForm(form), Description = H.LimitWords(form["description"].ToString(), 200),
                ReadAloudExcerpt = ReadAloudScript.LimitWords(form["readAloudExcerpt"].ToString())
            };
            var uploaded = await FileHelpers.SaveCoverUpload(form.Files.GetFile("coverFile"), uploadsDir);
            if (uploaded is not null) book.CoverImageUrl = uploaded;
            book.Links = FileHelpers.ParseLinks(form);
            store.AddBook(book);
            var baseUrl = PublicUrl.Base(request, settings);
            var mailingGenerator = new MailingListEmailGenerator();
            await store.TrySendPendingNewReleaseMailingAsync(
                mailingGenerator, baseUrl,
                settings.SendGridApiKey, settings.SendGridSenderEmail, settings.SendGridSenderName);
            var schedule = store.Schedules.FirstOrDefault(s => s.PostsPerWeek > 0);
            var platform = schedule?.Platform ?? "General";
            var purchaseUrl = PostBranding.PurchaseUrlForPost(book, baseUrl, platform);
            var text = generator.Generate(book, platform, purchaseUrl, book.PostVariantSeed, baseUrl);
            store.RecordGeneratedAd(book, platform, text);
            return Results.Redirect("/books");
        });

        app.MapPost("/books/edit/{id:int}", async (int id, HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var book = store.FindBook(id);
            if (book is null) return Results.Redirect("/books");
            var form = await request.ReadFormAsync();
            book.Title = form["title"].ToString(); book.AuthorName = form["authorName"].ToString();
            book.Genre = BookGenres.ParseFromForm(form); book.Description = H.LimitWords(form["description"].ToString(), 200);
            book.ReadAloudExcerpt = ReadAloudScript.LimitWords(form["readAloudExcerpt"].ToString());
            var uploaded = await FileHelpers.SaveCoverUpload(form.Files.GetFile("coverFile"), uploadsDir);
            if (uploaded is not null) book.CoverImageUrl = uploaded;
            book.Links = FileHelpers.ParseLinks(form);
            store.UpdateBook(book);
            return Results.Redirect("/books");
        });

        app.MapPost("/books/delete/{id:int}", (int id, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            store.RemoveBook(id);
            return Results.Redirect("/books");
        });

        app.MapPost("/books/{id:int}/regenerate-post", (HttpRequest request, int id, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var book = store.FindBook(id);
            if (book is not null)
            {
                book.PostVariantSeed++;
                store.UpdateBook(book);
                var baseUrl = PublicUrl.Base(request, settings);
                var schedule = store.Schedules.FirstOrDefault(s => s.PostsPerWeek > 0);
                var platform = schedule?.Platform ?? "General";
                var purchaseUrl = PostBranding.PurchaseUrlForPost(book, baseUrl, platform);
                var text = generator.Generate(book, platform, purchaseUrl, book.PostVariantSeed, baseUrl);
                store.RecordGeneratedAd(book, platform, text);
            }
            return Results.Redirect("/dashboard");
        });

        app.MapGet("/book/{trackingCode}", (string trackingCode, HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            var appBaseUrl = PublicUrl.Base(http.Request, settings);
            var pageUrl = PostBranding.BookShareUrl(appBaseUrl, trackingCode);
            var imageSize = ResolveCoverImageSize(store, uploadsDir, trackingCode);

            if (SocialCrawler.IsCrawler(request.Headers.UserAgent.ToString()))
            {
                var book = store.FindBookByTrackingCode(trackingCode);
                if (book is null) return Results.NotFound("Book not found.");
                return Results.Content(
                    PostBranding.RenderCrawlerPreviewHtml(book, pageUrl, appBaseUrl, imageSize),
                    "text/html");
            }

            var clicked = store.RecordClick(trackingCode, request.Query["from"].ToString());
            if (clicked is null) return Results.NotFound("Book not found.");
            var description = string.IsNullOrWhiteSpace(clicked.Description)
                ? $"Discover {clicked.Title} by {clicked.AuthorName}"
                : H.LimitWords(clicked.Description, 40);
            var ogMeta = PostBranding.BuildBookShareMeta(clicked, pageUrl, appBaseUrl, imageSize);
            return Results.Content(
                H.RenderMarketingPage(http, clicked.Title, PublicBookPage.Render(clicked, appBaseUrl, appBaseUrl), store, ogMeta, description),
                "text/html");
        });

        app.MapGet("/book/{trackingCode}/cover", (string trackingCode, AppStoreDb store) =>
        {
            var book = store.FindBookByTrackingCode(trackingCode);
            if (book is null || string.IsNullOrWhiteSpace(book.CoverImageUrl))
                return Results.NotFound("Cover not found.");

            if (book.CoverImageUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            {
                var path = Path.Combine(uploadsDir, Path.GetFileName(book.CoverImageUrl));
                if (!File.Exists(path)) return Results.NotFound("Cover not found.");
                var info = CoverImageInfo.TryGetLocal(uploadsDir, book.CoverImageUrl);
                var contentType = info?.ContentType ?? "image/jpeg";
                return Results.File(path, contentType);
            }

            if (book.CoverImageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                book.CoverImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return Results.Redirect(book.CoverImageUrl);

            return Results.NotFound("Cover not found.");
        });

        app.MapGet("/go/{trackingCode}", (string trackingCode, AppStoreDb store) =>
        {
            var book = store.RecordClick(trackingCode);
            if (book is null) return Results.NotFound("Tracking link not found.");
            var destination = book.Links.FirstOrDefault()?.Url;
            return string.IsNullOrWhiteSpace(destination) || !UrlSafety.IsSafeRedirect(destination)
                ? Results.NotFound("No valid purchase link.")
                : Results.Redirect(destination);
        });
    }

    static (int Width, int Height)? ResolveCoverImageSize(AppStoreDb store, string uploadsDir, string trackingCode)
    {
        var book = store.FindBookByTrackingCode(trackingCode);
        if (book is null) return null;
        var info = CoverImageInfo.TryGetLocal(uploadsDir, book.CoverImageUrl);
        if (info is null || info.Value.Width <= 0 || info.Value.Height <= 0) return null;
        return (info.Value.Width, info.Value.Height);
    }
}
