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

    /// <summary>
    /// The averages cross weeks after a turn, so the trend can lag the price: "Recovering" when the trend is Down
    /// but the price is above both averages, "Weakening" when it is Up but the price is below both, otherwise null.
    /// </summary>
    public static string? TrendPhase(string direction, decimal price, decimal mediumAverage, decimal longAverage) => direction switch
    {
        "Down" when price > mediumAverage && price > longAverage => "Recovering",
        "Up" when price < mediumAverage && price < longAverage => "Weakening",
        _ => null,
    };

    /// <summary>Where the price sits between the bands: 0 = lower band, 1 = upper band. Null if the bands have no width.</summary>
    public static decimal? PercentB(decimal price, decimal upperBand, decimal lowerBand) =>
        upperBand > lowerBand ? (price - lowerBand) / (upperBand - lowerBand) : null;

    /// <summary>Days of news counted in the news signal.</summary>
    public const int NewsWindowDays = 7;

    /// <summary>With fewer direct articles than this, the news signal stays on Hold.</summary>
    public const int MinNewsArticles = 3;

    /// <summary>The news signal's score, -100 to +100: the news mood, or 0 without enough articles.</summary>
    public static double NewsScore(int moodScore, int articles) => articles >= MinNewsArticles ? moodScore : 0;

    /// <summary>
    /// The technical signal's score on the same -100 to +100 scale as the news and combined signals.
    /// </summary>
    public static double TechnicalScaledScore(int technicalScore, int technicalMax) =>
        technicalMax > 0 ? (double)technicalScore / technicalMax * 100 : 0;

    /// <summary>
    /// The combined signal's score, -100 to +100: the average of technical and news when both clearly point the
    /// same way, otherwise 0 (Hold). News swings about twice as much as technical, so simply averaging them let
    /// news decide; requiring agreement keeps either one from overruling the other.
    /// </summary>
    public static double CombinedScore(int technicalScore, int technicalMax, int moodScore, int articles)
    {
        if (articles < MinNewsArticles)
            return 0;

        var technical = TechnicalScaledScore(technicalScore, technicalMax);
        var bothClear = Math.Abs(technical) >= BuySellThreshold && Math.Abs(moodScore) >= BuySellThreshold;
        return bothClear && Math.Sign(technical) == Math.Sign(moodScore) ? (technical + moodScore) / 2 : 0;
    }

    private const double BuySellThreshold = 20;

    /// <summary>Action for a score from -100 to +100, used by the news and combined signals.</summary>
    public static string ActionForScaledScore(double score) => score switch
    {
        >= 60 => "StrongBuy",
        >= 20 => "Buy",
        <= -60 => "StrongSell",
        <= -20 => "Sell",
        _ => "Hold",
    };

    /// <summary>+1 for any Buy, -1 for any Sell, 0 for Hold.</summary>
    public static int PositionForScaledScore(double score) => score >= 20 ? 1 : score <= -20 ? -1 : 0;

    public static string ActionFor(int score) => score switch
    {
        >= 3 => "StrongBuy",
        >= 1 => "Buy",
        <= -3 => "StrongSell",
        <= -1 => "Sell",
        _ => "Hold",
    };
}
