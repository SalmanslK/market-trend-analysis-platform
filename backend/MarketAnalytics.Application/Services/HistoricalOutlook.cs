using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// What followed in the past: for every earlier day in the same trend state as today (Up, Up but weakening, Down,
/// Down but recovering; the same golden-cross rule as the rest of the app), the return over the next month, six
/// months and year, against the same for every day. A base rate from history, not a forecast: the brief may only
/// talk about the future in these terms.
/// </summary>
public static class HistoricalOutlook
{
    public static readonly (string Label, int Days)[] Horizons = [("1 month", 21), ("6 months", 126), ("1 year", 252)];

    /// <summary>Fewer past days in the same state than this, and the brief is told the history is too thin.</summary>
    public const int MinSameStateDays = 60;

    public static OutlookDto? Compute(string symbol, IReadOnlyList<PriceDataPoint> prices, IIndicatorCalculationService indicators)
    {
        var series = IndicatorSeries.Compute(indicators, prices);
        var days = series.Prices;
        if (days.Count < SignalRules.LongTrendPeriod + Horizons[0].Days)
            return null;

        var states = days.Select(p => StateOn(p, series)).ToList();
        var today = states[^1];
        if (today == null)
            return null;

        var result = new OutlookDto
        {
            Symbol = symbol,
            State = today,
            HistoryFrom = days[0].DateTime,
            HistoryTo = days[^1].DateTime,
            // Separate spells in this state: how many independent episodes the numbers come from
            Spells = states.Where((s, i) => s == today && (i == 0 || states[i - 1] != today)).Count(),
        };

        foreach (var (label, horizon) in Horizons)
        {
            var same = new List<double>();
            var all = new List<double>();
            for (int i = 0; i + horizon < days.Count; i++)
            {
                if (states[i] == null)
                    continue;
                var change = (double)(days[i + horizon].Price / days[i].Price - 1) * 100;
                all.Add(change);
                if (states[i] == today)
                    same.Add(change);
            }
            result.Horizons.Add(new OutlookHorizonDto
            {
                Label = label,
                TradingDays = horizon,
                SameState = Stats(same),
                AllDays = Stats(all),
            });
        }
        return result;
    }

    private static string? StateOn(PriceDataPoint day, IndicatorSeries series)
    {
        if (series.Sma50[day.DateTime] is not decimal fast || series.Sma200[day.DateTime] is not decimal slow)
            return null;
        var direction = SignalRules.GoldenCross(fast, slow) > 0 ? "Up" : "Down";
        return SignalRules.TrendPhase(direction, day.Price, fast, slow) switch
        {
            "Recovering" => "Down but recovering",
            "Weakening" => "Up but weakening",
            _ => direction,
        };
    }

    private static OutlookStatsDto Stats(List<double> changes)
    {
        if (changes.Count == 0)
            return new OutlookStatsDto();
        var sorted = changes.OrderBy(c => c).ToList();
        double At(double q) => sorted[(int)Math.Round(q * (sorted.Count - 1))];
        return new OutlookStatsDto
        {
            Days = sorted.Count,
            ShareHigherPercent = Math.Round((decimal)sorted.Count(c => c > 0) / sorted.Count * 100, 0),
            MedianPercent = Math.Round((decimal)At(0.5), 1),
            Low10Percent = Math.Round((decimal)At(0.1), 1),
            High90Percent = Math.Round((decimal)At(0.9), 1),
        };
    }
}
