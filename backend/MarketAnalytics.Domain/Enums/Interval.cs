namespace MarketAnalytics.Domain.Enums;

/// <summary>
/// Represents the resampling interval for price data.
/// Matches Python resampling rules: Daily="D", Weekly="W", Monthly="M", Quarterly="Q", Yearly="Y"
/// </summary>
public enum Interval
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Yearly = 4
}