namespace BookPromoterAI;

class PostingSchedulerServiceDb : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    public PostingSchedulerServiceDb(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!DatabaseStartup.IsReady)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
                catch (TaskCanceledException) { break; }
                continue;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<AppStoreDb>();
                var postingService = scope.ServiceProvider.GetRequiredService<SocialPostingService>();
                var settings = scope.ServiceProvider.GetRequiredService<AppSettings>();
                var mailingGenerator = new MailingListEmailGenerator();
                var baseUrl = settings.PublicBaseUrl.TrimEnd('/');
                if (string.IsNullOrWhiteSpace(baseUrl))
                    baseUrl = "https://bookpromoterai.us";
                await store.RunDuePostsAsync(postingService);
                await store.RunDueOwnerPromosAsync(postingService, baseUrl);
                await store.RunDueMailingListEmailsAsync(
                    mailingGenerator,
                    baseUrl,
                    settings.SendGridApiKey,
                    settings.SendGridSenderEmail,
                    settings.SendGridSenderName);
                await store.RunDueOwnerBrandEmailsAsync(
                    baseUrl,
                    settings.SendGridApiKey,
                    settings.SendGridSenderEmail,
                    settings.SendGridSenderName);
                var generator = new PostGenerator();
                var uploads = scope.ServiceProvider.GetRequiredService<UploadPaths>();
                var videoRenderer = scope.ServiceProvider.GetRequiredService<VideoRenderService>();
                await store.RunWeeklyVideoPipelineAsync(generator, videoRenderer, uploads.Path, baseUrl, stoppingToken);
            }
            catch { /* log and continue */ }

            try { await Task.Delay(CheckInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
