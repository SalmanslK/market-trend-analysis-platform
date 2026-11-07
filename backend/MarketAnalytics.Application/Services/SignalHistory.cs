using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Each day's signals recomputed from prices known that day, shared by the backtests and the alerts.
/// </summary>
public static class SignalHistory
{
    /// <summary>
    /// The technical signal's score for each day from the first day all five rules can be evaluated.
    /// Uses the same rules as the live signal; each day's score only depends on prices up to that day.
    /// </summary>
    public static List<(DateTime Date, decimal Price, int Score)> TechnicalScores(IIndicatorCalculationService indicators, IReadOnlyList<PriceDataPoint> prices)
    {
        var sorted = prices.OrderBy(p => p.DateTime).ToList();
        var sma200 = indicators.ComputeSma(sorted, SignalRules.LongTrendPeriod);
        var sma50 = indicators.ComputeSma(sorted, SignalRules.MediumTrendPeriod);
        var rsi = indicators.ComputeRsi(sorted, SignalRules.RsiPeriod);
        var (_, _, histogram) = indicators.ComputeMacd(sorted);
        var (upper, lower) = indicators.ComputeBollingerBands(sorted);

        var scores = new List<(DateTime, decimal, int)>();
        foreach (var point in sorted)
        {
            var date = point.DateTime;
            if (sma200[date] is not decimal longAverage || sma50[date] is not decimal mediumAverage
                || rsi[date] is not decimal rsiValue || histogram[date] is not decimal macdHistogram
                || upper[date] is not decimal upperBand || lower[date] is not decimal lowerBand)
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
