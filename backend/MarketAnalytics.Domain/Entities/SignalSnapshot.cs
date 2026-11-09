namespace MarketAnalytics.Domain.Entities;

/// <summary>
/// The signals as the app showed them for one instrument on one price date, saved at the time. Backtests can be
/// overfit to the past without meaning to; this record can't, because each reading is stored before what happened
/// next is known. Comparing it with later prices gives a true forward test.
/// </summary>
public class SignalSnapshot
{
    public int Id { get; set; }

    public int InstrumentId { get; set; }

    /// <summary>The date of the latest price the signals were computed from.</summary>
    public DateTime Date { get; set; }

    /// <summary>That price (payout-adjusted at the time it was recorded; outcomes use the current adjusted series).</summary>
    public decimal Price { get; set; }

    /// <summary>Daily 5-vote technical signal: StrongBuy, Buy, Hold, Sell or StrongSell.</summary>
    public string DailyAction { get; set; } = string.Empty;
    public int DailyScore { get; set; }

    /// <summary>Steady technical signal (20-day average), -100 to +100; null with too little history.</summary>
    public string? SteadyAction { get; set; }
    public int? SteadyScore { get; set; }

    /// <summary>News signal, -100 to +100, and the stories behind it.</summary>
    public string NewsAction { get; set; } = string.Empty;
    public int NewsScore { get; set; }
    public int NewsArticles { get; set; }

    /// <summary>The combined signal built on the steady technical signal (the daily one when steady is missing).</summary>
    public string CombinedAction { get; set; } = string.Empty;

    /// <summary>Long-term trend: Up, Down or Unknown.</summary>
    public string Trend { get; set; } = string.Empty;

    /// <summary>The stronger-than-cash check; null when it doesn't apply.</summary>
    public bool? AheadOfCash { get; set; }

    /// <summary>When this reading was last saved. It is updated until the next price arrives (news can still change).</summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public Instrument Instrument { get; set; } = null!;
}
