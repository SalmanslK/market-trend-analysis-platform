using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Computes per-instrument performance, risk and a rule-based technical signal from daily prices.
///
/// Signal rules (each adds +1 bullish, -1 bearish or 0 neutral):
/// - Long-term trend: price above/below its 200-period SMA
/// - Medium-term trend: price above/below its 50-period SMA
/// - Momentum: MACD histogram positive/negative (MACD above/below its signal line)
/// - RSI(14): below 30 oversold (+1), above 70 overbought (-1), otherwise neutral
/// - Bollinger %B: below the lower band (+1), above the upper band (-1), otherwise neutral
/// Score >= 3 is StrongBuy, >= 1 Buy, 0 Hold, &lt;= -1 Sell, &lt;= -3 StrongSell.
/// The trend rules follow the trend; the RSI and Bollinger rules are mean-reversion rules, so they can
/// disagree. That is intentional: the reasons list shows which way each indicator points.
/// </summary>
public class MarketAnalyticsService : IMarketAnalyticsService
{
    private const int MinReturnsForVolatility = 20;

    private readonly IInstrumentRepository _instrumentRepository;
    private readonly IPricePointRepository _pricePointRepository;
    private readonly IIndicatorCalculationService _indicators;
    private readonly ILogger<MarketAnalyticsService> _logger;

    public MarketAnalyticsService(
        IInstrumentRepository instrumentRepository,
        IPricePointRepository pricePointRepository,
        IIndicatorCalculationService indicators,
        ILogger<MarketAnalyticsService> logger)
    {
        _instrumentRepository = instrumentRepository;
        _pricePointRepository = pricePointRepository;
        _indicators = indicators;
        _logger = logger;
    }

    public async Task<InstrumentAnalyticsDto?> GetInstrumentAnalyticsAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var instrument = await _instrumentRepository.GetBySymbolAsync(symbol, cancellationToken);
        return instrument == null ? null : await BuildAsync(instrument, cancellationToken);
    }

    public async Task<IEnumerable<InstrumentAnalyticsDto>> GetAllAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<InstrumentAnalyticsDto>();

        foreach (var instrument in await _instrumentRepository.GetActiveInstrumentsAsync(cancellationToken))
        {
            try
            {
                var analytics = await BuildAsync(instrument, cancellationToken);
                if (analytics != null)
                    result.Add(analytics);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Skip instruments that fail so one bad instrument doesn't break the overview
                _logger.LogError(ex, "Error computing analytics for instrument {Symbol}", instrument.Symbol);
            }
        }

        return result;
    }

    private async Task<InstrumentAnalyticsDto?> BuildAsync(Instrument instrument, CancellationToken cancellationToken)
    {
        var pricePoints = await _pricePointRepository.GetByInstrumentIdAsync(instrument.Id, cancellationToken: cancellationToken);
        var dailyPrices = PriceDataCleaner.RemoveSpikes(pricePoints.Select(p => new PriceDataPoint(p.DateTime, p.Price)));

        if (dailyPrices.Count == 0)
            return null;

        var analytics = ComputeAnalytics(dailyPrices);
        analytics.Symbol = instrument.Symbol;
        analytics.Name = instrument.Name;
        analytics.Type = instrument.Type;
        return analytics;
    }

    public InstrumentAnalyticsDto ComputeAnalytics(IReadOnlyList<PriceDataPoint> dailyPrices)
    {
        var prices = dailyPrices.OrderBy(p => p.DateTime).ToList();
        var last = prices[^1];
        var lastDate = last.DateTime;

        var analytics = new InstrumentAnalyticsDto
        {
            LastDate = lastDate,
            LastPrice = last.Price,
            DataPoints = prices.Count,
            Change1D = prices.Count >= 2 ? PercentChange(prices[^2].Price, last.Price) : null,
            Change1W = ChangeSince(prices, lastDate.AddDays(-7)),
            Change1M = ChangeSince(prices, lastDate.AddMonths(-1)),
            Change3M = ChangeSince(prices, lastDate.AddMonths(-3)),
            // YTD is measured from the last price of the previous year
            ChangeYtd = ChangeSince(prices, new DateTime(lastDate.Year, 1, 1, 0, 0, 0, lastDate.Kind).AddDays(-1)),
            Change1Y = ChangeSince(prices, lastDate.AddYears(-1)),
        };

        ComputeRisk(prices, analytics);
        ComputeIndicators(prices, analytics);
        analytics.Signal = BuildSignal(analytics);

        return analytics;
    }

    private static void ComputeRisk(List<PriceDataPoint> prices, InstrumentAnalyticsDto analytics)
    {
        var yearAgo = analytics.LastDate.AddYears(-1);
        var window = prices.Where(p => p.DateTime > yearAgo).ToList();

        // Include the last price before the window so the first return in the year is counted
        var startIndex = prices.FindIndex(p => p.DateTime > yearAgo);
        var returnsWindow = startIndex > 0 ? prices.Skip(startIndex - 1).ToList() : window;

        analytics.High52W = window.Max(p => p.Price);
        analytics.Low52W = window.Min(p => p.Price);
        var range = analytics.High52W - analytics.Low52W;
        analytics.RangePosition52W = range > 0 ? (analytics.LastPrice - analytics.Low52W) / range * 100 : null;

        // Annualize with the observed number of returns per year, since calendars differ:
        // stock indices trade ~250 days a year, gold and forex snapshots run every day
        var logReturns = returnsWindow.Zip(returnsWindow.Skip(1), (a, b) => Math.Log((double)b.Price / (double)a.Price)).ToList();
        var spanDays = (returnsWindow[^1].DateTime - returnsWindow[0].DateTime).TotalDays;
        if (logReturns.Count >= MinReturnsForVolatility && spanDays > 0)
        {
            var mean = logReturns.Average();
            var variance = logReturns.Sum(r => Math.Pow(r - mean, 2)) / (logReturns.Count - 1);
            var returnsPerYear = logReturns.Count * 365.25 / spanDays;
            analytics.Volatility1Y = (decimal)(Math.Sqrt(variance) * Math.Sqrt(returnsPerYear) * 100);
        }

        decimal peak = 0, maxDrawdown = 0;
        foreach (var point in window)
        {
            peak = Math.Max(peak, point.Price);
            maxDrawdown = Math.Min(maxDrawdown, (point.Price / peak - 1) * 100);
        }
        analytics.MaxDrawdown1Y = maxDrawdown;
    }

    private void ComputeIndicators(List<PriceDataPoint> prices, InstrumentAnalyticsDto analytics)
    {
        var lastDate = analytics.LastDate;

        analytics.Rsi14 = _indicators.ComputeRsi(prices, SignalRules.RsiPeriod)[lastDate];

        var (macd, signal, histogram) = _indicators.ComputeMacd(prices);
        analytics.Macd = macd[lastDate];
        analytics.MacdSignal = signal[lastDate];
        analytics.MacdHistogram = histogram[lastDate];

        analytics.Sma50 = _indicators.ComputeSma(prices, SignalRules.MediumTrendPeriod)[lastDate];
        analytics.Sma200 = _indicators.ComputeSma(prices, SignalRules.LongTrendPeriod)[lastDate];

        var (upper, lower) = _indicators.ComputeBollingerBands(prices);
        if (upper[lastDate] is decimal upperBand && lower[lastDate] is decimal lowerBand)
            analytics.PercentB = SignalRules.PercentB(analytics.LastPrice, upperBand, lowerBand);
    }

    private static TechnicalSignalDto BuildSignal(InstrumentAnalyticsDto a)
    {
        var reasons = new List<SignalReasonDto>();

        if (a.Sma200 is decimal sma200)
        {
            var impact = SignalRules.Trend(a.LastPrice, sma200);
            var above = impact > 0;
            reasons.Add(new SignalReasonDto
            {
                Indicator = "Long-term trend",
                Impact = impact,
                Detail = $"Price is {Math.Abs(PercentChange(sma200, a.LastPrice)):0.0}% {(above ? "above" : "below")} its 200-period average ({(above ? "uptrend" : "downtrend")})",
            });
        }

        if (a.Sma50 is decimal sma50)
        {
            var impact = SignalRules.Trend(a.LastPrice, sma50);
            var above = impact > 0;
            reasons.Add(new SignalReasonDto
            {
                Indicator = "Medium-term trend",
                Impact = impact,
                Detail = $"Price is {Math.Abs(PercentChange(sma50, a.LastPrice)):0.0}% {(above ? "above" : "below")} its 50-period average",
            });
        }

        if (a.MacdHistogram is decimal histogram)
        {
            reasons.Add(new SignalReasonDto
            {
                Indicator = "Momentum (MACD)",
                Impact = SignalRules.Momentum(histogram),
                Detail = histogram >= 0
                    ? "MACD is above its signal line: momentum is rising"
                    : "MACD is below its signal line: momentum is fading",
            });
        }

        if (a.Rsi14 is decimal rsi)
        {
            var impact = SignalRules.Rsi(rsi);
            var detail = impact switch
            {
                > 0 => $"RSI {rsi:0} is below 30: oversold, a rebound is more likely",
                < 0 => $"RSI {rsi:0} is above 70: overbought, a pullback is more likely",
                _ => $"RSI {rsi:0} is between 30 and 70: neither overbought nor oversold",
            };
            reasons.Add(new SignalReasonDto { Indicator = "RSI (14)", Impact = impact, Detail = detail });
        }

        if (a.PercentB is decimal percentB)
        {
            var impact = SignalRules.Bollinger(percentB);
            var detail = impact switch
            {
                > 0 => "Price is below the lower Bollinger Band: stretched to the downside",
                < 0 => "Price is above the upper Bollinger Band: stretched to the upside",
                _ => $"Price is inside the Bollinger Bands ({percentB * 100:0}% of the way from lower to upper)",
            };
            reasons.Add(new SignalReasonDto { Indicator = "Bollinger Bands", Impact = impact, Detail = detail });
        }

        var score = reasons.Sum(r => r.Impact);
        return new TechnicalSignalDto
        {
            Score = score,
            MaxScore = reasons.Count,
            Reasons = reasons,
            Action = SignalRules.ActionFor(score),
        };
    }

    /// <summary>
    /// % change from the last price on or before <paramref name="referenceDate"/> to the latest price,
    /// or null if there is no price that far back.
    /// </summary>
    private static decimal? ChangeSince(List<PriceDataPoint> prices, DateTime referenceDate)
    {
        var reference = prices.LastOrDefault(p => p.DateTime <= referenceDate);
        return reference == null ? null : PercentChange(reference.Price, prices[^1].Price);
    }

    private static decimal PercentChange(decimal from, decimal to) => (to / from - 1) * 100;
}
