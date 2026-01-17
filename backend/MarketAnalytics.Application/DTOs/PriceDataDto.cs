namespace MarketAnalytics.Application.DTOs;

/// <summary>
/// Data Transfer Object for price data with computed technical indicators.
/// Used for API responses containing processed price data.
/// </summary>
public class PriceDataDto
{
    public DateTime DateTime { get; set; }
    public decimal Price { get; set; }
    public decimal? Sma20 { get; set; }
    public decimal? UpperBand { get; set; }
    public decimal? LowerBand { get; set; }
    public decimal? Ma4W { get; set; }
    public decimal? Ma6M { get; set; }
    public decimal? Ma12M { get; set; }
}