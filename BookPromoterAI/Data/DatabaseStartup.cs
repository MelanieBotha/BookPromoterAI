using Microsoft.EntityFrameworkCore;

namespace BookPromoterAI;

static class DatabaseStartup
{
    static volatile bool _isReady;
    static volatile bool _isRunning;
    static string? _lastError;

    public static bool IsReady => _isReady;
    public static string? LastError => _lastError;

    public static async Task InitializeAsync(IServiceProvider services)
    {
        if (_isReady || _isRunning) return;
        _isRunning = true;

        try
        {
            using var scope = services.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            DatabaseInitializer.ApplyMigrations(db);
            scope.ServiceProvider.GetRequiredService<AppStoreDb>().SeedPromoCodes();
            scope.ServiceProvider.GetRequiredService<AppStoreDb>().SeedOwnerAccount();
            _lastError = null;
            _isReady = true;
            Console.WriteLine("[Startup] Database ready.");
        }
        catch (Exception ex)
        {
            _lastError = ex.ToString();
            Console.Error.WriteLine($"[Startup] Database setup failed: {ex}");
        }
        finally
        {
            _isRunning = false;
        }
    }
}

class DatabaseBootstrapHostedService : IHostedService
{
    private readonly IServiceProvider _services;

    public DatabaseBootstrapHostedService(IServiceProvider services) => _services = services;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => DatabaseStartup.InitializeAsync(_services), cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
