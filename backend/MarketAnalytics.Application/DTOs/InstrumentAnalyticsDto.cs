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

    /// <summary>All the standard metrics that can be calculated from closing prices.</summary>
    public List<MetricDto> Metrics { get; set; } = new();

    /// <summary>Fund cash payouts, newest first. Prices and returns above are adjusted for them.</summary>
    public List<PayoutDto> Payouts { get; set; } = new();

    /// <summary>Payouts over the last 12 months as a percentage of the latest price; null when there were none.</summary>
    public decimal? PayoutYield1Y { get; set; }
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
