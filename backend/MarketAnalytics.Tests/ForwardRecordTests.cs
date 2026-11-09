using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Tests;

public class ForwardRecordTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // One price per day from Start, growing by `daily` (a fraction) each day
    private static List<PriceDataPoint> Growing(int days, decimal daily, decimal from = 100m)
    {
        var result = new List<PriceDataPoint>();
        var price = from;
        for (int i = 0; i < days; i++)
        {
            result.Add(new PriceDataPoint(Start.AddDays(i), price));
            price *= 1 + daily;
        }
        return result;
    }

    [Fact]
    public void StrongerThanCash_ChecksOnTheFirstDayOfEachMonthOnceThereAre12Months()
    {
        var checks = StrongerThanCash.MonthlyChecks(Growing(500, 0.001m), Growing(500, 0.0003m));

        Assert.All(checks, c => Assert.Equal(1, c.Date.Day));
        Assert.Equal(new DateTime(2025, 1, 1), checks[0].Date.Date);
        Assert.All(checks, c => Assert.True(c.AheadOfCash));
    }

    [Fact]
    public void StrongerThanCash_SaysBehindCashAndSinceWhenAfterAFall()
    {
        // Rises for a year and a half, then falls hard: the 12-month return drops below cash's
        var prices = Growing(550, 0.001m);
        var price = prices[^1].Price;
        for (int i = 550; i < 800; i++)
        {
            price *= 0.997m;
            prices.Add(new PriceDataPoint(Start.AddDays(i), price));
        }

        var result = StrongerThanCash.Evaluate(prices, Growing(800, 0.0003m));

        Assert.NotNull(result);
        Assert.False(result!.AheadOfCash);
        Assert.True(result.Return12M < result.CashReturn12M);
        Assert.NotNull(result.Since);
        Assert.Equal(1, result.Since!.Value.Day);
    }

    [Fact]
    public void StrongerThanCash_IsNullWithLessThanAYear()
    {
        Assert.Null(StrongerThanCash.Evaluate(Growing(300, 0.001m), Growing(300, 0.0003m)));
    }

    [Fact]
    public void Summary_ScoresOnlyReadingsWhoseNext20DaysAreKnown()
    {
        var prices = Growing(40, 0.01m);
        var snapshots = new[]
        {
            new SignalSnapshot { InstrumentId = 1, Date = Start.AddDays(5), DailyAction = "Buy", SteadyAction = "StrongBuy", CombinedAction = "Hold", AheadOfCash = true },
            new SignalSnapshot { InstrumentId = 1, Date = Start.AddDays(30), DailyAction = "Sell", CombinedAction = "Sell", AheadOfCash = false },
        };

        var summary = SignalRecordSummary.Summarize(snapshots, new Dictionary<int, List<PriceDataPoint>> { [1] = prices });

        Assert.Equal(2, summary.Readings);
        Assert.Equal(1, summary.Scored);
        Assert.Equal(Start.AddDays(5), summary.Since);
        var buy = summary.Groups.Single(g => g.Signal == "Technical signal (steady)" && g.Reading == "Buy");
        Assert.Equal(1, buy.Count);
        // From the next close (day 6) to 20 days later (day 26): 1.01^20 - 1
        Assert.Equal(Math.Round(((decimal)Math.Pow(1.01, 20) - 1) * 100, 2), buy.AverageMove);
        Assert.Equal(100, buy.ShareUp);
        Assert.Equal(0, summary.Groups.Single(g => g.Reading == "Behind cash").Count);
        Assert.Null(summary.Groups.Single(g => g.Reading == "Behind cash").AverageMove);
    }

    [Theory]
    [InlineData("StrongBuy", "Buy")]
    [InlineData("Buy", "Buy")]
    [InlineData("Hold", "Hold")]
    [InlineData(null, "Hold")]
    [InlineData("StrongSell", "Sell")]
    public void Summary_GroupsStrongAndPlainSignalsTogether(string? action, string side)
    {
        Assert.Equal(side, SignalRecordSummary.Side(action));
    }
}
