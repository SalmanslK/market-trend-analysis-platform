using MarketAnalytics.Domain.Enums;

namespace MarketAnalytics.Application.DTOs;

/// <summary>
/// How the technical signal would have performed over an instrument's stored history.
/// Percentages are percent values (2.5 means +2.5%).
/// </summary>
public class BacktestResultDto
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public InstrumentType Type { get; set; }

    /// <summary>False when there is too little history for a meaningful test; only the identity fields are then set.</summary>
    public bool HasEnoughData { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Years { get; set; }
    public int TradingDays { get; set; }
    /// <summary>Assumed cost of each buy or sell, in percent of the amount traded.</summary>
    public decimal CostPercent { get; set; }
    /// <summary>Assumed yearly interest on cash, used only for StrategyWithInterest.</summary>
    public decimal CashInterestPercent { get; set; }

    /// <summary>Helped, LowerRisk or DidNotHelp: following the signal compared with simply holding.</summary>
    public string Verdict { get; set; } = "DidNotHelp";

    public PerformanceDto BuyAndHold { get; set; } = new();
    /// <summary>Invested after Buy signals, in cash after Sell signals, cash earning nothing.</summary>
    public PerformanceDto Strategy { get; set; } = new();
    /// <summary>The same strategy, with cash earning CashInterestPercent a year.</summary>
    public PerformanceDto StrategyWithInterest { get; set; } = new();

    /// <summary>% change in the period after each signal, compared with all days.</summary>
    public List<SignalOutcomeDto> Outcomes { get; set; } = new();

    /// <summary>Average 30-day change after Buy-type signals minus after Sell-type signals; positive means the signal told them apart.</summary>
    public decimal? BuyMinusSell30D { get; set; }
    public decimal? BuyMinusSell30DFirstHalf { get; set; }
    public decimal? BuyMinusSell30DSecondHalf { get; set; }

    /// <summary>Growth of 100 under each approach, thinned to at most a few hundred points for charting.</summary>
    public List<EquityPointDto> EquityCurve { get; set; } = new();
}

public class PerformanceDto
{
    public decimal TotalReturn { get; set; }
    public decimal AnnualReturn { get; set; }
    /// <summary>Largest peak-to-trough fall, in percent (negative).</summary>
    public decimal MaxDrawdown { get; set; }
    public int Trades { get; set; }
    /// <summary>Share of the time invested rather than in cash.</summary>
    public decimal TimeInvested { get; set; }
}

/// <summary>
/// What happened after days with a given signal. Days overlap heavily (a 30-day window starts on every day),
/// so Periods, the number of separate runs of the signal, is the better guide to how much evidence there is.
/// </summary>
public class SignalOutcomeDto
{
    /// <summary>StrongBuy, Buy, Hold, Sell, StrongSell or All.</summary>
    public string Action { get; set; } = string.Empty;
    public int Days { get; set; }
    public int Periods { get; set; }
    public List<HorizonOutcomeDto> Horizons { get; set; } = new();
}

public class HorizonOutcomeDto
{
    public int HorizonDays { get; set; }
    public int Samples { get; set; }
    public decimal AverageChange { get; set; }
    public decimal MedianChange { get; set; }
    /// <summary>Share of samples where the price was higher at the end, in percent.</summary>
    public decimal PercentPositive { get; set; }
}

public class EquityPointDto
{
    public DateTime Date { get; set; }
    public decimal BuyAndHold { get; set; }
    public decimal Strategy { get; set; }
}
