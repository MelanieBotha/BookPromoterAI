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
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<AppStoreDb>();
                var postingService = scope.ServiceProvider.GetRequiredService<SocialPostingService>();
                await store.RunDuePostsAsync(postingService);
            }
            catch { /* log and continue */ }

            try { await Task.Delay(CheckInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
