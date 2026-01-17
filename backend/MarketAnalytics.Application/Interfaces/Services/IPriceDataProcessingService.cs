using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Domain.Enums;

namespace MarketAnalytics.Application.Interfaces.Services;

/// <summary>
/// Service interface for processing instrument price data.
/// Orchestrates the full pipeline: load → clean → resample → filter → compute indicators.
/// </summary>
public interface IPriceDataProcessingService
{
    /// <summary>
    /// Processes price data for a specific instrument with the given interval and timeframe.
    /// Returns processed data with all computed indicators.
    /// </summary>
    Task<IEnumerable<PriceDataDto>> ProcessInstrumentDataAsync(
        string instrumentSymbol, 
        Interval interval, 
        Timeframe timeframe,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets summary data for all active instruments.
    /// Returns latest values with indicators for cross-instrument comparison.
    /// </summary>
    Task<IEnumerable<SummaryDto>> GetSummaryAsync(
        Interval interval, 
        Timeframe timeframe,
        CancellationToken cancellationToken = default);
}