namespace BookPromoterAI;

class PostingSchedulerServiceDb : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SocialPostingService _postingService;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    public PostingSchedulerServiceDb(IServiceScopeFactory scopeFactory, SocialPostingService postingService)
    {
        _scopeFactory = scopeFactory;
        _postingService = postingService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<AppStoreDb>();
                await store.RunDuePostsAsync(_postingService);
            }
            catch { /* log and continue */ }

            try { await Task.Delay(CheckInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
