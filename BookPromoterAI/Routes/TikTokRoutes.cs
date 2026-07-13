namespace BookPromoterAI;

static class TikTokRoutes
{
    public static void Map(WebApplication app, string uploadsDir, PostGenerator generator, LocalSpeechService speech)
    {
        app.MapGet("/tiktok", (HttpRequest request) =>
            Results.Redirect("/videos" + request.QueryString));

        app.MapGet("/videos", (HttpRequest request, HttpContext http, AppStoreDb store, AppSettings settings, VideoRenderService renderer, LocalSpeechService speechService, UploadPaths uploads, IServiceScopeFactory scopes) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var baseUrl = PublicUrl.Base(request, settings);
            var queued = store.EnsureWeeklyVideos(generator, baseUrl);
            store.ResetStuckRenderingVideos(TimeSpan.FromMinutes(15));
            // Do not await a full render on page load — that blocks the browser and times out proxies.
            KickBackgroundRender(scopes, uploads.Path, baseUrl);
            var notice = request.Query["created"] == "1"
                ? """<div class="notice success">Video created! Download it below and post to social media when you are ready.</div>"""
                : request.Query["uploaded"] == "1"
                    ? """<div class="notice success">Video uploaded.</div>"""
                    : request.Query["deleted"] == "1"
                        ? """<div class="notice success">Video removed.</div>"""
                        : request.Query["retried"] == "1"
                            ? """<div class="notice success">Video queued again. Status will change from Rendering to Ready in a few minutes — refresh this page.</div>"""
                        : request.Query["regenerated"] == "1"
                            ? """<div class="notice success">This week's videos were queued. Status will change from Rendering to Ready in a few minutes — refresh this page.</div>"""
                    : request.Query["error"] == "1"
                        ? $"""<div class="notice error">{H.Encode(request.Query["msg"].ToString())}</div>"""
                        : queued > 0
                            ? $"""<div class="notice success">Queued {queued} new video(s) for this week — they will appear below when rendering finishes (usually within a few minutes).</div>"""
                            : "";
            if (!renderer.IsFfmpegAvailable)
                notice += """<div class="notice error">FFmpeg is missing on this server — weekly videos cannot render. Redeploy using the root Dockerfile.</div>""";
            notice += speechService.IsNaturalVoiceConfigured
                ? $"""<div class="notice success">Voice: natural ElevenLabs ({H.Encode(speechService.DiagnosticStatus())}). Retry a video to regenerate with this voice.</div>"""
                : """<div class="notice error">Voice: robotic local TTS. Add ElevenLabs__ApiKey in Railway Variables, Deploy, then Retry videos.</div>""";
            return Results.Content(
                H.RenderPage(http, "Videos", TikTokPage.Render(store, generator, notice), store),
                "text/html");
        });

        app.MapPost("/videos/speech", async (HttpRequest request, AppStoreDb store, LocalSpeechService speechService) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Unauthorized();
            var form = await request.ReadFormAsync();
            var text = form["text"].ToString();
            var speech = await speechService.SynthesizeAsync(text);
            if (!speech.Ok || speech.Data is null)
                return Results.Json(new { error = speech.Error ?? "Could not generate speech." });

            var plan = speech.WordTimings is { Count: > 0 }
                ? ReadAloudScript.BuildFromWordTimings(speech.WordTimings)
                : ReadAloudScript.Build(text, TikTokVideoLimits.ClampSpeechMs(speech.DurationMs));
            return Results.Json(new
            {
                wavBase64 = Convert.ToBase64String(speech.Data),
                durationMs = TikTokVideoLimits.ClampSpeechMs(speech.DurationMs),
                maxDurationMs = TikTokVideoLimits.MaxDurationMs,
                provider = speech.Provider,
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

        app.MapPost("/videos/delete/{id:int}", (int id, HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var ok = store.DeleteTikTokVideo(id);
            if (request.Query.ContainsKey("ajax"))
                return ok ? Results.Json(new { ok = true }) : Results.Json(new { ok = false }, statusCode: 404);
            return Results.Redirect(ok ? "/videos?deleted=1#videos-week" : "/videos?error=1&msg=" + Uri.EscapeDataString("Could not remove that video."));
        });

        app.MapPost("/videos/retry/{id:int}", (int id, HttpRequest request, AppStoreDb store, AppSettings settings, UploadPaths uploads, IServiceScopeFactory scopes) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var baseUrl = PublicUrl.Base(request, settings);
            var retried = store.RetryFailedWeeklyVideo(id, generator, baseUrl);
            if (retried > 0)
                KickBackgroundRender(scopes, uploads.Path, baseUrl);
            if (request.Query.ContainsKey("ajax"))
                return retried > 0 ? Results.Json(new { ok = true }) : Results.Json(new { ok = false, error = "Video not found or not in Failed state. Refresh the page." }, statusCode: 404);
            return Results.Redirect(retried > 0 ? "/videos?retried=1#videos-week" : "/videos?error=1&msg=" + Uri.EscapeDataString("Could not retry that video."));
        });

        app.MapPost("/videos/regenerate/{id:int}", (int id, HttpRequest request, AppStoreDb store, AppSettings settings, UploadPaths uploads, IServiceScopeFactory scopes) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var baseUrl = PublicUrl.Base(request, settings);
            var ok = store.RegenerateWeeklyVideo(id, generator, baseUrl, allowReady: true);
            if (ok > 0)
                KickBackgroundRender(scopes, uploads.Path, baseUrl);
            if (request.Query.ContainsKey("ajax"))
                return ok > 0 ? Results.Json(new { ok = true }) : Results.Json(new { ok = false, error = "Video not found. Refresh the page." }, statusCode: 404);
            return Results.Redirect(ok > 0 ? "/videos?retried=1#videos-week" : "/videos?error=1&msg=" + Uri.EscapeDataString("Could not regenerate that video."));
        });

        app.MapPost("/videos/regenerate-week", (HttpRequest request, AppStoreDb store, AppSettings settings, UploadPaths uploads, IServiceScopeFactory scopes) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var baseUrl = PublicUrl.Base(request, settings);
            var queued = store.RegenerateThisWeeksVideos(generator, baseUrl);
            if (queued > 0)
                KickBackgroundRender(scopes, uploads.Path, baseUrl);
            if (request.Query.ContainsKey("ajax"))
                return Results.Json(new { ok = true, queued });
            return Results.Redirect(queued > 0
                ? $"/videos?regenerated=1&n={queued}#videos-week"
                : "/videos?error=1&msg=" + Uri.EscapeDataString("No books with covers to generate. Add a cover under Books first."));
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
        app.MapPost("/tiktok/delete/{id:int}", (int id, HttpRequest request, AppStoreDb store) =>
        {
            if (!store.IsLoggedIn || !store.HasCustomerAccess) return Results.Redirect("/start");
            var ok = store.DeleteTikTokVideo(id);
            return Results.Redirect(ok ? "/videos?deleted=1#videos-week" : "/videos");
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

    static void KickBackgroundRender(IServiceScopeFactory scopes, string uploadsPath, string baseUrl)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var bgStore = scope.ServiceProvider.GetRequiredService<AppStoreDb>();
                var bgRenderer = scope.ServiceProvider.GetRequiredService<VideoRenderService>();
                await bgStore.RenderPendingVideosAsync(bgRenderer, uploadsPath, baseUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Videos] Background render failed: {ex.Message}");
            }
        });
    }
}
