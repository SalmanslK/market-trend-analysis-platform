namespace MarketAnalytics.Domain.Entities;

/// <summary>
/// A fund's size (assets under management) as reported for one month. Funds have no trading volume; the change in
/// size beyond what the price change explains is the money investors put in or took out.
/// </summary>
public class FundAssets
{
    public int Id { get; set; }

    public int InstrumentId { get; set; }

    /// <summary>The month the source reports the size for (first day of the month, UTC).</summary>
    public DateTime Month { get; set; }

    /// <summary>Fund size in rupees.</summary>
    public decimal Rupees { get; set; }

    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    public Instrument Instrument { get; set; } = null!;
}
