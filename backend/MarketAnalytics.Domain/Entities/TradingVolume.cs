namespace MarketAnalytics.Domain.Entities;

/// <summary>
/// How many shares changed hands on one day in an index's companies. Heavy trading on a move suggests many
/// investors are behind it; a move on thin trading is easier to reverse.
/// </summary>
public class TradingVolume
{
    public int Id { get; set; }

    public int InstrumentId { get; set; }

    /// <summary>Trading day (UTC date, matching the price dates).</summary>
    public DateTime Date { get; set; }

    /// <summary>Shares traded that day.</summary>
    public long Shares { get; set; }

    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    public Instrument Instrument { get; set; } = null!;
}
