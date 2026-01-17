using MarketAnalytics.Domain.Enums;

namespace MarketAnalytics.Application.DTOs;

/// <summary>
/// Request DTO for price data queries.
/// </summary>
public class PriceDataRequest
{
    public Interval Interval { get; set; } = Interval.Daily;
    public Timeframe Timeframe { get; set; } = Timeframe.Quarter;
}