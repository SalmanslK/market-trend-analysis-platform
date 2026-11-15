using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Tests;

public class MarketActivitySummaryTests
{
    private static DateTime Day(int i) => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i);
    private static DateTime Month(int y, int m) => new(y, m, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SummarizeVolume_ComparesTheLastMonthWithTheUsualLevel()
    {
        // 80 days at 100, then 20 days at 200: recent 200, usual (100 days) 120
        var volumes = Enumerable.Range(0, 100).Select(i => new TradingVolume { Date = Day(i), Shares = i < 80 ? 100 : 200 }).ToList();

        var summary = MarketActivity.SummarizeVolume(volumes)!;

        Assert.Equal(200, summary.Average20);
        Assert.Equal(120, summary.Average100);
        Assert.Equal(166.7m, summary.RecentVersusUsualPercent);
        Assert.True(summary.Busier);
        Assert.Equal(Day(99), summary.LatestDate);
    }

    [Fact]
    public void SummarizeVolume_NeedsAHundredDays()
    {
        var volumes = Enumerable.Range(0, 99).Select(i => new TradingVolume { Date = Day(i), Shares = 1 }).ToList();
        Assert.Null(MarketActivity.SummarizeVolume(volumes));
    }

    [Fact]
    public void SummarizeFundSize_SeparatesMoneyInFromPriceGains()
    {
        // The fund grew 30% while its unit price rose 10%: 20% of the old size was new money
        var sizes = new List<FundAssets>
        {
            new() { Month = Month(2026, 9), Rupees = 1000 },
            new() { Month = Month(2026, 10), Rupees = 1300 },
        };
        var prices = new List<PriceDataPoint> { new(Month(2026, 8).AddDays(30), 10m), new(Month(2026, 9).AddDays(29), 11m), new(Month(2026, 10).AddDays(5), 50m) };

        var summary = MarketActivity.SummarizeFundSize(sizes, prices)!;

        Assert.Equal(1300, summary.Rupees);
        Assert.Equal(20.0m, summary.Flow1MPercent);
        Assert.Null(summary.Flow12MPercent);
    }

    [Fact]
    public void SummarizeFundSize_SkipsAFlowAcrossAMissingMonth()
    {
        var sizes = new List<FundAssets>
        {
            new() { Month = Month(2026, 8), Rupees = 1000 },
            new() { Month = Month(2026, 10), Rupees = 1300 },
        };
        var prices = new List<PriceDataPoint> { new(Month(2026, 7), 10m) };

        Assert.Null(MarketActivity.SummarizeFundSize(sizes, prices)!.Flow1MPercent);
    }
}
