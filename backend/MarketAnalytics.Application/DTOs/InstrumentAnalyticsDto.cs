using MarketAnalytics.Domain.Enums;

namespace MarketAnalytics.Application.DTOs;

/// <summary>
/// Performance, risk and technical-signal snapshot for one instrument, computed from its daily prices.
/// Percentages are expressed as percent values (e.g. 2.5 means +2.5%).
/// </summary>
public class InstrumentAnalyticsDto
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public InstrumentType Type { get; set; }
    /// <summary>A cash (money market) or income fund: its price only creeps up, so Buy/Sell signals don't apply to it.</summary>
    public bool IsCash { get; set; }
    /// <summary>"Cash fund" or "Income fund" when IsCash.</summary>
    public string? LowRiskKind { get; set; }
    public DateTime LastDate { get; set; }
    public decimal LastPrice { get; set; }
    public int DataPoints { get; set; }

    // Performance: % change from the last price on or before each lookback date
    public decimal? Change1D { get; set; }
    public decimal? Change1W { get; set; }
    public decimal? Change1M { get; set; }
    public decimal? Change3M { get; set; }
    public decimal? ChangeYtd { get; set; }
    public decimal? Change1Y { get; set; }

    // Risk over the last year
    public decimal? High52W { get; set; }
    public decimal? Low52W { get; set; }
    /// <summary>Where the last price sits in the 52-week range: 0 = at the low, 100 = at the high.</summary>
    public decimal? RangePosition52W { get; set; }
    /// <summary>Annualized standard deviation of daily returns, in percent.</summary>
    public decimal? Volatility1Y { get; set; }
    /// <summary>Largest peak-to-trough fall in the last year, in percent (negative).</summary>
    public decimal? MaxDrawdown1Y { get; set; }

    // Latest technical indicator values (daily)
    public decimal? Rsi14 { get; set; }
    public decimal? Macd { get; set; }
    public decimal? MacdSignal { get; set; }
    public decimal? MacdHistogram { get; set; }
    public decimal? Sma50 { get; set; }
    public decimal? Sma200 { get; set; }
    /// <summary>Bollinger %B: 0 = at the lower band, 1 = at the upper band.</summary>
    public decimal? PercentB { get; set; }

    /// <summary>The headline label shown for the instrument: the direction of its long-term trend.</summary>
    public LongTermTrendDto LongTermTrend { get; set; } = new();

    /// <summary>The Buy/Hold/Sell signal and what each of its indicators shows right now.</summary>
    public TechnicalSignalDto Signal { get; set; } = new();

    /// <summary>Buy/Sell from the last 7 days of news about this instrument.</summary>
    public ScaledSignalDto NewsSignal { get; set; } = new();

    /// <summary>Buy or Sell only when the technical and news signals clearly agree, otherwise Hold.</summary>
    public ScaledSignalDto CombinedSignal { get; set; } = new();

    /// <summary>
    /// The steady technical signal: the daily 5-vote score averaged over the last 20 trading days, -100 to +100.
    /// It changes far less often than the daily signal and did better in testing; the pages show it by default.
    /// Null when the history is too short for all five rules (200+ prices); the pages then show the daily signal.
    /// </summary>
    public ScaledSignalDto? SteadySignal { get; set; }

    /// <summary>The combined signal using the steady technical signal instead of the daily one; null with SteadySignal.</summary>
    public ScaledSignalDto? SteadyCombinedSignal { get; set; }

    /// <summary>All the standard metrics that can be calculated from closing prices.</summary>
    public List<MetricDto> Metrics { get; set; } = new();

    /// <summary>Fund cash payouts, newest first. Prices and returns above are adjusted for them.</summary>
    public List<PayoutDto> Payouts { get; set; } = new();

    /// <summary>Payouts over the last 12 months as a percentage of the latest price; null when there were none.</summary>
    public decimal? PayoutYield1Y { get; set; }

    /// <summary>
    /// The "stronger than cash" check (see StrongerThanCash). Null for cash and income funds, currencies, and
    /// instruments with less than 12 months of history.
    /// </summary>
    public CashTestDto? StrongerThanCash { get; set; }
}

/// <summary>
/// Whether an instrument beat cash over the 12 months before this month's check. The rule holds the instrument only
/// while it does, and cash otherwise; it is re-checked on the first trading day of each month.
/// </summary>
public class CashTestDto
{
    public bool AheadOfCash { get; set; }
    /// <summary>The instrument's return over the 12 months to the check, in percent (payouts included).</summary>
    public decimal Return12M { get; set; }
    /// <summary>The Meezan Cash Fund's return over the same 12 months, in percent.</summary>
    public decimal CashReturn12M { get; set; }
    /// <summary>The first trading day of this month, when the check was made.</summary>
    public DateTime CheckedOn { get; set; }
    /// <summary>The check since which the answer has been the same; null if it hasn't changed in the stored history.</summary>
    public DateTime? Since { get; set; }
}

/// <summary>
/// A signal scored from -100 to +100.
/// </summary>
public class ScaledSignalDto
{
    /// <summary>StrongBuy, Buy, Hold, Sell or StrongSell.</summary>
    public string Action { get; set; } = "Hold";
    public int Score { get; set; }
    /// <summary>Direct news articles behind it; below 3, news isn't used.</summary>
    public int Articles { get; set; }
    /// <summary>How clearly the news agrees, 0-100.</summary>
    public int NewsConfidence { get; set; }
    /// <summary>Other instruments whose news reading is identical, because they are linked to the same stories.</summary>
    public List<string> SharedWith { get; set; } = new();
}

/// <summary>
/// One technical or risk metric at the latest price, with its usual reading in plain language.
/// </summary>
/// <summary>
/// One fund payout: the date the NAV reflected it and the rupees paid per unit.
/// </summary>
public class PayoutDto
{
    public DateTime Date { get; set; }
    public decimal AmountPerUnit { get; set; }
}

public class MetricDto
{
    public string Group { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    /// <summary>price, percent or number.</summary>
    public string Unit { get; set; } = "number";
    /// <summary>Positive, Negative or Neutral; empty when the metric has no good or bad direction.</summary>
    public string Reading { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Long-term trend by the golden cross rule: Up while the 50-period average is above the 200-period average.
/// It describes the trend so far and reacts slowly; it isn't a prediction or a recommendation.
/// </summary>
public class LongTermTrendDto
{
    /// <summary>Up, Down, or Unknown when there are fewer than 200 prices.</summary>
    public string Direction { get; set; } = "Unknown";
    /// <summary>The day the trend last changed to this direction; null if it hasn't changed in the stored history.</summary>
    public DateTime? Since { get; set; }
    /// <summary>
    /// Recovering when the trend is Down but the price is back above both averages; Weakening when it is Up but the
    /// price has fallen below both. Null otherwise. The direction itself only changes when the averages cross.
    /// </summary>
    public string? Phase { get; set; }
}

/// <summary>
/// Rule-based technical signal. Each rule adds +1 (bullish), -1 (bearish) or 0 (neutral) to the score.
/// This summarizes indicators only; it is not financial advice.
/// </summary>
public class TechnicalSignalDto
{
    /// <summary>StrongBuy, Buy, Hold, Sell or StrongSell.</summary>
    public string Action { get; set; } = "Hold";
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public List<SignalReasonDto> Reasons { get; set; } = new();
}

public class SignalReasonDto
{
    public string Indicator { get; set; } = string.Empty;
    /// <summary>+1 bullish, -1 bearish, 0 neutral.</summary>
    public int Impact { get; set; }
    public string Detail { get; set; } = string.Empty;
}
