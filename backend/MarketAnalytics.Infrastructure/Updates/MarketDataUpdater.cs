using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Infrastructure.Data;
using MarketAnalytics.Infrastructure.DataFetchers;

namespace MarketAnalytics.Infrastructure.Updates;

/// <summary>What one update found for an instrument.</summary>
public record InstrumentUpdateResult(string Symbol, int NewPrices, DateTime? LatestDate, int NewPayouts, string? Error);

/// <summary>
/// Fetches new prices and fund payouts for every active instrument. Used by the Worker on its schedule and by the
/// API when someone asks for a refresh, so both update the data the same way.
/// Matches Python logic: update_instrument_data() - fetches incremental data for each instrument.
/// </summary>
public class MarketDataUpdater
{
    private readonly ApplicationDbContext _dbContext;
    private readonly InstrumentDataFetcherFactory _fetcherFactory;
    private readonly FundPayoutFetcher _payoutFetcher;
    private readonly ILogger<MarketDataUpdater> _logger;

    public MarketDataUpdater(
        ApplicationDbContext dbContext,
        InstrumentDataFetcherFactory fetcherFactory,
        FundPayoutFetcher payoutFetcher,
        ILogger<MarketDataUpdater> logger)
    {
        _dbContext = dbContext;
        _fetcherFactory = fetcherFactory;
        _payoutFetcher = payoutFetcher;
        _logger = logger;
    }

    public async Task<List<InstrumentUpdateResult>> UpdateAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting data update for all instruments");

        var activeInstruments = await _dbContext.Instruments
            .Where(i => i.IsActive)
            .OrderBy(i => i.Symbol)
            .ToListAsync(cancellationToken);

        var results = new List<InstrumentUpdateResult>();
        foreach (var instrument in activeInstruments)
        {
            int newPrices = 0;
            DateTime? latest = null;
            string? error = null;
            try
            {
                (newPrices, latest) = await UpdatePricesAsync(instrument, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error updating data for instrument {Symbol}", instrument.Symbol);
                error = ex.Message;

                // Drop the failed instrument's pending changes so they aren't retried by the log save below
                _dbContext.ChangeTracker.Clear();

                // Log the error to DataUpdateLog; a failure here must not stop the remaining instruments
                try
                {
                    await LogUpdateResultAsync(instrument, null, 0, UpdateStatus.Failed, ex.Message, cancellationToken);
                }
                catch (Exception logEx) when (logEx is not OperationCanceledException)
                {
                    _logger.LogError(logEx, "Error writing update log for instrument {Symbol}", instrument.Symbol);
                    _dbContext.ChangeTracker.Clear();
                }
            }

            var newPayouts = 0;
            try
            {
                newPayouts = await UpdateFundPayoutsAsync(instrument, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Prices stay usable without payouts; they are retried on the next update
                _logger.LogError(ex, "Error updating payouts for {Symbol}", instrument.Symbol);
                _dbContext.ChangeTracker.Clear();
            }

            results.Add(new InstrumentUpdateResult(instrument.Symbol, newPrices, latest, newPayouts, error));
        }

        _logger.LogInformation("Completed data update");
        return results;
    }

    /// <summary>
    /// Sources that take a "days" parameter serve as much history as asked for.
    /// </summary>
    public static string FullHistoryUrl(string url) =>
        Regex.Replace(url, @"([?&]days=)\d+", "${1}10000");

    /// <summary>Stores new prices and corrects changed ones. Returns the number of new dates and the latest date.</summary>
    private async Task<(int NewPrices, DateTime? Latest)> UpdatePricesAsync(Instrument instrument, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing instrument: {Symbol}", instrument.Symbol);

        // Get latest date from database (matches Python: max_date = old_df["datetime"].max())
        var latestDate = await _dbContext.PricePoints
            .Where(p => p.InstrumentId == instrument.Id)
            .OrderByDescending(p => p.DateTime)
            .Select(p => (DateTime?)p.DateTime)
            .FirstOrDefaultAsync(cancellationToken);

        var fetcher = _fetcherFactory.GetFetcher(instrument.DataSourceType);

        // Fetch new data since latest date (matches Python: new_df = new_df[new_df["datetime"] > max_date]).
        // A new instrument asks for its whole history instead of the usual last year
        var url = latestDate == null ? FullHistoryUrl(instrument.DataSourceUrl) : instrument.DataSourceUrl;
        var newDataList = (await fetcher.FetchHistoricalDataAsync(url, instrument.Symbol, latestDate)).ToList();

        if (newDataList.Count == 0)
        {
            _logger.LogInformation("No new data found for {Symbol}", instrument.Symbol);
            await LogUpdateResultAsync(instrument, latestDate, 0, UpdateStatus.Success, null, cancellationToken);
            return (0, latestDate);
        }

        // Clean and validate data (positive prices, valid dates)
        var validData = newDataList
            .Where(dp => dp.Price > 0 && dp.DateTime >= new DateTime(1990, 1, 1) && dp.DateTime <= DateTime.UtcNow)
            .OrderBy(dp => dp.DateTime)
            .ToList();

        if (validData.Count == 0)
        {
            _logger.LogWarning("No valid data after cleaning for {Symbol}", instrument.Symbol);
            await LogUpdateResultAsync(instrument, latestDate, 0, UpdateStatus.Failed, "No valid data after cleaning", cancellationToken);
            return (0, latestDate);
        }

        var pricePoints = validData.Select(dp => new PricePoint
        {
            InstrumentId = instrument.Id,
            DateTime = dp.DateTime,
            Price = dp.Price,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        // Upsert (matches Python: concat + drop_duplicates, keep="last"). Existing rows in the fetched range are
        // loaded in one query instead of one per price
        var from = pricePoints.Min(p => p.DateTime);
        var to = pricePoints.Max(p => p.DateTime);
        var existingByDate = await _dbContext.PricePoints
            .Where(p => p.InstrumentId == instrument.Id && p.DateTime >= from && p.DateTime <= to)
            .ToDictionaryAsync(p => p.DateTime, cancellationToken);

        var added = 0;
        foreach (var pricePoint in pricePoints.GroupBy(p => p.DateTime).Select(g => g.Last()))
        {
            if (existingByDate.TryGetValue(pricePoint.DateTime, out var existing))
            {
                // Update existing (matches Python: keep="last")
                existing.Price = pricePoint.Price;
                existing.CreatedAt = pricePoint.CreatedAt;
            }
            else
            {
                await _dbContext.PricePoints.AddAsync(pricePoint, cancellationToken);
                added++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var latestNewDate = validData.Max(dp => dp.DateTime);
        _logger.LogInformation("Updated {Symbol}: {Count} new records added. Latest date: {Date}", instrument.Symbol, added, latestNewDate);

        await LogUpdateResultAsync(instrument, latestNewDate, pricePoints.Count, UpdateStatus.Success, null, cancellationToken);
        return (added, latestNewDate);
    }

    /// <summary>
    /// Stores payouts not seen before for funds whose NAVs come from a nav-history URL. The source lists
    /// the full payout history each time, so nothing is ever deleted. Returns the number stored.
    /// </summary>
    private async Task<int> UpdateFundPayoutsAsync(Instrument instrument, CancellationToken cancellationToken)
    {
        var url = FundPayoutFetcher.PayoutUrlFor(instrument.DataSourceUrl);
        if (url == null)
            return 0;

        var fetched = await _payoutFetcher.FetchAsync(url, cancellationToken);
        var known = (await _dbContext.FundPayouts
            .Where(p => p.InstrumentId == instrument.Id)
            .Select(p => p.PayoutDate)
            .ToListAsync(cancellationToken)).ToHashSet();

        var added = fetched
            .GroupBy(p => p.Date)
            .Where(g => !known.Contains(g.Key))
            .Select(g => new FundPayout { InstrumentId = instrument.Id, PayoutDate = g.Key, AmountPerUnit = g.First().AmountPerUnit })
            .ToList();

        if (added.Count == 0)
            return 0;

        await _dbContext.FundPayouts.AddRangeAsync(added, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Stored {Count} new payouts for {Symbol}", added.Count, instrument.Symbol);
        return added.Count;
    }

    private async Task LogUpdateResultAsync(
        Instrument instrument,
        DateTime? lastUpdateDate,
        int recordsFetched,
        UpdateStatus status,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        await _dbContext.DataUpdateLogs.AddAsync(new DataUpdateLog
        {
            InstrumentId = instrument.Id,
            LastUpdateDate = lastUpdateDate,
            RecordsFetched = recordsFetched,
            Status = status,
            ErrorMessage = errorMessage,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
