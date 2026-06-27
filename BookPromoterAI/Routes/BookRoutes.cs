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
                Genre = form["genre"].ToString(), Description = H.LimitWords(form["description"].ToString(), 200),
                CoverImageUrl = form["coverImageUrl"].ToString(), CoverSourceUrl = form["coverSourceUrl"].ToString()
            };
            var uploaded = await FileHelpers.SaveCoverUpload(form.Files.GetFile("coverFile"), uploadsDir);
            if (uploaded is not null) book.CoverImageUrl = uploaded;
            book.Links = FileHelpers.ParseLinks(form);
            store.AddBook(book);
            var baseUrl = PublicUrl.Base(request, settings);
            var purchaseUrl = PostBranding.PrimaryPurchaseUrl(book) ?? "";
            var schedule = store.Schedules.FirstOrDefault(s => s.PostsPerWeek > 0);
            var text = generator.Generate(book, schedule?.Platform ?? "General", purchaseUrl, book.PostVariantSeed, baseUrl);
            store.RecordGeneratedAd(book, schedule?.Platform ?? "General", text);
            return Results.Redirect("/books");
        });

        app.MapPost("/books/edit/{id:int}", async (int id, HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var book = store.FindBook(id);
            if (book is null) return Results.Redirect("/books");
            var form = await request.ReadFormAsync();
            book.Title = form["title"].ToString(); book.AuthorName = form["authorName"].ToString();
            book.Genre = form["genre"].ToString(); book.Description = H.LimitWords(form["description"].ToString(), 200);
            book.CoverImageUrl = form["coverImageUrl"].ToString(); book.CoverSourceUrl = form["coverSourceUrl"].ToString();
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
                var purchaseUrl = PostBranding.PrimaryPurchaseUrl(book) ?? "";
                var schedule = store.Schedules.FirstOrDefault(s => s.PostsPerWeek > 0);
                var text = generator.Generate(book, schedule?.Platform ?? "General", purchaseUrl, book.PostVariantSeed, baseUrl);
                store.RecordGeneratedAd(book, schedule?.Platform ?? "General", text);
            }
            return Results.Redirect("/dashboard");
        });

        app.MapGet("/book/{trackingCode}", (string trackingCode, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            var book = store.RecordClick(trackingCode);
            if (book is null) return Results.NotFound("Book not found.");
            var appBaseUrl = PublicUrl.Base(http.Request, settings);
            return Results.Content(
                H.RenderMarketingPage(http, book.Title, PublicBookPage.Render(book, appBaseUrl, PublicUrl.Local(http.Request)), store),
                "text/html");
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
}
