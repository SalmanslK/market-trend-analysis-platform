using MarketAnalytics.Infrastructure.News;

namespace MarketAnalytics.Worker;

/// <summary>
/// Background service that collects business news every hour (the work itself is in <see cref="NewsUpdater"/>,
/// shared with the API's refresh button).
/// </summary>
public class NewsUpdateBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NewsUpdateBackgroundService> _logger;
    private readonly TimeSpan _updateInterval = TimeSpan.FromHours(1);

    public NewsUpdateBackgroundService(IServiceProvider serviceProvider, ILogger<NewsUpdateBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("News Update Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                await scope.ServiceProvider.GetRequiredService<NewsUpdater>().UpdateAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during scheduled news update");
            }

            await Task.Delay(_updateInterval, stoppingToken);
        }
    }
}
