using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Adjusts a fund's NAVs for its cash payouts, so a payout counts as return instead of a price fall
/// (a "total return" series). Prices before each payout are scaled down by the payout's share of the
/// NAV, the way stock prices are adjusted for dividends; the latest price stays the real NAV.
/// </summary>
public static class PayoutAdjuster
{
    // The source lists the payout date, but the NAV drop can be published up to a few days later
    public const int ExDateWindowDays = 5;

    // Each trading day after the listed date counts as missing the payout amount by this share more,
    // so an ordinary market fall a few days later doesn't win over a matching drop on the listed date
    private const decimal DelayPenalty = 0.25m;

    /// <summary>
    /// The trading day whose NAV reflects each payout: of the days from the listed date to
    /// <see cref="ExDateWindowDays"/> after, the one whose fall is closest to the payout, preferring earlier days.
    /// Payouts outside the price history are left out.
    /// </summary>
    public static List<(FundPayout Payout, int Index)> MatchExDates(IReadOnlyList<PriceDataPoint> prices, IEnumerable<FundPayout> payouts)
    {
        var result = new List<(FundPayout, int)>();
        foreach (var payout in payouts.Where(p => p.AmountPerUnit > 0))
        {
            var from = payout.PayoutDate.Date;
            var to = from.AddDays(ExDateWindowDays);
            var best = -1;
            var bestMiss = decimal.MaxValue;
            var delay = 0;
            // Funds such as Meezan Rozana Amdani pay daily, so find the window by binary search instead of scanning
            for (int i = Math.Max(1, FirstOnOrAfter(prices, from)); i < prices.Count; i++)
            {
                var date = prices[i].DateTime.Date;
                if (date > to) break;
                var fall = prices[i - 1].Price - prices[i].Price;
                var miss = Math.Abs(fall - payout.AmountPerUnit) + DelayPenalty * payout.AmountPerUnit * delay++;
                if (miss < bestMiss)
                {
                    best = i;
                    bestMiss = miss;
                }
            }
            if (best > 0)
                result.Add((payout, best));
        }
        return result;
    }

    private static int FirstOnOrAfter(IReadOnlyList<PriceDataPoint> prices, DateTime date)
    {
        int low = 0, high = prices.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (prices[middle].DateTime.Date < date) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    /// <summary>
    /// Returns the prices adjusted for the payouts. Prices must be sorted oldest first.
    /// </summary>
    public static List<PriceDataPoint> Adjust(IReadOnlyList<PriceDataPoint> prices, IEnumerable<FundPayout> payouts)
    {
        var matches = MatchExDates(prices, payouts);
        if (matches.Count == 0)
            return prices.ToList();

        // Each day's factor is the product of (1 - payout / NAV before payout) over all later payouts
        var factorFrom = new decimal[prices.Count + 1];
        Array.Fill(factorFrom, 1m);
        foreach (var (payout, index) in matches)
        {
            var before = prices[index - 1].Price;
            var factor = 1 - payout.AmountPerUnit / before;
            if (factor > 0 && factor < 1)
                factorFrom[index - 1] *= factor;
        }

        var result = new PriceDataPoint[prices.Count];
        var cumulative = 1m;
        for (int i = prices.Count - 1; i >= 0; i--)
        {
            cumulative *= factorFrom[i];
            result[i] = new PriceDataPoint(prices[i].DateTime, prices[i].Price * cumulative);
        }
        return result.ToList();
    }
}
