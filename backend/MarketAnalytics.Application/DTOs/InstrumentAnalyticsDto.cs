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

    public TechnicalSignalDto Signal { get; set; } = new();
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
