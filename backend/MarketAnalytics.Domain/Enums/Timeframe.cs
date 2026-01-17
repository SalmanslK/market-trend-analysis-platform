namespace MarketAnalytics.Domain.Enums;

/// <summary>
/// Represents the timeframe filter for price data.
/// Used to filter data from the last date backwards.
/// </summary>
public enum Timeframe
{
    Day = 0,
    Week = 1,
    Month = 2,
    Quarter = 3,
    HalfYear = 4,
    Year = 5,
    Ytd = 6,
    All = 7
}