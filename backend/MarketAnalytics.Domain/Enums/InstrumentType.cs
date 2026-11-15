namespace MarketAnalytics.Domain.Enums;

/// <summary>
/// Represents the type of financial instrument.
/// </summary>
public enum InstrumentType
{
    StockIndex = 0,
    MutualFund = 1,
    Forex = 2,
    Commodity = 3,
    /// <summary>A company's shares, priced per share (adjusted for splits and bonus issues when stored).</summary>
    Stock = 4
}