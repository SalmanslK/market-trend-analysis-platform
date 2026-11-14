using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Company share prices come as traded, so a split, bonus issue or large payout looks like a crash (Lucky Cement's
/// 5-for-1 split in 2025: -80% in a day). PSX limits a share's daily move to 5-10%, so a bigger move between
/// consecutive trading days is one of those, and earlier prices are rescaled to run through it. Only for company
/// shares: funds have their payouts adjusted from the recorded list, and indices, gold and currencies never jump so.
/// Shared by the price update and the stock-rankings research, so both see the same history.
/// </summary>
public static class StockSplitAdjuster
{
    public const decimal Down = 0.85m, Up = 1.18m;

    /// <summary>Penny shares can move a whole rupee (often more than 10%) in a day, so they are left alone.</summary>
    public const decimal MinPrice = 20m;

    /// <summary>A longer gap (a suspension, or the 2008 market freeze) can hide a real fall, so it is left alone.</summary>
    public static readonly TimeSpan MaxGap = TimeSpan.FromDays(7);

    /// <summary>Prices sorted by date, rescaled across splits; the number of jumps rescaled.</summary>
    public static (List<PriceDataPoint> Prices, int Adjustments) Adjust(IEnumerable<PriceDataPoint> prices)
    {
        var sorted = prices.Where(p => p.Price > 0).OrderBy(p => p.DateTime).ToList();
        var result = new PriceDataPoint[sorted.Count];
        var factor = 1m;
        var adjustments = 0;
        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            result[i] = factor == 1m ? sorted[i] : new PriceDataPoint(sorted[i].DateTime, Math.Round(sorted[i].Price * factor, 6));
            if (i == 0)
                break;
            var (before, after) = (sorted[i - 1], sorted[i]);
            var ratio = after.Price / before.Price;
            if ((ratio < Down || ratio > Up) && before.Price >= MinPrice && after.DateTime - before.DateTime <= MaxGap)
            {
                factor *= ratio;
                adjustments++;
            }
        }
        return (result.ToList(), adjustments);
    }
}
