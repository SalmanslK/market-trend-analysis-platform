using MarketAnalytics.Infrastructure.Updates;

namespace MarketAnalytics.Worker;

/// <summary>
/// Background service for scheduled price and payout updates (the work itself is in <see cref="MarketDataUpdater"/>,
/// shared with the API's refresh button).
/// Checks every few hours: each check only fetches what is new, and sources publish at different times
/// (indices after the close, fund NAVs in the evening or next morning), so a daily run started at an
/// arbitrary time could leave prices up to a day behind, or miss a day while the PC sleeps.
/// </summary>
public class DataUpdateBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataUpdateBackgroundService> _logger;
    private readonly TimeSpan _updateInterval = TimeSpan.FromHours(6);

    public DataUpdateBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<DataUpdateBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data Update Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                await scope.ServiceProvider.GetRequiredService<MarketDataUpdater>().UpdateAllAsync(stoppingToken);
                await RecordSignalsAsync(scope, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during scheduled data update");
            }

            await Task.Delay(_updateInterval, stoppingToken);
        }
    }

    // Saves the day's signals for the forward record; a failure here must not stop the price updates
    private async Task RecordSignalsAsync(IServiceScope scope, CancellationToken stoppingToken)
    {
        try
        {
            await scope.ServiceProvider.GetRequiredService<SignalRecorder>().RecordAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error recording signals");
        }
    }
}
