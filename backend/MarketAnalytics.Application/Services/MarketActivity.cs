using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Summaries of trading volume and fund flows. Descriptive only: the volume research (SETUP_CHECKLIST.md) found volume
/// makes the technical signal less bad but no rule built on it beat holding, so nothing here feeds a signal.
/// </summary>
public static class MarketActivity
{
    public const int RecentDays = 20;
    public const int UsualDays = 100;

    /// <summary>Null without 100 days of volume.</summary>
    public static VolumeSummaryDto? SummarizeVolume(IReadOnlyList<TradingVolume> volumes)
    {
        if (volumes.Count < UsualDays)
            return null;
        var recent = volumes.TakeLast(RecentDays).Average(v => (double)v.Shares);
        var usual = volumes.TakeLast(UsualDays).Average(v => (double)v.Shares);
        var latest = volumes[^1];
        return new VolumeSummaryDto
        {
            LatestDate = latest.Date,
            LatestShares = latest.Shares,
            Average20 = (long)Math.Round(recent),
            Average100 = (long)Math.Round(usual),
            RecentVersusUsualPercent = usual > 0 ? Math.Round((decimal)(recent / usual * 100), 1) : 0,
            Busier = recent >= usual,
        };
    }

    /// <summary>
    /// Null without sizes. Flows compare the size change with the change in the unit price (payouts not added back,
    /// since a payout leaves the fund too), each taken on the reported month's date or the last price before it.
    /// </summary>
    public static FundSizeSummaryDto? SummarizeFundSize(IReadOnlyList<FundAssets> sizes, IReadOnlyList<PriceDataPoint> unitPrices)
    {
        if (sizes.Count == 0)
            return null;
        var latest = sizes[^1];
        return new FundSizeSummaryDto
        {
            Month = latest.Month,
            Rupees = latest.Rupees,
            Flow1MPercent = Flow(sizes, sizes.Count - 2, unitPrices),
            Flow12MPercent = Flow(sizes, sizes.Count - 13, unitPrices),
        };
    }

    private static decimal? Flow(IReadOnlyList<FundAssets> sizes, int fromIndex, IReadOnlyList<PriceDataPoint> unitPrices)
    {
        if (fromIndex < 0)
            return null;
        var (from, to) = (sizes[fromIndex], sizes[^1]);
        // Months must be consecutive for the period to mean what it says
        if (to.Month != from.Month.AddMonths(sizes.Count - 1 - fromIndex) || from.Rupees <= 0)
            return null;
        var priceFrom = PriceOnOrBefore(unitPrices, from.Month);
        var priceTo = PriceOnOrBefore(unitPrices, to.Month);
        if (priceFrom is not > 0 || priceTo == null)
            return null;
        return Math.Round((to.Rupees / from.Rupees - priceTo.Value / priceFrom.Value) * 100, 1);
    }

    private static decimal? PriceOnOrBefore(IReadOnlyList<PriceDataPoint> prices, DateTime day)
    {
        decimal? found = null;
        foreach (var p in prices)
        {
            if (p.DateTime.Date > day.Date)
                break;
            found = p.Price;
        }
        return found;
    }
}
