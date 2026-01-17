using MarketAnalytics.Domain.Enums;

namespace MarketAnalytics.Domain.Entities;

/// <summary>
/// Represents a financial instrument (e.g., KSE100, MIF, USDPKR, GOLDINT).
/// </summary>
public class Instrument
{
    public int Id { get; set; }
    
    /// <summary>
    /// Unique symbol identifier (e.g., "KSE100", "MIF", "USDPKR").
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the instrument.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of instrument (Stock Index, Mutual Fund, Forex, Commodity).
    /// </summary>
    public InstrumentType Type { get; set; }
    
    /// <summary>
    /// URL endpoint for fetching data (API endpoint or scraping URL).
    /// </summary>
    public string DataSourceUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of data source (JSON API or HTML Scraper).
    /// </summary>
    public DataSourceType DataSourceType { get; set; }
    
    /// <summary>
    /// Whether this instrument is active for data updates and queries.
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Timestamp when the instrument was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Timestamp when the instrument was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<PricePoint> PricePoints { get; set; } = new List<PricePoint>();
    public virtual ICollection<DataUpdateLog> UpdateLogs { get; set; } = new List<DataUpdateLog>();
}