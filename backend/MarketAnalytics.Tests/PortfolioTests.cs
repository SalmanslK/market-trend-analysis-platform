using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Tests;

public class PortfolioTests
{
    private static List<DateTime> Days(params string[] dates) =>
        dates.Select(d => DateTime.SpecifyKind(DateTime.Parse(d), DateTimeKind.Utc)).ToList();

    [Fact]
    public void Simulate_HoldsEachPartAtItsWeight()
    {
        var dates = Days("2024-01-01", "2024-01-02");
        var stocks = new double[] { 100, 120 };
        var gold = new double[] { 50, 50 };

        var run = PortfolioService.Simulate(dates, new[] { stocks, gold }, new bool[]?[] { null, null },
            new[] { 0.6, 0.4 }, new[] { true, true }, cash: null, "Never", cost: 0);

        // 60% gains 20%, 40% unchanged
        Assert.Equal(1.12, run.Equity[1], 10);
        Assert.Equal(2, run.Trades);
    }

    [Fact]
    public void Simulate_RebalancesAtTheNewYear()
    {
        var dates = Days("2024-12-30", "2024-12-31", "2025-01-02", "2025-01-03");
        var stocks = new double[] { 100, 200, 200, 100 };
        var gold = new double[] { 100, 100, 100, 100 };

        var never = PortfolioService.Simulate(dates, new[] { stocks, gold }, new bool[]?[] { null, null }, new[] { 0.5, 0.5 }, new[] { true, true }, null, "Never", 0);
        var yearly = PortfolioService.Simulate(dates, new[] { stocks, gold }, new bool[]?[] { null, null }, new[] { 0.5, 0.5 }, new[] { true, true }, null, "Yearly", 0);

        // Without rebalancing, stocks grew to 2/3 of the mix and the halving hurts more
        Assert.Equal(1.0, never.Equity[3], 10);
        // Rebalanced to 50/50 at 1.5 on Jan 2, then stocks halve: 0.75 * 0.5 + 0.75 = 1.125
        Assert.Equal(1.125, yearly.Equity[3], 10);
    }

    [Fact]
    public void Simulate_ChargesCostsButNotForTheCashFund()
    {
        var dates = Days("2024-01-01", "2024-01-02");
        var flat = new double[] { 100, 100 };

        var run = PortfolioService.Simulate(dates, new[] { flat, flat }, new bool[]?[] { null, null },
            new[] { 0.5, 0.5 }, new[] { true, false }, null, "Never", cost: 0.01);

        Assert.Equal(0.5 * 0.99 + 0.5, run.Equity[1], 10);
    }

    [Fact]
    public void Simulate_TrendFilterMovesToCashTheCloseAfterTheTrendTurns()
    {
        var dates = Days("2024-01-01", "2024-01-02", "2024-01-03", "2024-01-04");
        var stocks = new double[] { 100, 100, 50, 50 };
        var cash = new double[] { 10, 10, 10, 11 };
        // Trend turns down at the close of day 1, so the part is sold at day 2's close, after the fall
        var trend = new[] { true, false, false, false };

        var run = PortfolioService.Simulate(dates, new[] { stocks }, new bool[]?[] { trend }, new[] { 1.0 }, new[] { true }, cash, "Never", 0);

        Assert.Equal(0.5, run.Equity[2], 10);
        // Then it earns what the cash fund earned
        Assert.Equal(0.55, run.Equity[3], 10);
    }

    [Theory]
    [InlineData("2024-06-28", "2024-07-01", "HalfYearly", true)]
    [InlineData("2024-03-29", "2024-04-01", "HalfYearly", false)]
    [InlineData("2024-03-29", "2024-04-01", "Quarterly", true)]
    [InlineData("2024-12-31", "2025-01-01", "Never", false)]
    public void IsNewPeriod_FollowsTheSchedule(string previous, string current, string schedule, bool expected)
    {
        Assert.Equal(expected, PortfolioService.IsNewPeriod(DateTime.Parse(previous), DateTime.Parse(current), schedule));
    }

    [Fact]
    public void Align_CarriesTheLastPriceForward()
    {
        var prices = new List<PriceDataPoint> { new(Days("2024-01-02")[0], 10), new(Days("2024-01-04")[0], 12) };

        var aligned = PortfolioService.Align(prices, Days("2024-01-01", "2024-01-02", "2024-01-03", "2024-01-05"), fillBefore: true);

        Assert.Equal(new double[] { 10, 10, 10, 12 }, aligned);
        Assert.Equal(0, PortfolioService.Align(prices, Days("2024-01-01"), fillBefore: false)[0]);
    }

    [Fact]
    public void Measure_ReportsReturnFallAndYears()
    {
        var dates = Days("2024-01-01", "2024-07-01", "2025-01-01");
        var result = PortfolioService.Measure(dates, new[] { 1.0, 0.8, 1.1 }, cashAnnualReturn: null);

        Assert.Equal(10m, result.TotalReturn);
        Assert.Equal(-20m, result.MaxDrawdown);
        Assert.Equal(new[] { 2024, 2025 }, result.YearlyReturns.Select(y => y.Year));
        Assert.Null(result.Sharpe);
    }
}
