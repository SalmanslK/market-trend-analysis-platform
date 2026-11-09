namespace MarketAnalytics.Application.DTOs;

/// <summary>
/// One change of a signal or trend from one day's close to the next.
/// </summary>
public class SignalAlertDto
{
    public DateTime Date { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Combined, Technical, News or Trend.</summary>
    public string Signal { get; set; } = string.Empty;
    /// <summary>A Buy/Sell action (StrongBuy ... StrongSell) or, for Trend, Up or Down.</summary>
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    /// <summary>The combined signal turned to Buy or Sell, or the trend changed.</summary>
    public bool Important { get; set; }
}
