using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Interfaces.Services;

/// <summary>
/// Service interface for per-instrument performance, risk and technical-signal analytics.
/// </summary>
public interface IMarketAnalyticsService
{
    /// <summary>
    /// Gets analytics for one instrument, or null if the instrument doesn't exist or has no prices.
    /// </summary>
    Task<InstrumentAnalyticsDto?> GetInstrumentAnalyticsAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets analytics for all active instruments that have prices.
    /// </summary>
    Task<IEnumerable<InstrumentAnalyticsDto>> GetAllAnalyticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes analytics from daily prices (exposed for testing).
    /// </summary>
    InstrumentAnalyticsDto ComputeAnalytics(IReadOnlyList<PriceDataPoint> dailyPrices);
}
