using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Service for processing instrument price data.
/// Orchestrates the full pipeline: load → clean → resample → filter → compute indicators.
/// </summary>
public class PriceDataProcessingService : IPriceDataProcessingService
{
    private readonly IPricePointRepository _pricePointRepository;
    private readonly IInstrumentRepository _instrumentRepository;
    private readonly IResamplingService _resamplingService;
    private readonly ITimeframeFilterService _timeframeFilterService;
    private readonly IIndicatorCalculationService _indicatorCalculationService;

    public PriceDataProcessingService(
        IPricePointRepository pricePointRepository,
        IInstrumentRepository instrumentRepository,
        IResamplingService resamplingService,
        ITimeframeFilterService timeframeFilterService,
        IIndicatorCalculationService indicatorCalculationService)
    {
        _pricePointRepository = pricePointRepository;
        _instrumentRepository = instrumentRepository;
        _resamplingService = resamplingService;
        _timeframeFilterService = timeframeFilterService;
        _indicatorCalculationService = indicatorCalculationService;
    }

    /// <summary>
    /// Processes price data for a specific instrument with the given interval and timeframe.
    /// Returns processed data with all computed indicators.
    /// </summary>
    public async Task<IEnumerable<PriceDataDto>> ProcessInstrumentDataAsync(
        string instrumentSymbol,
        Interval interval,
        Timeframe timeframe,
        CancellationToken cancellationToken = default)
    {
        // 1. Get instrument
        var instrument = await _instrumentRepository.GetBySymbolAsync(instrumentSymbol, cancellationToken);
        if (instrument == null)
        {
            throw new ArgumentException($"Instrument with symbol '{instrumentSymbol}' not found.", nameof(instrumentSymbol));
        }

        // 2. Load raw price data from database
        var rawPricePoints = await _pricePointRepository.GetByInstrumentIdAsync(
            instrument.Id, 
            cancellationToken: cancellationToken);

        if (!rawPricePoints.Any())
        {
            return Enumerable.Empty<PriceDataDto>();
        }

        // 3. Convert to value objects
        var priceDataPoints = rawPricePoints
            .OrderBy(p => p.DateTime)
            .Select(p => new PriceDataPoint(p.DateTime, p.Price))
            .ToList();

        // 4. Clean data (already done in database - positive prices, valid dates, no duplicates)
        // Skip explicit cleaning as database constraints ensure integrity

        // 5. Resample by interval
        var resampledData = _resamplingService.ResamplePriceData(priceDataPoints, interval);
        var resampledList = resampledData.ToList();

        // 6. Filter by timeframe
        var filteredData = _timeframeFilterService.FilterByTimeframe(resampledList, timeframe);
        var filteredList = filteredData.ToList();

        if (!filteredList.Any())
        {
            return Enumerable.Empty<PriceDataDto>();
        }

        // 7. Compute indicators
        var sma20 = _indicatorCalculationService.ComputeSma20(filteredList);
        var (upperBand, lowerBand) = _indicatorCalculationService.ComputeBollingerBands(filteredList);
        var intervalMAs = _indicatorCalculationService.ComputeIntervalSpecificMovingAverages(filteredList, interval);

        // 8. Combine into DTOs
        var result = filteredList.Select(dp =>
        {
            var dto = new PriceDataDto
            {
                DateTime = dp.DateTime,
                Price = dp.Price,
                Sma20 = sma20.GetValueOrDefault(dp.DateTime),
                UpperBand = upperBand.GetValueOrDefault(dp.DateTime),
                LowerBand = lowerBand.GetValueOrDefault(dp.DateTime)
            };

            // Add interval-specific MAs
            if (intervalMAs.TryGetValue("MA_4W", out var ma4W))
            {
                dto.Ma4W = ma4W.GetValueOrDefault(dp.DateTime);
            }

            if (intervalMAs.TryGetValue("MA_6M", out var ma6M))
            {
                dto.Ma6M = ma6M.GetValueOrDefault(dp.DateTime);
            }

            if (intervalMAs.TryGetValue("MA_12M", out var ma12M))
            {
                dto.Ma12M = ma12M.GetValueOrDefault(dp.DateTime);
            }

            return dto;
        }).ToList();

        return result;
    }

    /// <summary>
    /// Gets summary data for all active instruments.
    /// Returns latest values with indicators for cross-instrument comparison.
    /// </summary>
    public async Task<IEnumerable<SummaryDto>> GetSummaryAsync(
        Interval interval,
        Timeframe timeframe,
        CancellationToken cancellationToken = default)
    {
        var activeInstruments = await _instrumentRepository.GetActiveInstrumentsAsync(cancellationToken);
        var summaryList = new List<SummaryDto>();

        foreach (var instrument in activeInstruments)
        {
            try
            {
                var processedData = await ProcessInstrumentDataAsync(
                    instrument.Symbol, 
                    interval, 
                    timeframe, 
                    cancellationToken);

                var dataList = processedData.ToList();
                if (dataList.Any())
                {
                    var latest = dataList.Last();
                    summaryList.Add(new SummaryDto
                    {
                        Instrument = instrument.Symbol,
                        Date = latest.DateTime,
                        Price = latest.Price,
                        Sma20 = latest.Sma20,
                        UpperBand = latest.UpperBand,
                        LowerBand = latest.LowerBand
                    });
                }
            }
            catch
            {
                // Skip instruments that fail (log in production)
                continue;
            }
        }

        return summaryList.OrderBy(s => s.Date);
    }
}