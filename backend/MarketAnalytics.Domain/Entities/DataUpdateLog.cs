using MarketAnalytics.Domain.Enums;

namespace MarketAnalytics.Domain.Entities;

/// <summary>
/// Represents a log entry for data update operations.
/// Tracks the results of background data fetches.
/// </summary>
public class DataUpdateLog
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to the Instrument.
    /// </summary>
    public int InstrumentId { get; set; }
    
    /// <summary>
    /// Last date that was successfully updated in the database.
    /// </summary>
    public DateTime? LastUpdateDate { get; set; }
    
    /// <summary>
    /// Number of records fetched in this update.
    /// </summary>
    public int RecordsFetched { get; set; }
    
    /// <summary>
    /// Status of the update operation.
    /// </summary>
    public UpdateStatus Status { get; set; }
    
    /// <summary>
    /// Error message if the update failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Timestamp when the update operation was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public virtual Instrument Instrument { get; set; } = null!;
}