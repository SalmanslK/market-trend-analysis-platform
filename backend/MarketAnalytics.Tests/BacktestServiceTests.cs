using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketAnalytics.Tests;

public class BacktestServiceTests
{
    // Repositories aren't used when prices are passed in directly
    private readonly BacktestService _service = new(null!, null!, new IndicatorCalculationService(), NullLogger<BacktestService>.Instance);
    private readonly MarketAnalyticsService _analytics = new(null!, null!, new IndicatorCalculationService(), NullLogger<MarketAnalyticsService>.Instance);

    private static readonly DateTime Start = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<PriceDataPoint> Daily(IEnumerable<decimal> prices) =>
        prices.Select((p, i) => new PriceDataPoint(Start.AddDays(i), p)).ToList();

    // A repeatable zig-zag so the signal changes direction many times
    private static List<PriceDataPoint> Wavy(int count) =>
        Daily(Enumerable.Range(0, count).Select(i => 100m + (decimal)(30 * Math.Sin(i / 25.0) + 8 * Math.Sin(i / 3.0) + i * 0.05)));

    private static DateTime[] Dates(int count) => Enumerable.Range(0, count).Select(i => Start.AddDays(i)).ToArray();

    [Fact]
    public void Simulate_ActsOnASignalAtTheNextClose()
    {
        // Signal at close 0 -> buy at close 1 (100). Earns 100->110->121. Signal at close 2 -> sell at close 3 (121).
        // Trading at the signal's own close would instead have missed or caught different moves.
        var closes = new double[] { 100, 100, 110, 121, 110 };
        var scores = new[] { 1, 1, -1, -1, -1 };

        var (equity, trades, timeInvested) = BacktestService.Simulate(Dates(5), closes, scores, cost: 0, cashInterest: 0);

        Assert.Equal(1.21, equity[^1], 10);
        Assert.Equal(2, trades);
        Assert.Equal(50m, timeInvested); // invested through 2 of the 4 periods
    }

    [Fact]
    public void Simulate_ChargesCostOnEveryTrade()
    {
        var closes = new double[] { 100, 100, 110, 121, 110 };
        var scores = new[] { 1, 1, -1, -1, -1 };

        var (equity, _, _) = BacktestService.Simulate(Dates(5), closes, scores, cost: 0.01, cashInterest: 0);

        Assert.Equal(0.99 * 1.21 * 0.99, equity[^1], 10);
    }

    [Fact]
    public void Simulate_HoldScoreKeepsThePosition()
    {
        var closes = new double[] { 100, 100, 110, 120 };
        var scores = new[] { 1, 0, 0, 0 };

        var (equity, trades, _) = BacktestService.Simulate(Dates(4), closes, scores, cost: 0, cashInterest: 0);

        Assert.Equal(1.2, equity[^1], 10);
        Assert.Equal(1, trades);
    }

    [Fact]
    public void Simulate_CashEarnsInterestOnlyWhenAsked()
    {
        var dates = new[] { Start, Start.AddDays(365.25) };
        var closes = new double[] { 100, 50 };
        var scores = new[] { -1, -1 };

        Assert.Equal(1.0, BacktestService.Simulate(dates, closes, scores, 0, 0).Equity[^1], 10);
        Assert.Equal(1.1, BacktestService.Simulate(dates, closes, scores, 0, 0.1).Equity[^1], 10);
    }

    [Fact]
    public void DailyScores_DoNotChange_WhenFuturePricesAreAdded()
    {
        var prices = Wavy(500);

        var early = _service.ComputeDailyScores(prices.Take(350).ToList());
        var full = _service.ComputeDailyScores(prices);

        // Same dates, same scores: nothing in a day's score depends on later prices
        Assert.Equal(early.Select(d => (d.Date, d.Score)), full.Take(early.Count).Select(d => (d.Date, d.Score)));
    }

    [Theory]
    [InlineData(300)]
    [InlineData(377)]
    [InlineData(450)]
    public void LastDailyScore_MatchesTheLiveSignal(int count)
    {
        var prices = Wavy(count);

        var live = _analytics.ComputeAnalytics(prices).Signal;
        var backtested = _service.ComputeDailyScores(prices)[^1];

        Assert.Equal(live.Score, backtested.Score);
        Assert.Equal(5, live.MaxScore);
    }

    [Fact]
    public void GoldenCross_FollowsTheTwoAverages()
    {
        var scores = BacktestService.GoldenCrossScores(new[] { 110m, 100m, 90m }, new[] { 100m, 100m, 100m });

        Assert.Equal(new[] { 1, 1, -1 }, scores);
    }

    [Fact]
    public void MonthlyTrend_OnlyDecidesOnTheFirstTradingDayOfEachMonth()
    {
        var dates = new[]
        {
            new DateTime(2024, 1, 30), new DateTime(2024, 1, 31), // first day in the data, then mid-month
            new DateTime(2024, 2, 1), new DateTime(2024, 2, 2),   // new month, then mid-month
            new DateTime(2025, 2, 3),                             // same month number, different year
        };
        var prices = new[] { 110m, 50m, 50m, 150m, 150m };
        var averages = new[] { 100m, 100m, 100m, 100m, 100m };

        var scores = BacktestService.MonthlyTrendScores(dates, prices, averages);

        Assert.Equal(new[] { 1, 0, -1, 0, 1 }, scores);
    }

    [Fact]
    public void SlowerRules_DoNotChange_WhenFuturePricesAreAdded()
    {
        var prices = Wavy(900);
        var early = _service.Run(prices.Take(700).ToList(), costPercent: 0m);
        var full = _service.Run(prices, costPercent: 0m);

        // Compare at a date in both runs, away from the shortened run's final point
        var sharedDate = early.EquityCurve[early.EquityCurve.Count / 2].Date;
        var earlyPoint = early.EquityCurve.Single(p => p.Date == sharedDate);
        var fullDays = _service.ComputeDailyScores(prices);
        var earlyDays = _service.ComputeDailyScores(prices.Take(700).ToList());
        var index = fullDays.FindIndex(d => d.Date == sharedDate);

        // Recompute the full run's equity at that date from its own scores
        var dates = fullDays.Take(index + 1).Select(d => d.Date).ToList();
        var closes = fullDays.Take(index + 1).Select(d => (double)d.Price).ToList();
        var indicators = new IndicatorCalculationService();
        var sma50 = indicators.ComputeSma(prices, 50);
        var sma200 = indicators.ComputeSma(prices, 200);
        var scores = BacktestService.GoldenCrossScores(dates.Select(d => sma50[d]!.Value).ToList(), dates.Select(d => sma200[d]!.Value).ToList());
        var equity = BacktestService.Simulate(dates, closes, scores, 0, 0).Equity[^1];

        Assert.Equal(equity * 100, (double)earlyPoint.GoldenCross, 6);
        Assert.Equal(earlyDays[0].Date, fullDays[0].Date);
        Assert.NotEmpty(full.Rules);
    }

    [Fact]
    public void Run_ReportsTheSignalAndBothSlowerRules()
    {
        var result = _service.Run(Wavy(1200));

        Assert.Equal(new[] { "signal", "goldenCross", "monthlyTrend" }, result.Rules.Select(r => r.Key));
        Assert.Equal(result.Strategy.TotalReturn, result.Rules[0].Performance.TotalReturn);
        Assert.Equal(result.Verdict, result.Rules[0].Verdict);
        // Slower rules are slower: fewer trades than the signal
        Assert.True(result.Rules[1].Performance.Trades < result.Rules[0].Performance.Trades);
        Assert.True(result.Rules[2].Performance.Trades < result.Rules[0].Performance.Trades);
    }

    private static List<ScoredNews> SteadyNews(int days, int score, int perDay = 1) =>
        Enumerable.Range(0, days).SelectMany(i => Enumerable.Repeat(new ScoredNews(Start.AddDays(i), score, 0.9, NewsSentimentAnalyzer.LabelFor(score)), perDay)).ToList();

    [Fact]
    public void SignalRules_ScoreNewsAndCombined_AsSpecified()
    {
        // News needs at least 3 articles
        Assert.Equal(0, SignalRules.NewsScore(80, 2));
        Assert.Equal(80, SignalRules.NewsScore(80, 3));

        // Combined is the average of technical (as % of its maximum) and news
        Assert.Equal((40.0 + 80) / 2, SignalRules.CombinedScore(2, 5, 80, 3));
        // Without enough news it is technical alone
        Assert.Equal(40, SignalRules.CombinedScore(2, 5, 80, 2));
        // They can cancel out
        Assert.Equal(0, SignalRules.CombinedScore(-3, 5, 60, 10));

        Assert.Equal("StrongBuy", SignalRules.ActionForScaledScore(60));
        Assert.Equal("Buy", SignalRules.ActionForScaledScore(20));
        Assert.Equal("Hold", SignalRules.ActionForScaledScore(19.9));
        Assert.Equal("Sell", SignalRules.ActionForScaledScore(-20));
        Assert.Equal("StrongSell", SignalRules.ActionForScaledScore(-60));
    }

    [Fact]
    public void CombinedAction_MatchesTechnicalAction_WithoutNews()
    {
        foreach (var score in Enumerable.Range(-5, 11))
            Assert.Equal(SignalRules.ActionFor(score), SignalRules.ActionForScaledScore(SignalRules.CombinedScore(score, 5, 0, 0)));
    }

    [Fact]
    public void DailyMood_OnlyUsesTheLast7Days_AndNothingLater()
    {
        var news = new List<ScoredNews>
        {
            new(Start, -100, 1, "Negative"),
            new(Start.AddDays(6), 100, 1, "Positive"),
            new(Start.AddDays(7), 100, 1, "Positive"),
            new(Start.AddDays(20), -100, 1, "Negative"),
        };
        var dates = new[] { Start, Start.AddDays(6), Start.AddDays(7), Start.AddDays(14), Start.AddDays(19) };

        var moods = NewsService.DailyMood(dates, news, 7);

        Assert.Equal(new[] { 1, 2, 2, 0, 0 }, moods.Select(m => m.Articles));
        Assert.Equal(-100, moods[0].Score);
        Assert.Equal(0, moods[1].Score);
        Assert.Equal(100, moods[2].Score); // the day-0 article has left the window
    }

    [Fact]
    public void NewsTest_Reacting_OnlyAfterTheNewsIsPublished()
    {
        // Prices jump on day 500; news only turns positive that same day, so no signal may profit from the jump
        var dates = Dates(1000).ToList();
        var closes = Enumerable.Range(0, 1000).Select(i => i < 500 ? 100.0 : 200.0).ToList();
        var technical = Enumerable.Repeat(0, 1000).ToList();
        var news = SteadyNews(500, -80, 3).Concat(Enumerable.Range(500, 500).SelectMany(i => Enumerable.Repeat(new ScoredNews(Start.AddDays(i), 80, 0.9, "Positive"), 3))).ToList();

        var test = BacktestService.RunNewsTest(dates, closes, technical, news, cost: 0);

        var rule = test.Rules.Single(r => r.Key == "news");
        Assert.True(test.HasEnoughNews);
        Assert.True(Math.Abs(rule.Performance.TotalReturn) < 0.001m, $"{rule.Performance.TotalReturn}");
        Assert.Equal(100m, test.Holding.TotalReturn);
    }

    [Fact]
    public void NewsTest_NeedsEnoughNewsHistory()
    {
        var dates = Dates(1000).ToList();
        var closes = Enumerable.Range(0, 1000).Select(i => 100.0 + i).ToList();
        var technical = Enumerable.Repeat(1, 1000).ToList();
        // News only in the last 100 days
        var news = Enumerable.Range(900, 100).SelectMany(i => Enumerable.Repeat(new ScoredNews(Start.AddDays(i), 80, 0.9, "Positive"), 3)).ToList();

        var test = BacktestService.RunNewsTest(dates, closes, technical, news, cost: 0);

        Assert.False(test.HasEnoughNews);
        Assert.Empty(test.Rules);
        Assert.Equal(300, test.Articles);
    }

    [Fact]
    public void NewsTest_ComparesAllRulesOnTheSameDates()
    {
        var dates = Dates(800).ToList();
        var closes = Enumerable.Range(0, 800).Select(i => 100.0 + 30 * Math.Sin(i / 25.0)).ToList();
        var technical = closes.Select((_, i) => i % 40 < 20 ? 2 : -2).ToList();
        var news = Enumerable.Range(300, 500).SelectMany(i => Enumerable.Repeat(new ScoredNews(Start.AddDays(i), i % 60 < 30 ? 70 : -70, 0.9, i % 60 < 30 ? "Positive" : "Negative"), 3)).ToList();

        var test = BacktestService.RunNewsTest(dates, closes, technical, news, cost: 0.005);

        Assert.True(test.HasEnoughNews);
        Assert.Equal(new[] { "technical", "news", "combined" }, test.Rules.Select(r => r.Key));
        Assert.Equal(Start.AddDays(300), test.StartDate);
        Assert.Equal(500, test.TradingDays);
        Assert.InRange(test.CoveragePercent, 99m, 100m);
        Assert.Equal(100m, test.EquityCurve[0].Holding);
    }

    [Fact]
    public void LiveSignals_UseTheSameRules_AsTheBacktest()
    {
        var analytics = _analytics.ComputeAnalytics(Wavy(400));
        var mood = new MarketAnalytics.Application.DTOs.NewsSentimentSummaryDto { Score = 70, Articles = 5, Confidence = 60 };

        MarketAnalyticsService.ApplyNewsSignals(analytics, mood);

        Assert.Equal("StrongBuy", analytics.NewsSignal.Action);
        var expected = SignalRules.CombinedScore(analytics.Signal.Score, analytics.Signal.MaxScore, 70, 5);
        Assert.Equal(SignalRules.ActionForScaledScore(expected), analytics.CombinedSignal.Action);
        Assert.Equal((int)Math.Round(expected), analytics.CombinedSignal.Score);
    }

    [Fact]
    public void Run_NeedsEnoughHistory()
    {
        var result = _service.Run(Wavy(300));

        Assert.False(result.HasEnoughData);
        Assert.Empty(result.EquityCurve);
    }

    [Fact]
    public void Run_BuyAndHoldReturn_MatchesThePriceChangeLessCost()
    {
        var prices = Wavy(800);
        var days = _service.ComputeDailyScores(prices);

        var result = _service.Run(prices, costPercent: 1m);

        // Bought at the second scored day's close, paying 1%
        var expected = ((double)days[^1].Price / (double)days[1].Price * 0.99 - 1) * 100;
        Assert.True(result.HasEnoughData);
        Assert.Equal(expected, (double)result.BuyAndHold.TotalReturn, 6);
        Assert.Equal(1, result.BuyAndHold.Trades);
    }

    [Fact]
    public void Run_SteadyRise_DoesNotBeatHolding()
    {
        // In a straight-line rise the signal stays invested after entry, so it can't beat holding
        var result = _service.Run(Daily(Enumerable.Range(0, 800).Select(i => 100m + i)));

        Assert.True(result.Strategy.AnnualReturn <= result.BuyAndHold.AnnualReturn);
        Assert.True(result.Strategy.TimeInvested > 95);
    }

    [Fact]
    public void Run_OutcomeCounts_AddUpToAllDays()
    {
        var result = _service.Run(Wavy(800));

        var all = result.Outcomes.Single(o => o.Action == "All");
        Assert.Equal(result.TradingDays, all.Days);
        Assert.Equal(all.Days, result.Outcomes.Where(o => o.Action != "All").Sum(o => o.Days));
        Assert.All(result.Outcomes.Where(o => o.Action != "All"), o => Assert.InRange(o.Periods, 1, o.Days));
        Assert.All(all.Horizons, h => Assert.InRange(h.PercentPositive, 0, 100));
    }

    [Fact]
    public void Run_ThinsTheChartCurve_ButKeepsTheEnds()
    {
        var prices = Wavy(2000);

        var result = _service.Run(prices);

        Assert.InRange(result.EquityCurve.Count, 2, 700);
        Assert.Equal(result.StartDate, result.EquityCurve[0].Date);
        Assert.Equal(result.EndDate, result.EquityCurve[^1].Date);
        Assert.Equal(100m, result.EquityCurve[0].BuyAndHold);
    }
}
