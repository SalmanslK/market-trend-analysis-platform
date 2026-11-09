using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<PriceDataProcessingService> _logger;
    private readonly IFundPayoutRepository? _fundPayoutRepository;

    public PriceDataProcessingService(
        IPricePointRepository pricePointRepository,
        IInstrumentRepository instrumentRepository,
        IResamplingService resamplingService,
        ITimeframeFilterService timeframeFilterService,
        IIndicatorCalculationService indicatorCalculationService,
        ILogger<PriceDataProcessingService> logger,
        IFundPayoutRepository? fundPayoutRepository = null)
    {
        _fundPayoutRepository = fundPayoutRepository;
        _pricePointRepository = pricePointRepository;
        _instrumentRepository = instrumentRepository;
        _resamplingService = resamplingService;
        _timeframeFilterService = timeframeFilterService;
        _indicatorCalculationService = indicatorCalculationService;
        _logger = logger;
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

        // 2-4. Load and clean: dates and duplicates are enforced by the database, but the sources occasionally
        //      contain one-day price spikes, which are dropped; fund NAVs are adjusted for payouts so a payout
        //      doesn't show as a fall
        var priceDataPoints = await PriceHistory.LoadDailyAsync(
            _pricePointRepository, _fundPayoutRepository, instrument.Id, cancellationToken: cancellationToken);

        if (priceDataPoints.Count == 0)
        {
            return Enumerable.Empty<PriceDataDto>();
        }

        // 5. Resample by interval
        var resampledData = _resamplingService.ResamplePriceData(priceDataPoints, interval);
        var resampledList = resampledData.ToList();

        // 6. Compute indicators on the full resampled history, so the bands and moving averages
        //    cover the whole timeframe window instead of starting 20 periods into it
        var sma20 = _indicatorCalculationService.ComputeSma20(resampledList);
        var (upperBand, lowerBand) = _indicatorCalculationService.ComputeBollingerBands(resampledList);
        var intervalMAs = _indicatorCalculationService.ComputeIntervalSpecificMovingAverages(resampledList, interval);
        var rsi = _indicatorCalculationService.ComputeRsi(resampledList);
        var (macd, macdSignal, macdHistogram) = _indicatorCalculationService.ComputeMacd(resampledList);

        // 7. Filter by timeframe
        var filteredData = _timeframeFilterService.FilterByTimeframe(resampledList, timeframe);
        var filteredList = filteredData.ToList();

        if (!filteredList.Any())
        {
            return Enumerable.Empty<PriceDataDto>();
        }

        // 8. Combine into DTOs
        var result = filteredList.Select(dp =>
        {
            var dto = new PriceDataDto
            {
                DateTime = dp.DateTime,
                Price = dp.Price,
                Sma20 = sma20.GetValueOrDefault(dp.DateTime),
                UpperBand = upperBand.GetValueOrDefault(dp.DateTime),
                LowerBand = lowerBand.GetValueOrDefault(dp.DateTime),
                Rsi14 = rsi.GetValueOrDefault(dp.DateTime),
                Macd = macd.GetValueOrDefault(dp.DateTime),
                MacdSignal = macdSignal.GetValueOrDefault(dp.DateTime),
                MacdHistogram = macdHistogram.GetValueOrDefault(dp.DateTime)
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
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Skip instruments that fail so one bad instrument doesn't break the whole summary
                _logger.LogError(ex, "Error building summary for instrument {Symbol}", instrument.Symbol);
            }
        }

        return summaryList.OrderBy(s => s.Date);
    }
}