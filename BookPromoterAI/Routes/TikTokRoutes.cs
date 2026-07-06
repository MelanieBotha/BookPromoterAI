namespace BookPromoterAI;

static class TikTokRoutes
{
    public static void Map(WebApplication app, string uploadsDir)
    {
        app.MapGet("/tiktok", (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var notice = request.Query["connected"] == "1"
                ? """<div class="notice success">TikTok account connected.</div>"""
                : request.Query["uploaded"] == "1"
                    ? """<div class="notice success">Video uploaded. Click <strong>Send to TikTok inbox</strong> when ready.</div>"""
                    : request.Query["posted"] == "1"
                        ? $"""<div class="notice success">{H.Encode(request.Query["msg"].ToString())}</div>"""
                        : request.Query["error"] == "1"
                            ? $"""<div class="notice error">{H.Encode(request.Query["msg"].ToString())}</div>"""
                            : "";
            return Results.Content(
                H.RenderPage(http, "TikTok", TikTokPage.Render(store, settings, notice), store),
                "text/html");
        });

        app.MapPost("/tiktok/upload", async (HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var form = await request.ReadFormAsync();
            var bookIdText = form["bookId"].ToString();
            var title = form["title"].ToString().Trim();
            var caption = form["caption"].ToString().Trim();
            var file = form.Files.GetFile("video");

            if (string.IsNullOrWhiteSpace(title))
                return Results.Redirect("/tiktok?error=1&msg=" + Uri.EscapeDataString("Enter a video title."));

            var videoUrl = await FileHelpers.SaveVideoUpload(file, uploadsDir);
            if (videoUrl is null)
                return Results.Redirect("/tiktok?error=1&msg=" + Uri.EscapeDataString("Could not save video. Use MP4/MOV/WEBM/AVI under 1 GB."));

            var bookId = 0;
            var bookTitle = "Book promo";
            if (int.TryParse(bookIdText, out var parsedId) && parsedId > 0)
            {
                var book = store.Books.FirstOrDefault(b => b.Id == parsedId);
                if (book is not null)
                {
                    bookId = book.Id;
                    bookTitle = book.Title;
                }
            }

            store.AddTikTokVideo(bookId, bookTitle, title, caption, videoUrl);
            return Results.Redirect("/tiktok?uploaded=1");
        });

        app.MapPost("/tiktok/post/{id:int}", async (int id, HttpRequest request, AppStoreDb store, AppSettings settings, TikTokService tiktok) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var appBaseUrl = PublicUrl.Base(request, settings);
            var (ok, message) = await store.PostTikTokVideoAsync(id, tiktok, appBaseUrl);
            if (ok)
                return Results.Redirect($"/tiktok?posted=1&msg={Uri.EscapeDataString(message)}");
            return Results.Redirect($"/tiktok?error=1&msg={Uri.EscapeDataString(message)}");
        });

        app.MapPost("/tiktok/delete/{id:int}", (int id, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            store.DeleteTikTokVideo(id);
            return Results.Redirect("/tiktok");
        });
    }
}
