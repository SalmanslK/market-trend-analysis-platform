using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Loads daily prices the way every analysis reads them: spikes removed and, for funds, adjusted for payouts.
/// </summary>
public static class PriceHistory
{
    public static async Task<List<PriceDataPoint>> LoadDailyAsync(
        IPricePointRepository prices,
        IFundPayoutRepository? payouts,
        int instrumentId,
        DateTime? fromUtc = null,
        CancellationToken cancellationToken = default)
    {
        var points = await prices.GetByInstrumentIdAsync(instrumentId, fromUtc, cancellationToken: cancellationToken);
        var cleaned = PriceDataCleaner.RemoveSpikes(points.Select(p => new PriceDataPoint(p.DateTime, p.Price)));
        if (payouts == null)
            return cleaned;

        return PayoutAdjuster.Adjust(cleaned, await payouts.GetByInstrumentIdAsync(instrumentId, cancellationToken));
    }
}
