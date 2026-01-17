namespace MarketAnalytics.Domain.ValueObjects;

/// <summary>
/// Value object representing a single price data point (DateTime, Price).
/// Used for calculations and data processing operations.
/// </summary>
public record PriceDataPoint(DateTime DateTime, decimal Price);