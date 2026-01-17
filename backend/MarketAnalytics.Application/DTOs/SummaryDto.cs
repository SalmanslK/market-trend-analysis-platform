namespace MarketAnalytics.Application.DTOs;

/// <summary>
/// Data Transfer Object for cross-instrument summary.
/// Contains latest values for all instruments with indicators.
/// </summary>
public class SummaryDto
{
    public string Instrument { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Price { get; set; }
    public decimal? Sma20 { get; set; }
    public decimal? UpperBand { get; set; }
    public decimal? LowerBand { get; set; }
}