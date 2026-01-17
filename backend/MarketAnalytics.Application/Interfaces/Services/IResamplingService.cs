using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Interfaces.Services;

/// <summary>
/// Service interface for resampling price data by interval.
/// Matches Python logic: resample(rule).last() - takes last value per period.
/// </summary>
public interface IResamplingService
{
    /// <summary>
    /// Resamples price data points by the specified interval.
    /// For each interval period, takes the last price value.
    /// </summary>
    /// <param name="dataPoints">Original price data points</param>
    /// <param name="interval">Resampling interval (Daily, Weekly, Monthly, Quarterly, Yearly)</param>
    /// <returns>Resampled data points</returns>
    IEnumerable<PriceDataPoint> ResamplePriceData(IEnumerable<PriceDataPoint> dataPoints, Interval interval);
}