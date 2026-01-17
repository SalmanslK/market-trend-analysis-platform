using MarketAnalytics.Domain.Enums;

namespace MarketAnalytics.Domain.ValueObjects;

/// <summary>
/// Value object representing a resampled price data point.
/// Includes the interval used for resampling.
/// </summary>
public record ResampledDataPoint(DateTime DateTime, decimal Price, Interval Interval);