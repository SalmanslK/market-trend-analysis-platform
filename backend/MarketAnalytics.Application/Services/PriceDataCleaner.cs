using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Read-time cleaning of stored prices. Stored data is left untouched.
/// </summary>
public static class PriceDataCleaner
{
    // A spike must move this far from the previous price...
    private const decimal SpikeThreshold = 0.15m;
    // ...and the next price must come back to within this distance of the previous one
    private const decimal RevertTolerance = 0.05m;

    /// <summary>
    /// Removes non-positive prices and single-point spikes: a price that jumps more than 15% away from its
    /// neighbours while the next price returns to within 5% of the previous one (e.g. NAV 83.40 -> 53.85 -> 83.36).
    /// Such one-day reversals are data errors in the sources. Lasting moves such as a fund's NAV dropping
    /// after a dividend payout are kept, because the price does not come back.
    /// </summary>
    public static List<PriceDataPoint> RemoveSpikes(IEnumerable<PriceDataPoint> prices)
    {
        var sorted = prices.Where(p => p.Price > 0).OrderBy(p => p.DateTime).ToList();
        if (sorted.Count < 3)
            return sorted;

        var result = new List<PriceDataPoint>(sorted.Count) { sorted[0] };
        for (int i = 1; i < sorted.Count - 1; i++)
        {
            var previous = result[^1].Price;
            var current = sorted[i].Price;
            var next = sorted[i + 1].Price;

            var jump = current / previous - 1;
            var back = next / current - 1;
            var isSpike = Math.Abs(jump) > SpikeThreshold
                && Math.Abs(back) > SpikeThreshold
                && Math.Sign(jump) != Math.Sign(back)
                && Math.Abs(next / previous - 1) < RevertTolerance;

            if (!isSpike)
                result.Add(sorted[i]);
        }
        result.Add(sorted[^1]);

        return result;
    }
}
