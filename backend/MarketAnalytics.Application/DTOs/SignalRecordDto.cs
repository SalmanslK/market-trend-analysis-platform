namespace MarketAnalytics.Application.DTOs;

/// <summary>
/// The forward record: signals saved as they were shown, scored against what prices did afterwards.
/// </summary>
public class SignalRecordDto
{
    /// <summary>The first saved reading; null before anything has been recorded.</summary>
    public DateTime? Since { get; set; }
    /// <summary>Readings saved (one per instrument per price date).</summary>
    public int Readings { get; set; }
    /// <summary>Readings old enough to have a full outcome.</summary>
    public int Scored { get; set; }
    /// <summary>The outcome is the move over this many trading days, from the next day's close.</summary>
    public int HorizonDays { get; set; }
    public List<SignalRecordGroupDto> Groups { get; set; } = new();
}

public class SignalRecordGroupDto
{
    /// <summary>"Any day", "Technical signal (steady)", "Combined signal" or "Stronger than cash".</summary>
    public string Signal { get; set; } = string.Empty;
    /// <summary>Buy, Hold, Sell, "Ahead of cash", "Behind cash" or "All readings".</summary>
    public string Reading { get; set; } = string.Empty;
    /// <summary>Scored readings in this group.</summary>
    public int Count { get; set; }
    /// <summary>Average move afterwards, in percent; null with no scored readings.</summary>
    public decimal? AverageMove { get; set; }
    /// <summary>Share of readings followed by a rise, in percent.</summary>
    public decimal? ShareUp { get; set; }
}
