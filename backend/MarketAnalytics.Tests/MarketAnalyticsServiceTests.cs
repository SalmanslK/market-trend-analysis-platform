using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketAnalytics.Tests;

public class MarketAnalyticsServiceTests
{
    // Repositories aren't used by ComputeAnalytics
    private readonly MarketAnalyticsService _service = new(null!, null!, new IndicatorCalculationService(), NullLogger<MarketAnalyticsService>.Instance);

    private static readonly DateTime Start = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<PriceDataPoint> Daily(IEnumerable<decimal> prices) =>
        prices.Select((p, i) => new PriceDataPoint(Start.AddDays(i), p)).ToList();

    [Fact]
    public void Returns_AreMeasuredFromThePriceOnOrBeforeEachLookbackDate()
    {
        // Price rises by 1 each day: 100, 101, ... 499 (400 days, last date 2026-02-04)
        var prices = Daily(Enumerable.Range(0, 400).Select(i => 100m + i));

        var a = _service.ComputeAnalytics(prices);

        Assert.Equal((499m / 498m - 1) * 100, a.Change1D);
        Assert.Equal((499m / 492m - 1) * 100, a.Change1W);
        // Last price of 2025 (day 364, price 464) is the YTD baseline
        Assert.Equal((499m / 464m - 1) * 100, a.ChangeYtd);
        // 2025-02-04 is day 34 (price 134)
        Assert.Equal((499m / 134m - 1) * 100, a.Change1Y);
    }

    [Fact]
    public void Returns_AreEmpty_WhenHistoryIsTooShort()
    {
        var a = _service.ComputeAnalytics(Daily(Enumerable.Range(0, 10).Select(i => 100m + i)));

        Assert.NotNull(a.Change1W);
        Assert.Null(a.Change1M);
        Assert.Null(a.Change1Y);
    }

    [Fact]
    public void MaxDrawdown_IsTheLargestFallFromAPeak()
    {
        var a = _service.ComputeAnalytics(Daily(new[] { 100m, 120m, 90m, 110m }));

        Assert.Equal(-25m, a.MaxDrawdown1Y);
        Assert.Equal(120m, a.High52W);
        Assert.Equal(90m, a.Low52W);
        Assert.Equal((110m - 90m) / 30m * 100, a.RangePosition52W);
    }

    [Fact]
    public void Volatility_IsZero_ForFlatPrices()
    {
        var a = _service.ComputeAnalytics(Daily(Enumerable.Repeat(100m, 60)));

        Assert.Equal(0m, a.Volatility1Y);
    }

    [Fact]
    public void Signal_SteadyUptrend_IsBuy_WithOverboughtRsiHoldingBackAStrongBuy()
    {
        // Linear rise: above the 50 and 200 averages (+2), MACD rising (+1), RSI 100 overbought (-1),
        // price inside the bands (0) -> score 2 -> Buy
        var a = _service.ComputeAnalytics(Daily(Enumerable.Range(0, 300).Select(i => 100m + i)));

        Assert.Equal(5, a.Signal.MaxScore);
        Assert.Equal(2, a.Signal.Score);
        Assert.Equal("Buy", a.Signal.Action);
        Assert.Contains(a.Signal.Reasons, r => r.Indicator == "RSI (14)" && r.Impact == -1);
    }

    [Fact]
    public void Signal_SteadyDowntrend_IsSell()
    {
        // Mirror image: below both averages (-2), MACD falling (-1), RSI 0 oversold (+1) -> score -2 -> Sell
        var a = _service.ComputeAnalytics(Daily(Enumerable.Range(0, 300).Select(i => 400m - i)));

        Assert.Equal(-2, a.Signal.Score);
        Assert.Equal("Sell", a.Signal.Action);
    }

    [Fact]
    public void LongTermTrend_IsUnknown_WithoutEnoughHistory()
    {
        var a = _service.ComputeAnalytics(Daily(Enumerable.Range(0, 150).Select(i => 100m + i)));

        Assert.Equal("Unknown", a.LongTermTrend.Direction);
        Assert.Null(a.LongTermTrend.Since);
    }

    [Fact]
    public void LongTermTrend_HasNoChangeDate_WhenItNeverChanged()
    {
        var a = _service.ComputeAnalytics(Daily(Enumerable.Range(0, 300).Select(i => 100m + i)));

        Assert.Equal("Up", a.LongTermTrend.Direction);
        Assert.Null(a.LongTermTrend.Since);
    }

    [Fact]
    public void LongTermTrend_ReportsTheDayTheAveragesCrossed()
    {
        // Rises for 300 days, then falls steeply until the 50-day average drops below the 200-day average
        var prices = Daily(Enumerable.Range(0, 300).Select(i => 100m + i)
            .Concat(Enumerable.Range(1, 150).Select(i => 399m - 2 * i)));
        var sma50 = new IndicatorCalculationService().ComputeSma(prices, 50);
        var sma200 = new IndicatorCalculationService().ComputeSma(prices, 200);
        var firstDown = prices.First(p => sma200[p.DateTime].HasValue && sma50[p.DateTime] < sma200[p.DateTime]).DateTime;

        var a = _service.ComputeAnalytics(prices);

        Assert.Equal("Down", a.LongTermTrend.Direction);
        Assert.Equal(firstDown, a.LongTermTrend.Since);
    }

    [Fact]
    public void SpikeFilter_DropsOneDayReversals_ButKeepsLastingDrops()
    {
        // 83.40 -> 53.85 -> 83.36 is a data error; 76 -> 58 that stays down (dividend payout) is real
        var prices = Daily(new[] { 83.40m, 53.85m, 83.36m, 84m, 76m, 58m, 58.5m, 59m });

        var cleaned = PriceDataCleaner.RemoveSpikes(prices);

        Assert.DoesNotContain(cleaned, p => p.Price == 53.85m);
        Assert.Contains(cleaned, p => p.Price == 58m);
        Assert.Equal(prices.Count - 1, cleaned.Count);
    }

    [Fact]
    public void Signal_SkipsTrendRules_WithoutEnoughHistory()
    {
        // 60 points: enough for the 50-period average but not the 200-period one
        var a = _service.ComputeAnalytics(Daily(Enumerable.Range(0, 60).Select(i => 100m + i % 5)));

        Assert.DoesNotContain(a.Signal.Reasons, r => r.Indicator == "Price vs 200-day average");
        Assert.Contains(a.Signal.Reasons, r => r.Indicator == "Price vs 50-day average");
        Assert.Equal(4, a.Signal.MaxScore);
    }

    [Theory]
    [InlineData("Down", 110, 100, 105, "Recovering")]
    [InlineData("Down", 102, 100, 105, null)]
    [InlineData("Up", 90, 100, 95, "Weakening")]
    [InlineData("Up", 98, 100, 95, null)]
    [InlineData("Up", 110, 100, 95, null)]
    public void TrendPhase_FlagsAPriceThatHasTurnedBeforeTheAveragesCross(string direction, int price, int sma50, int sma200, string? expected)
    {
        Assert.Equal(expected, SignalRules.TrendPhase(direction, price, sma50, sma200));
    }

    [Fact]
    public void SharedNews_ListsInstrumentsWithTheSameReading()
    {
        NewsSentimentSummaryDto Mood(string symbol, int articles, int score) =>
            new() { Symbol = symbol, Articles = articles, Score = score, Confidence = 79 };
        var moods = new[] { Mood("KSE100", 55, 60), Mood("MIF", 55, 60), Mood("KMI30", 55, 60), Mood("GOLDPK", 198, 23) };

        Assert.Equal(new[] { "KMI30", "MIF" }, MarketAnalyticsService.SharedNews(moods[0], moods));
        Assert.Empty(MarketAnalyticsService.SharedNews(moods[3], moods));
        Assert.Empty(MarketAnalyticsService.SharedNews(Mood("USDPKR", 0, 0), moods.Append(Mood("X", 0, 0))));
    }
}
