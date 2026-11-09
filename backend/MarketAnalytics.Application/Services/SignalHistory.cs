using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// The indicator series the signal rules read, computed once per price history so the latest figures, the steady
/// signal, the backtests and the alerts don't each compute them again.
/// </summary>
public record IndicatorSeries(
    List<PriceDataPoint> Prices,
    Dictionary<DateTime, decimal?> Sma50,
    Dictionary<DateTime, decimal?> Sma200,
    Dictionary<DateTime, decimal?> Rsi,
    Dictionary<DateTime, decimal?> Macd,
    Dictionary<DateTime, decimal?> MacdSignal,
    Dictionary<DateTime, decimal?> MacdHistogram,
    Dictionary<DateTime, decimal?> UpperBand,
    Dictionary<DateTime, decimal?> LowerBand)
{
    public static IndicatorSeries Compute(IIndicatorCalculationService indicators, IEnumerable<PriceDataPoint> prices)
    {
        var sorted = prices.OrderBy(p => p.DateTime).ToList();
        var (macd, signal, histogram) = indicators.ComputeMacd(sorted);
        var (upper, lower) = indicators.ComputeBollingerBands(sorted);
        return new IndicatorSeries(
            sorted,
            indicators.ComputeSma(sorted, SignalRules.MediumTrendPeriod),
            indicators.ComputeSma(sorted, SignalRules.LongTrendPeriod),
            indicators.ComputeRsi(sorted, SignalRules.RsiPeriod),
            macd, signal, histogram, upper, lower);
    }
}

/// <summary>
/// Each day's signals recomputed from prices known that day, shared by the backtests and the alerts.
/// </summary>
public static class SignalHistory
{
    /// <summary>
    /// The technical signal's score for each day from the first day all five rules can be evaluated.
    /// Uses the same rules as the live signal; each day's score only depends on prices up to that day.
    /// </summary>
    public static List<(DateTime Date, decimal Price, int Score)> TechnicalScores(IIndicatorCalculationService indicators, IReadOnlyList<PriceDataPoint> prices) =>
        TechnicalScores(IndicatorSeries.Compute(indicators, prices));

    public static List<(DateTime Date, decimal Price, int Score)> TechnicalScores(IndicatorSeries series)
    {
        var scores = new List<(DateTime, decimal, int)>();
        foreach (var point in series.Prices)
        {
            var date = point.DateTime;
            if (series.Sma200[date] is not decimal longAverage || series.Sma50[date] is not decimal mediumAverage
                || series.Rsi[date] is not decimal rsiValue || series.MacdHistogram[date] is not decimal macdHistogram
                || series.UpperBand[date] is not decimal upperBand || series.LowerBand[date] is not decimal lowerBand)
                continue;

            var percentB = SignalRules.PercentB(point.Price, upperBand, lowerBand);
            var score = SignalRules.Trend(point.Price, longAverage)
                + SignalRules.Trend(point.Price, mediumAverage)
                + SignalRules.Momentum(macdHistogram)
                + SignalRules.Rsi(rsiValue)
                + (percentB is decimal b ? SignalRules.Bollinger(b) : 0);

            scores.Add((date, point.Price, score));
        }
        return scores;
    }
}
