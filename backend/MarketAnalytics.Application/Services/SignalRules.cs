namespace MarketAnalytics.Application.Services;

/// <summary>
/// The technical signal's scoring rules, shared by the live signal and the backtest so that the backtest
/// measures exactly what the app shows. Each rule returns +1 (bullish), -1 (bearish) or 0 (neutral).
/// </summary>
public static class SignalRules
{
    public const int LongTrendPeriod = 200;
    public const int MediumTrendPeriod = 50;
    public const int RsiPeriod = 14;

    /// <summary>Price above (+1) or below (-1) a moving average.</summary>
    public static int Trend(decimal price, decimal average) => price >= average ? 1 : -1;

    /// <summary>MACD above (+1) or below (-1) its signal line.</summary>
    public static int Momentum(decimal macdHistogram) => macdHistogram >= 0 ? 1 : -1;

    /// <summary>Oversold below 30 (+1), overbought above 70 (-1), otherwise neutral.</summary>
    public static int Rsi(decimal rsi) => rsi switch
    {
        < 30 => 1,
        > 70 => -1,
        _ => 0,
    };

    /// <summary>Below the lower band (+1), above the upper band (-1), otherwise neutral.</summary>
    public static int Bollinger(decimal percentB) => percentB switch
    {
        < 0 => 1,
        > 1 => -1,
        _ => 0,
    };

    /// <summary>Golden cross: the 50-period average above (+1) or below (-1) the 200-period average.</summary>
    public static int GoldenCross(decimal mediumAverage, decimal longAverage) => mediumAverage >= longAverage ? 1 : -1;

    /// <summary>Where the price sits between the bands: 0 = lower band, 1 = upper band. Null if the bands have no width.</summary>
    public static decimal? PercentB(decimal price, decimal upperBand, decimal lowerBand) =>
        upperBand > lowerBand ? (price - lowerBand) / (upperBand - lowerBand) : null;

    public static string ActionFor(int score) => score switch
    {
        >= 3 => "StrongBuy",
        >= 1 => "Buy",
        <= -3 => "StrongSell",
        <= -1 => "Sell",
        _ => "Hold",
    };
}
