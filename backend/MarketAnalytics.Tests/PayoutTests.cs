using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.ValueObjects;
using MarketAnalytics.Infrastructure.DataFetchers;

namespace MarketAnalytics.Tests;

public class PayoutTests
{
    private static readonly DateTime Start = new(2025, 6, 23, 0, 0, 0, DateTimeKind.Utc);

    private static List<PriceDataPoint> Daily(params decimal[] prices) =>
        prices.Select((p, i) => new PriceDataPoint(Start.AddDays(i), p)).ToList();

    private static FundPayout Payout(int day, decimal amount) =>
        new() { PayoutDate = Start.AddDays(day), AmountPerUnit = amount };

    [Fact]
    public void Adjust_ScalesEarlierPricesSoThePayoutIsNotAFall()
    {
        // NAV 100 -> 110, then a 10 payout takes it to 100 and it rises to 105
        var prices = Daily(100, 110, 100, 105);

        var adjusted = PayoutAdjuster.Adjust(prices, new[] { Payout(2, 10) });

        // Latest prices stay real NAVs
        Assert.Equal(100m, adjusted[2].Price);
        Assert.Equal(105m, adjusted[3].Price);
        // Before the payout prices are scaled by (1 - 10/110), so the payout day shows no change
        Assert.Equal(110m * (1 - 10m / 110m), adjusted[1].Price);
        Assert.Equal(100m * (1 - 10m / 110m), adjusted[0].Price);
        // Total return: 100 -> 105 plus 10 paid out, reinvested at 100
        Assert.Equal(105m / 100m * 110m / 100m, 105m / adjusted[0].Price, 10);
    }

    [Fact]
    public void MatchExDates_FindsTheDropDaysAfterTheListedDate()
    {
        // Listed on day 1, but the NAV only drops on day 3
        var prices = Daily(100, 101, 102, 92, 93);

        var matches = PayoutAdjuster.MatchExDates(prices, new[] { Payout(1, 10) });

        Assert.Equal(3, Assert.Single(matches).Index);
    }

    [Fact]
    public void MatchExDates_PrefersTheMatchingDropOverALaterMarketFall()
    {
        // MIF, June 2026: a 1.00 payout on the listed day, then a 1.05 market fall three trading days later
        var prices = Daily(161.00m, 160.00m, 161.80m, 161.75m, 160.70m);

        var matches = PayoutAdjuster.MatchExDates(prices, new[] { Payout(1, 1) });

        Assert.Equal(1, Assert.Single(matches).Index);
    }

    [Fact]
    public void Adjust_CombinesSeveralPayouts()
    {
        var prices = Daily(100, 90, 90, 81);

        var adjusted = PayoutAdjuster.Adjust(prices, new[] { Payout(1, 10), Payout(3, 9) });

        // Each payout is 10% of the NAV before it, so day 0 is scaled by 0.9 twice
        Assert.Equal(81m, adjusted[0].Price);
        Assert.Equal(81m, adjusted[1].Price);
        Assert.Equal(81m, adjusted[3].Price);
    }

    [Fact]
    public void Adjust_IgnoresPayoutsOutsideThePriceHistory()
    {
        var prices = Daily(100, 101, 102);

        var adjusted = PayoutAdjuster.Adjust(prices, new[] { Payout(-30, 5), Payout(40, 5) });

        Assert.Equal(prices, adjusted);
    }

    [Fact]
    public void ApplyPayouts_ListsNewestFirstWithTheLastYearsYield()
    {
        var prices = Daily(100, 110, 100, 105);
        var analytics = new InstrumentAnalyticsDto { LastDate = prices[^1].DateTime, LastPrice = 105m };

        MarketAnalyticsService.ApplyPayouts(analytics, prices, new[] { Payout(2, 10), Payout(-400, 3) });

        var payout = Assert.Single(analytics.Payouts);
        Assert.Equal(prices[2].DateTime, payout.Date);
        Assert.Equal(Math.Round(10m / 105m * 100, 2), analytics.PayoutYield1Y);
    }

    [Theory]
    [InlineData("https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/698bac51-81aa-4e0c-ad94-93d630e1e2a4?days=365",
        "https://beta-restapi.sarmaaya.pk/api/mutual-funds/payouts/698bac51-81aa-4e0c-ad94-93d630e1e2a4")]
    [InlineData("https://beta-restapi.sarmaaya.pk/api/indices/KSE100/history", null)]
    public void PayoutUrlFor_OnlyMatchesFundNavHistory(string source, string? expected)
    {
        Assert.Equal(expected, FundPayoutFetcher.PayoutUrlFor(source));
    }

    [Fact]
    public void Parse_ReadsDatesAndAmounts()
    {
        var json = """{"success":true,"response":[{"name":"MIF","payoutPerUnit":13.13,"payoutDate":"2024-06-27"},{"name":"MIF","payoutPerUnit":0,"payoutDate":"2023-06-27"}]}""";

        var payout = Assert.Single(FundPayoutFetcher.Parse(json));

        Assert.Equal(new DateTime(2024, 6, 27, 0, 0, 0, DateTimeKind.Utc), payout.Date);
        Assert.Equal(DateTimeKind.Utc, payout.Date.Kind);
        Assert.Equal(13.13m, payout.AmountPerUnit);
    }
}
