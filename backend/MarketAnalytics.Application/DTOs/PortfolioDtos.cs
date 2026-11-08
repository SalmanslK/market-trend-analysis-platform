namespace MarketAnalytics.Application.DTOs;

/// <summary>
/// A mix of instruments to test: weights in percent (they need not add to 100; the rest is cash), how often to
/// bring it back to those weights, and whether each part sits in cash while its own long-term trend is down.
/// </summary>
public class PortfolioRequestDto
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, decimal> Weights { get; set; } = new();
    /// <summary>Never, Yearly, HalfYearly or Quarterly.</summary>
    public string Rebalance { get; set; } = "Yearly";
    public bool TrendFilter { get; set; }
}

/// <summary>
/// How a mix would have done. Percentages are percent values (2.5 means +2.5%).
/// </summary>
public class PortfolioResultDto
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, decimal> Weights { get; set; } = new();
    public string Rebalance { get; set; } = "Yearly";
    public bool TrendFilter { get; set; }
    public decimal CostPercent { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Years { get; set; }

    public decimal TotalReturn { get; set; }
    public decimal AnnualReturn { get; set; }
    public decimal MaxDrawdown { get; set; }
    /// <summary>Yearly volatility of daily changes.</summary>
    public decimal Volatility { get; set; }
    /// <summary>Return above cash (Meezan Cash Fund over the same dates) per unit of volatility.</summary>
    public decimal? Sharpe { get; set; }
    /// <summary>Calendar-year returns, oldest first; the first and last years can be partial.</summary>
    public List<YearReturnDto> YearlyReturns { get; set; } = new();
    public int Trades { get; set; }

    /// <summary>Growth of 100, one point a week.</summary>
    public List<PortfolioPointDto> EquityCurve { get; set; } = new();
}

public class YearReturnDto
{
    public int Year { get; set; }
    public decimal Return { get; set; }
}

public class PortfolioPointDto
{
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
}

/// <summary>
/// Several mixes tested over the same dates, so they can be compared fairly.
/// </summary>
public class PortfolioComparisonDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal CostPercent { get; set; }
    /// <summary>Yearly return of the cash fund over the same dates, when it has data.</summary>
    public decimal? CashAnnualReturn { get; set; }
    public List<PortfolioResultDto> Results { get; set; } = new();
    /// <summary>Instruments that can be mixed, with the first date each has data.</summary>
    public List<PortfolioInstrumentDto> Instruments { get; set; } = new();
}

public class PortfolioInstrumentDto
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime FirstDate { get; set; }
    public bool IsCash { get; set; }
    /// <summary>"Cash fund" or "Income fund" for a low-risk fund.</summary>
    public string? LowRiskKind { get; set; }
}
