using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.ValueObjects;
using MarketAnalytics.Infrastructure.Brief;

namespace MarketAnalytics.Tests;

public class BriefTests
{
    private static List<PriceDataPoint> Prices(Func<int, double> price, int days) =>
        Enumerable.Range(0, days)
            .Select(i => new PriceDataPoint(new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i), (decimal)price(i)))
            .ToList();

    [Fact]
    public void Outlook_InASteadyRise_EveryPastUpDayEndedHigher()
    {
        // Rising 0.1% a day: the trend is Up throughout, and every later price is higher
        var prices = Prices(i => 100 * Math.Pow(1.001, i), 900);

        var outlook = HistoricalOutlook.Compute("TEST", prices, new IndicatorCalculationService())!;

        Assert.Equal("Up", outlook.State);
        Assert.Equal(1, outlook.Spells);
        var month = outlook.Horizons[0];
        Assert.Equal("1 month", month.Label);
        Assert.Equal(100, month.SameState.ShareHigherPercent);
        Assert.Equal(Math.Round((decimal)((Math.Pow(1.001, 21) - 1) * 100), 1), month.SameState.MedianPercent);
        // Every day with a trend counts in "all days" too, here the same days
        Assert.Equal(month.AllDays.Days, month.SameState.Days);
    }

    [Fact]
    public void Outlook_NeedsTwoHundredDaysAndAMonthAfter()
    {
        Assert.Null(HistoricalOutlook.Compute("TEST", Prices(i => 100 + i, 210), new IndicatorCalculationService()));
    }

    [Fact]
    public void SplitSources_MovesTheSourcesSectionIntoAList()
    {
        var brief = "## Today in one paragraph\nCalm.\n\n## Sources\n- Reuters: Oil steadies - https://example.com/oil\n- https://example.org/imf\n- Reuters again - https://example.com/oil\n";

        var (text, sources) = ClaudeCodeBriefWriter.SplitSources(brief);

        Assert.Equal("## Today in one paragraph\nCalm.", text);
        Assert.Equal(2, sources.Count);
        Assert.Equal("Reuters: Oil steadies", sources[0].Title);
        Assert.Equal("https://example.com/oil", sources[0].Url);
        Assert.Equal("https://example.org/imf", sources[1].Title);
    }

    [Fact]
    public void SplitSources_LeavesABriefWithoutSourcesAlone()
    {
        var (text, sources) = ClaudeCodeBriefWriter.SplitSources("## Gold\nUp.\n");
        Assert.Equal("## Gold\nUp.", text);
        Assert.Empty(sources);
    }

    [Theory]
    [InlineData("""{"type":"result","is_error":false,"result":"## Gold\nUp."}""", "## Gold\nUp.", false)]
    [InlineData("""{"type":"result","is_error":true,"result":"Usage limit reached"}""", "Usage limit reached", true)]
    [InlineData("not json", null, true)]
    public void ParseResult_ReadsClaudeCodeJson(string output, string? text, bool isError)
    {
        Assert.Equal((text, isError), ClaudeCodeBriefWriter.ParseResult(output));
    }
}
