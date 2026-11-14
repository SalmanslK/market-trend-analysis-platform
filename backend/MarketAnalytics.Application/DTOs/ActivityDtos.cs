namespace MarketAnalytics.Application.DTOs;

/// <summary>How busy trading in an index has been lately, from daily shares traded.</summary>
public class VolumeSummaryDto
{
    public DateTime LatestDate { get; set; }
    public long LatestShares { get; set; }
    /// <summary>Average shares a day over the last 20 trading days (about a month).</summary>
    public long Average20 { get; set; }
    /// <summary>Average shares a day over the last 100 trading days (about five months).</summary>
    public long Average100 { get; set; }
    /// <summary>The last month's average as a percentage of the usual (100-day) level.</summary>
    public decimal RecentVersusUsualPercent { get; set; }
    /// <summary>The 20-day average is at least the 100-day one: "confirming" in the volume research.</summary>
    public bool Busier { get; set; }
}

/// <summary>A fund's size and the money investors put in or took out (size change beyond the price change).</summary>
public class FundSizeSummaryDto
{
    /// <summary>The month the source reports the size for.</summary>
    public DateTime Month { get; set; }
    public decimal Rupees { get; set; }
    /// <summary>Money in (+) or out (-) in the latest reported month, as a percentage of the fund's size.</summary>
    public decimal? Flow1MPercent { get; set; }
    /// <summary>Money in or out over the 12 reported months, as a percentage of the size 12 months earlier.</summary>
    public decimal? Flow12MPercent { get; set; }
}

public class ActivityPointDto
{
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
}

/// <summary>The activity history for a chart: daily shares traded, or monthly fund size in rupees.</summary>
public class ActivitySeriesDto
{
    public string Symbol { get; set; } = string.Empty;
    /// <summary>"Volume" or "FundSize"; empty when the instrument has neither.</summary>
    public string Kind { get; set; } = string.Empty;
    public List<ActivityPointDto> Points { get; set; } = new();
}
