using MarketAnalytics.Infrastructure.DataFetchers;

namespace MarketAnalytics.Tests;

public class MarketActivityTests
{
    private static DateTime Utc(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("https://beta-restapi.sarmaaya.pk/api/indices/price-history/KSE100?days=365",
        "https://beta-restapi.sarmaaya.pk/api/indices/volume-history/KSE100?days=30")]
    [InlineData("https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/698bac51-81aa-4e0c-ad94-93d630e1e2a4?days=365", null)]
    [InlineData("https://beta-restapi.sarmaaya.pk/api/forex/price-history/USDPKR?days=1095", null)]
    public void VolumeUrlFor_OnlyMatchesIndices(string source, string? expected)
    {
        Assert.Equal(expected, MarketActivityFetcher.VolumeUrlFor(source, 30));
    }

    [Theory]
    [InlineData("https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/698bac51-81aa-4e0c-ad94-93d630e1e2a4?days=365",
        "https://beta-restapi.sarmaaya.pk/api/mutual-funds/aum-history/698bac51-81aa-4e0c-ad94-93d630e1e2a4?days=10000")]
    [InlineData("https://beta-restapi.sarmaaya.pk/api/indices/price-history/KSE100?days=365", null)]
    public void FundSizeUrlFor_OnlyMatchesFunds(string source, string? expected)
    {
        Assert.Equal(expected, MarketActivityFetcher.FundSizeUrlFor(source));
    }

    [Fact]
    public void ParseVolumes_SkipsDaysWithoutVolume_AndSortsByDate()
    {
        var json = """{"success":true,"response":[{"symbol":"KSE100","volume":195449769,"date":"2026-11-13T00:00:00.000Z"},{"symbol":"KSE100","volume":0,"date":"2013-11-05T00:00:00.000Z"},{"symbol":"KSE100","volume":236415338,"date":"2026-11-12T00:00:00.000Z"}]}""";

        var volumes = MarketActivityFetcher.ParseVolumes(json);

        Assert.Equal([(Utc(2026, 11, 12), 236415338L), (Utc(2026, 11, 13), 195449769L)], volumes);
        Assert.All(volumes, v => Assert.Equal(DateTimeKind.Utc, v.Date.Kind));
    }

    [Fact]
    public void ParseFundSizes_ConvertsThousandsBefore2025ToRupees()
    {
        var json = """{"response":[{"aum":43492760,"date":"2024-12-01"},{"aum":40559380000,"date":"2025-01-01"}]}""";

        var sizes = MarketActivityFetcher.ParseFundSizes(json);

        Assert.Equal([(Utc(2024, 12, 1), 43492760000m), (Utc(2025, 1, 1), 40559380000m)], sizes);
    }

    [Fact]
    public void Parse_ToleratesAMissingResponse()
    {
        Assert.Empty(MarketActivityFetcher.ParseVolumes("""{"success":false}"""));
        Assert.Empty(MarketActivityFetcher.ParseFundSizes("""{"response":null}"""));
    }
}
