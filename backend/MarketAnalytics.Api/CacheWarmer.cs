using MarketAnalytics.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace MarketAnalytics.Api;

/// <summary>
/// Recomputes the overview's answers (all analytics and the last 30 days' changes) soon after new prices or news
/// arrive, so nobody waits for them. Each check costs two small queries when nothing has changed.
/// </summary>
public class CacheWarmer : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CacheWarmer> _logger;

    public CacheWarmer(IServiceScopeFactory scopes, ILogger<CacheWarmer> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the API finish starting (migrations) first
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var cache = scope.ServiceProvider.GetRequiredService<DataCache>();
                var analytics = scope.ServiceProvider.GetRequiredService<IMarketAnalyticsService>();
                var alerts = scope.ServiceProvider.GetRequiredService<IAlertService>();
                var news = scope.ServiceProvider.GetRequiredService<IOptions<FeatureSettings>>().Value.News;

                // The same keys the controllers use for the overview's default requests
                await cache.GetOrComputeAsync("analytics", async () => (await analytics.GetAllAnalyticsAsync(stoppingToken)).ToList(), stoppingToken);
                await cache.GetOrComputeAsync($"alerts:30:{true}:{news}", () => alerts.GetRecentAsync(30, true, news, stoppingToken), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Warming the overview cache failed; pages will compute on demand");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
