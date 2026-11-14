using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Company share prices come as traded, so a split, bonus issue or large payout looks like a crash (Lucky Cement's
/// 5-for-1 split in 2025: -80% in a day). PSX limits a share's daily move to 5-10%, so a bigger move between
/// consecutive trading days, from a level that was steady to a new level that stays steady, is one of those, and
/// earlier prices are rescaled to run through it. The source's older prices also carry glitches (a price that jumps
/// for a few days and comes back, or Rs 0.30 for a Rs 39 share); those fail the steadiness test and are left alone.
/// Only for company shares: funds have their payouts adjusted from the recorded list, and indices, gold and
/// currencies never jump so. Shared by the price update and the stock-rankings research.
/// </summary>
public static class StockSplitAdjuster
{
    public const decimal Down = 0.85m, Up = 1.18m;

    /// <summary>Penny shares can move a whole rupee (often more than 10%) in a day, so they are left alone.</summary>
    public const decimal MinPrice = 20m;

    /// <summary>A longer gap (a suspension, or the 2008 market freeze) can hide a real fall, so it is left alone.</summary>
    public static readonly TimeSpan MaxGap = TimeSpan.FromDays(7);

    /// <summary>Days looked at on each side of a jump, and how far from their middle value the jump's ends may be.</summary>
    private const int Window = 5;
    private const decimal Steady = 0.10m;

    /// <summary>Prices sorted by date with one-day spikes removed and rescaled across splits; the jumps rescaled.</summary>
    public static (List<PriceDataPoint> Prices, int Adjustments) Adjust(IEnumerable<PriceDataPoint> prices)
    {
        var sorted = PriceDataCleaner.RemoveSpikes(prices);
        var isSplit = new bool[sorted.Count];
        for (int i = 1; i < sorted.Count; i++)
        {
            var (before, after) = (sorted[i - 1], sorted[i]);
            var ratio = after.Price / before.Price;
            isSplit[i] = (ratio < Down || ratio > Up)
                && before.Price >= MinPrice
                && after.DateTime - before.DateTime <= MaxGap
                && IsSteady(sorted, Math.Max(0, i - Window), i, before.Price)
                && IsSteady(sorted, i, Math.Min(sorted.Count, i + Window), after.Price);
        }

        var result = new PriceDataPoint[sorted.Count];
        var factor = 1m;
        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            result[i] = factor == 1m ? sorted[i] : new PriceDataPoint(sorted[i].DateTime, Math.Round(sorted[i].Price * factor, 6));
            if (i > 0 && isSplit[i])
                factor *= sorted[i].Price / sorted[i - 1].Price;
        }
        return (result.ToList(), isSplit.Count(s => s));
    }

    /// <summary>True when the price sits within 10% of the middle of prices[from..to).</summary>
    private static bool IsSteady(List<PriceDataPoint> prices, int from, int to, decimal price)
    {
        var window = prices.Skip(from).Take(to - from).Select(p => p.Price).OrderBy(p => p).ToList();
        if (window.Count == 0)
            return false;
        var middle = window[window.Count / 2];
        return Math.Abs(price / middle - 1) <= Steady;
    }
}
