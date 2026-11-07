namespace MarketAnalytics.Domain.Entities;

/// <summary>
/// A cash payout (dividend) a mutual fund made per unit. The fund's NAV drops by about this amount
/// when the payout is made, so returns are only comparable once prices are adjusted for it.
/// </summary>
public class FundPayout
{
    public int Id { get; set; }

    public int InstrumentId { get; set; }

    /// <summary>Payout date as listed by the source. The NAV drop can show up a few days later.</summary>
    public DateTime PayoutDate { get; set; }

    /// <summary>Rupees paid per unit.</summary>
    public decimal AmountPerUnit { get; set; }

    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    public Instrument Instrument { get; set; } = null!;
}
