namespace MarketAnalytics.Domain.Entities;

/// <summary>
/// Represents a single price point (datetime, price) for an instrument.
/// This is the primary time-series data storage entity.
/// </summary>
public class PricePoint
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to the Instrument.
    /// </summary>
    public int InstrumentId { get; set; }
    
    /// <summary>
    /// Date and time of the price point (stored in UTC).
    /// </summary>
    public DateTime DateTime { get; set; }
    
    /// <summary>
    /// Price value (supports high precision: 18,6).
    /// </summary>
    public decimal Price { get; set; }
    
    /// <summary>
    /// Timestamp when this price point was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public virtual Instrument Instrument { get; set; } = null!;
}