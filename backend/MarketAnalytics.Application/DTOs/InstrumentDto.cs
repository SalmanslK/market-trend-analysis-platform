using MarketAnalytics.Domain.Enums;

namespace MarketAnalytics.Application.DTOs;

/// <summary>
/// Data Transfer Object for Instrument information.
/// </summary>
public class InstrumentDto
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public InstrumentType Type { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastUpdateDate { get; set; }
}