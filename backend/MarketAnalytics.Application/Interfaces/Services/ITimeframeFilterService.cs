using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Interfaces.Services;

/// <summary>
/// Service interface for filtering price data by timeframe.
/// Matches Python logic: filters data from last date backwards using date offsets.
/// </summary>
public interface ITimeframeFilterService
{
    /// <summary>
    /// Filters price data points by the specified timeframe.
    /// </summary>
    /// <param name="dataPoints">Price data points to filter</param>
    /// <param name="timeframe">Timeframe filter (Day, Week, Month, Quarter, HalfYear, Year, Ytd, All)</param>
    /// <returns>Filtered data points</returns>
    IEnumerable<PriceDataPoint> FilterByTimeframe(IEnumerable<PriceDataPoint> dataPoints, Timeframe timeframe);
}