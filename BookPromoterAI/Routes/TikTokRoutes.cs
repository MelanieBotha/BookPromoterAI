namespace BookPromoterAI;

static class TikTokRoutes
{
    public static void Map(WebApplication app, string uploadsDir, PostGenerator generator, LocalSpeechService speech)
    {
        app.MapGet("/tiktok", (HttpRequest request) =>
            Results.Redirect("/videos" + request.QueryString));

        app.MapGet("/videos", (HttpRequest request, HttpContext http, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var notice = request.Query["created"] == "1"
                ? """<div class="notice success">Video created! Download it below and post to social media when you are ready.</div>"""
                : request.Query["uploaded"] == "1"
                    ? """<div class="notice success">Video uploaded.</div>"""
                    : request.Query["error"] == "1"
                        ? $"""<div class="notice error">{H.Encode(request.Query["msg"].ToString())}</div>"""
                        : "";
            return Results.Content(
                H.RenderPage(http, "Videos", TikTokPage.Render(store, generator, notice), store),
                "text/html");
        });

        app.MapPost("/videos/speech", async (HttpRequest request, AppStoreDb store, LocalSpeechService speechService) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Unauthorized();
            var form = await request.ReadFormAsync();
            var text = form["text"].ToString();
            var (wav, durationMs, error) = await speechService.SynthesizeAsync(text);
            if (wav is null)
                return Results.Json(new { error = error ?? "Could not generate speech." });

            var plan = ReadAloudScript.Build(text, TikTokVideoLimits.ClampSpeechMs(durationMs));
            return Results.Json(new
            {
                wavBase64 = Convert.ToBase64String(wav),
                durationMs = TikTokVideoLimits.ClampSpeechMs(durationMs),
                maxDurationMs = TikTokVideoLimits.MaxDurationMs,
                beats = plan.Beats.Select(b => new { text = b.Text, startMs = b.StartMs, endMs = b.EndMs })
            });
        });

        app.MapPost("/videos/create", async (HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            return await SaveVideoAsync(request, store, uploadsDir, "/videos?created=1");
        });

        app.MapPost("/videos/upload", async (HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            return await SaveVideoAsync(request, store, uploadsDir, "/videos?uploaded=1");
        });

        app.MapPost("/videos/delete/{id:int}", (int id, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            store.DeleteTikTokVideo(id);
            return Results.Redirect("/videos");
        });

        // Legacy paths (redirect GET only; POST handlers duplicated)
        app.MapPost("/tiktok/create", async (HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            return await SaveVideoAsync(request, store, uploadsDir, "/videos?created=1");
        });
        app.MapPost("/tiktok/upload", async (HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            return await SaveVideoAsync(request, store, uploadsDir, "/videos?uploaded=1");
        });
        app.MapPost("/tiktok/delete/{id:int}", (int id, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            store.DeleteTikTokVideo(id);
            return Results.Redirect("/videos");
        });
    }

    static async Task<IResult> SaveVideoAsync(HttpRequest request, AppStoreDb store, string uploadsDir, string successUrl)
    {
        var form = await request.ReadFormAsync();
        var bookIdText = form["bookId"].ToString();
        var title = form["title"].ToString().Trim();
        var caption = form["caption"].ToString().Trim();
        var file = form.Files.GetFile("video");

        if (string.IsNullOrWhiteSpace(title))
            return Results.Redirect("/videos?error=1&msg=" + Uri.EscapeDataString("Enter a video title."));

        var videoUrl = await FileHelpers.SaveVideoUpload(file, uploadsDir);
        if (videoUrl is null)
            return Results.Redirect("/videos?error=1&msg=" + Uri.EscapeDataString("Could not save video. Try again or use MP4/MOV/WEBM under 1 GB."));

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
        return Results.Redirect(successUrl);
    }
}
