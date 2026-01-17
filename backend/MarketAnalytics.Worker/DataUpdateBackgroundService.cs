using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Domain.ValueObjects;
using MarketAnalytics.Infrastructure.Data;
using MarketAnalytics.Infrastructure.DataFetchers;

namespace MarketAnalytics.Worker;

/// <summary>
/// Background service for scheduled data updates.
/// Matches Python logic: update_instrument_data() - fetches incremental data for each instrument.
/// Runs daily to update market data.
/// </summary>
public class DataUpdateBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataUpdateBackgroundService> _logger;
    private readonly TimeSpan _updateInterval = TimeSpan.FromHours(24); // Daily updates

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
                await UpdateAllInstrumentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled data update");
            }

            // Wait for next update interval
            await Task.Delay(_updateInterval, stoppingToken);
        }
    }

    private async Task UpdateAllInstrumentsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting scheduled data update for all instruments");

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fetcherFactory = scope.ServiceProvider.GetRequiredService<InstrumentDataFetcherFactory>();

        var activeInstruments = await dbContext.Instruments
            .Where(i => i.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var instrument in activeInstruments)
        {
            try
            {
                await UpdateInstrumentDataAsync(instrument, dbContext, fetcherFactory, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating data for instrument {Symbol}", instrument.Symbol);
                
                // Log the error to DataUpdateLog
                await LogUpdateResultAsync(dbContext, instrument, null, 0, UpdateStatus.Failed, ex.Message, cancellationToken);
            }
        }

        _logger.LogInformation("Completed scheduled data update");
    }

    private async Task UpdateInstrumentDataAsync(
        Instrument instrument,
        ApplicationDbContext dbContext,
        InstrumentDataFetcherFactory fetcherFactory,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing instrument: {Symbol}", instrument.Symbol);

        // Get latest date from database (matches Python: max_date = old_df["datetime"].max())
        var latestDate = await dbContext.PricePoints
            .Where(p => p.InstrumentId == instrument.Id)
            .OrderByDescending(p => p.DateTime)
            .Select(p => (DateTime?)p.DateTime)
            .FirstOrDefaultAsync(cancellationToken);

        // Get appropriate data fetcher
        var fetcher = fetcherFactory.GetFetcher(instrument.DataSourceType);

        // Fetch new data since latest date (matches Python: new_df = new_df[new_df["datetime"] > max_date])
        var newDataPoints = await fetcher.FetchHistoricalDataAsync(
            instrument.DataSourceUrl,
            instrument.Symbol,
            latestDate);

        var newDataList = newDataPoints.ToList();

        if (!newDataList.Any())
        {
            _logger.LogInformation("No new data found for {Symbol}", instrument.Symbol);
            await LogUpdateResultAsync(dbContext, instrument, latestDate, 0, UpdateStatus.Success, null, cancellationToken);
            return;
        }

        // Clean and validate data (positive prices, valid dates)
        var validData = newDataList
            .Where(dp => dp.Price > 0 && dp.DateTime >= new DateTime(1990, 1, 1) && dp.DateTime <= DateTime.UtcNow)
            .OrderBy(dp => dp.DateTime)
            .ToList();

        if (!validData.Any())
        {
            _logger.LogWarning("No valid data after cleaning for {Symbol}", instrument.Symbol);
            await LogUpdateResultAsync(dbContext, instrument, latestDate, 0, UpdateStatus.Failed, "No valid data after cleaning", cancellationToken);
            return;
        }

        // Convert to PricePoint entities
        var pricePoints = validData.Select(dp => new PricePoint
        {
            InstrumentId = instrument.Id,
            DateTime = dp.DateTime,
            Price = dp.Price,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        // Bulk upsert (handles duplicates) - matches Python: concat + drop_duplicates
        // Note: EF Core will handle unique constraint violations, but we'll use a more explicit approach
        foreach (var pricePoint in pricePoints)
        {
            var existing = await dbContext.PricePoints
                .FirstOrDefaultAsync(
                    p => p.InstrumentId == pricePoint.InstrumentId && p.DateTime == pricePoint.DateTime,
                    cancellationToken);

            if (existing != null)
            {
                // Update existing (matches Python: keep="last")
                existing.Price = pricePoint.Price;
                existing.CreatedAt = pricePoint.CreatedAt;
            }
            else
            {
                await dbContext.PricePoints.AddAsync(pricePoint, cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var latestNewDate = validData.Max(dp => dp.DateTime);

        _logger.LogInformation("Updated {Symbol}: {Count} new records added. Latest date: {Date}",
            instrument.Symbol, pricePoints.Count, latestNewDate);

        // Log update result
        await LogUpdateResultAsync(dbContext, instrument, latestNewDate, pricePoints.Count, UpdateStatus.Success, null, cancellationToken);
    }

    private async Task LogUpdateResultAsync(
        ApplicationDbContext dbContext,
        Instrument instrument,
        DateTime? lastUpdateDate,
        int recordsFetched,
        UpdateStatus status,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var log = new DataUpdateLog
        {
            InstrumentId = instrument.Id,
            LastUpdateDate = lastUpdateDate,
            RecordsFetched = recordsFetched,
            Status = status,
            ErrorMessage = errorMessage,
            CreatedAt = DateTime.UtcNow
        };

        await dbContext.DataUpdateLogs.AddAsync(log, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}