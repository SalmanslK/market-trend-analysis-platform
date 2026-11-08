using System.Net;
using System.Text;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Infrastructure.News;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketAnalytics.Tests;

public class NewsTopicClassifierTests
{
    [Fact]
    public void PakistanStockNews_IsTaggedAsPakistanStocks()
    {
        var topics = NewsTopicClassifier.Classify("PSX sheds 3,560 points amid panic selling in jittery October", "");

        Assert.Contains("pakistan-stocks", topics);
    }

    [Fact]
    public void ForeignStockNews_IsNotTaggedAsPakistanStocks()
    {
        var topics = NewsTopicClassifier.Classify("European stocks fall as bond yields climb", "Shares in Frankfurt and Paris slipped.");

        Assert.DoesNotContain("pakistan-stocks", topics);
    }

    [Fact]
    public void GlobalGoldNews_IsTaggedWithoutPakistanContext()
    {
        var topics = NewsTopicClassifier.Classify("Gold prices plunge to one-year low below $3,900", "");

        Assert.Equal(new[] { "gold" }, topics);
    }

    [Theory]
    [InlineData("Financials and consumer staples stocks lift Sri Lankan shares higher", "The index rose as Rs. 3 billion in shares traded.")]
    [InlineData("Indian stocks set for pullback after rally on oil slide", "The rupee weakened.")]
    public void ForeignMarketNews_IsNotPakistanStocks_EvenWithRupeeMentions(string title, string summary)
    {
        Assert.DoesNotContain("pakistan-stocks", NewsTopicClassifier.Classify(title, summary));
    }

    [Fact]
    public void PakistanNews_MentioningForeignMarkets_IsStillPakistanStocks()
    {
        Assert.Contains("pakistan-stocks", NewsTopicClassifier.Classify("PSX falls as India tensions rise", ""));
    }

    [Fact]
    public void GoldFund_FollowsGoldNews_NotStockNews()
    {
        Assert.True(NewsTopicClassifier.IsDirectTopic("gold", "MGF"));
        Assert.False(NewsTopicClassifier.IsDirectTopic("pakistan-stocks", "MGF"));
        Assert.Empty(NewsTopicClassifier.TopicsForSymbol("pakistan-stocks", "MGF"));
        Assert.True(NewsTopicClassifier.IsDirectTopic("pakistan-stocks", "MIF"));
    }

    [Fact]
    public void Topics_LinkToTheirInstruments()
    {
        var linked = NewsTopicClassifier.TopicsForSymbol("gold,rupee-dollar", "GOLDPK").Select(t => t.Key);

        Assert.Equal(new[] { "gold", "rupee-dollar" }, linked);
        Assert.Contains("rupee-dollar", NewsTopicClassifier.TopicKeysForSymbol("USDPKR"));
    }
}

public class NewsSentimentAnalyzerTests
{
    [Fact]
    public void NegativeHeadline_ScoresNegative()
    {
        var result = NewsSentimentAnalyzer.Analyze("PSX sheds 3,560 points amid panic selling in jittery October", "");

        Assert.Equal("Negative", result.Label);
        Assert.True(result.Score <= -80);
        Assert.Contains("sheds", result.NegativeWords);
        Assert.True(result.Confidence > 80);
    }

    [Fact]
    public void PositiveHeadline_ScoresPositive()
    {
        var result = NewsSentimentAnalyzer.Analyze("KSE-100 surges to record high as bulls return", "");

        Assert.Equal("Positive", result.Label);
        Assert.True(result.Score >= 80);
    }

    [Fact]
    public void StrongWords_ScoreHigherThanMildOnes()
    {
        var strong = NewsSentimentAnalyzer.Analyze("PSX surges 2,000 points", "").Score;
        var plain = NewsSentimentAnalyzer.Analyze("PSX gains 900 points", "").Score;
        var mild = NewsSentimentAnalyzer.Analyze("PSX edges up slightly", "").Score;

        Assert.True(strong > plain && plain > mild && mild > 0);
    }

    [Theory]
    [InlineData("Rupee continues upward trend against dollar", "Positive")]
    [InlineData("PSX slips 348 points on profit-taking", "Negative")]
    [InlineData("KSE-100 closes in the green as bulls return", "Positive")]
    [InlineData("Profit-taking snaps four-day rally at PSX", "Negative")]
    [InlineData("KSE-100 ends three-day losing streak", "Positive")]
    [InlineData("PSX Benchmark Index Up 2.5% WoW", "Positive")]
    [InlineData("A look at Top Stocks at PSX as Market ends October in Red", "Negative")]
    [InlineData("PSX stages strong comeback on easing fears", "Positive")]
    [InlineData("PSX Closing Bell: Bulls Head for the Exit", "Negative")]
    [InlineData("PSX reverses gains as geopolitics weigh", "Negative")]
    [InlineData("PSX reclaims 175,000-point level", "Positive")]
    [InlineData("Stocks dumped as PSX dives over 3,000 points", "Negative")]
    [InlineData("Gold price up by Rs 1500", "Positive")]
    [InlineData("Pakistan rupee at all time low owing to political instability", "Negative")]
    public void CommonMarketPhrasing_IsRecognised(string title, string expected)
    {
        Assert.Equal(expected, NewsSentimentAnalyzer.Analyze(title, "").Label);
    }

    [Theory]
    [InlineData("KSE100", "PSX rebounds over 1,000 points on oil price fall", "Positive")]   // cheaper oil helps Pakistan
    [InlineData("KSE100", "PSX ends four-session rally on oil surge", "Negative")]
    [InlineData("GOLDPK", "Gold prices surge as dollar weakens", "Positive")]
    [InlineData("GOLDPK", "Gold drops over 2% as dollar strengthens", "Negative")]
    [InlineData("GOLDPK", "Gold prices remain unchanged, silver declines in domestic market", "Neutral")]
    [InlineData("GOLDPK", "Gold firms, silver slides as oil surge revives inflation pressure", "Positive")]
    [InlineData("USDPKR", "Rupee gains against US dollar", "Negative")]                 // rupee stronger -> USD/PKR falls
    [InlineData("USDPKR", "Dollar hits historic high at Rs170 in interbank market", "Positive")]
    [InlineData("USDPKR", "Pakistan's foreign exchange reserves climb to record high", "Negative")]
    public void Sentiment_IsReadFromTheInstrumentsPointOfView(string symbol, string title, string expected)
    {
        Assert.Equal(expected, NewsSentimentAnalyzer.AnalyzeFor(symbol, title, "").Label);
    }

    [Fact]
    public void OtherSubjects_DontCountInTheGeneralReading()
    {
        // The headline is about gold; silver falling says nothing about it
        var result = NewsSentimentAnalyzer.Analyze("Gold holds steady in Pakistan, silver rates decline", "");

        Assert.Equal("Neutral", result.Label);
    }

    [Theory]
    [InlineData("Rupee falls 9 paise to 94.50 against US dollar in early trade", true)]
    [InlineData("Rupee falls to record low of 95.63 against U.S. dollar", true)]
    [InlineData("Gold price cut by Tk 2,740 a bhari", true)]
    [InlineData("Gold Rate Today in Pakistan | Latest Gold Prices Per Tola and 10 Grams", false)]
    [InlineData("Pakistani rupee edges higher against US dollar", false)]
    public void ForeignLocalMarkets_AreRecognised(string title, bool expected)
    {
        Assert.Equal(expected, NewsTopicClassifier.IsForeignLocalMarket(title));
    }

    [Fact]
    public void BackgroundTopics_AreNotDirectForUnrelatedInstruments()
    {
        // Oil news is shown for USD/PKR as context, but its tone isn't about the rupee
        Assert.False(NewsTopicClassifier.IsDirectTopic("oil,gold", "USDPKR"));
        Assert.True(NewsTopicClassifier.IsDirectTopic("oil,gold", "GOLDINT"));
        Assert.True(NewsTopicClassifier.IsDirectTopic("rupee-dollar", "USDPKR"));
        Assert.False(NewsTopicClassifier.IsDirectTopic("rupee-dollar", "GOLDPK"));
    }

    [Fact]
    public void MixedWording_ScoresBetweenTheTwo()
    {
        var mixed = NewsSentimentAnalyzer.Analyze("Stocks rebound after early losses", "");
        var clear = NewsSentimentAnalyzer.Analyze("Stocks rebound", "");

        Assert.InRange(mixed.Score, 1, clear.Score - 1);
        Assert.True(mixed.Confidence < clear.Confidence);
    }

    [Fact]
    public void HeadlineWithoutMarketWords_IsNeutralWithNoConfidence()
    {
        var result = NewsSentimentAnalyzer.Analyze("State Bank eyes open banking ecosystem", "");

        Assert.Equal("Neutral", result.Label);
        Assert.Equal(0, result.Confidence);
    }

    [Fact]
    public void Summary_IsConfidentOnlyWhenArticlesAgree()
    {
        var agreeing = NewsService.Summarize("KSE100", 7, Enumerable.Repeat((-80, 0.9, "Negative"), 5).ToList());
        var split = NewsService.Summarize("KSE100", 7, new List<(int, double, string)>
        {
            (-80, 0.9, "Negative"), (80, 0.9, "Positive"), (-60, 0.8, "Negative"), (70, 0.8, "Positive"),
        });

        Assert.Equal("Negative", agreeing.Label);
        Assert.Equal(5, agreeing.Negative);
        Assert.True(agreeing.Confidence > 70);
        Assert.True(split.Confidence < 40);
    }

    [Fact]
    public void Summary_WithNoArticles_IsNeutral()
    {
        var summary = NewsService.Summarize("GOLDINT", 7, new());

        Assert.Equal("Neutral", summary.Label);
        Assert.Equal(0, summary.Articles);
        Assert.Equal(0, summary.Confidence);
    }
}

public class RssNewsFetcherTests
{
    private const string Feed = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0"><channel>
          <item>
            <title>Refineries to sell petrol 'below cost'</title>
            <link>https://tribune.com.pk/story/1</link>
            <description>&lt;p&gt;Oil &amp;amp; gas firms &lt;b&gt;warn&lt;/b&gt;&lt;/p&gt;</description>
            <pubDate>Sun, 01 Nov 26 07:18:13 +0500</pubDate>
          </item>
          <item>
            <title>PSX: KSE-100 sheds over 1,100 points - Business Recorder</title>
            <link>https://news.google.com/rss/articles/abc</link>
            <description>PSX: KSE-100 sheds over 1,100 points Business Recorder</description>
            <pubDate>Fri, 30 Oct 2026 12:30:00 GMT</pubDate>
            <source url="https://www.brecorder.com">Business Recorder</source>
          </item>
          <item><title>No date</title><link>https://example.com/x</link></item>
        </channel></rss>
        """;

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/xml") });
    }

    private static RssNewsFetcher Fetcher() =>
        new(new HttpClient(new StubHandler(Feed)), NullLogger<RssNewsFetcher>.Instance);

    [Fact]
    public async Task PublisherFeed_ParsesTwoDigitYearsAndStripsHtml()
    {
        var items = await Fetcher().FetchAsync(new NewsFeed("Express Tribune", "https://tribune.com.pk/feed/business"));

        var first = items[0];
        Assert.Equal("Express Tribune", first.Source);
        Assert.Equal(new DateTime(2026, 11, 1, 2, 18, 13, DateTimeKind.Utc), first.PublishedAt);
        Assert.Equal("Oil & gas firms warn", first.Summary);
        Assert.Equal(2, items.Count); // the item without a date is skipped
    }

    [Theory]
    [InlineData("Live Gold Price in Pakistani Rupee (PKR)")]
    [InlineData("Gold price in Pakistan: Rates on October 1")]
    public void EvergreenPricePages_AreExcluded(string title)
    {
        Assert.Matches(RssNewsFetcher.Excluded, title);
    }

    [Fact]
    public async Task GoogleNewsSearch_UsesPublisherAndCleansTitle()
    {
        var items = await Fetcher().SearchAsync(RssNewsFetcher.InstrumentSearches[0]);

        var google = items.Single(i => i.Url.Contains("news.google.com"));
        Assert.Equal("Business Recorder", google.Source);
        Assert.Equal("PSX: KSE-100 sheds over 1,100 points", google.Title);
        Assert.Equal(string.Empty, google.Summary);
        Assert.Equal(new DateTime(2026, 10, 30, 12, 30, 0, DateTimeKind.Utc), google.PublishedAt);
    }

    [Theory]
    [InlineData("Phillips 66 $PSX Stock Holdings Cut by Profund Advisors LLC")]
    [InlineData("KMI 30 Index Today: Live Chart & Price (KMI30)")]
    [InlineData("KSE100PR - KSE 100 Price Action Index Constituents")]
    public void NonNewsAndForeignTickerResults_AreExcluded(string title)
    {
        Assert.Matches(RssNewsFetcher.Excluded, title);
    }

    [Fact]
    public void SearchResults_AreOnlyLinked_WhenTheTitleIsAboutTheInstrument()
    {
        var kse = RssNewsFetcher.InstrumentSearches.Single(s => s.Symbols.Contains("KSE100"));

        Assert.Matches(kse.Relevant, "PSX sheds 3,560 points amid panic selling");
        Assert.DoesNotMatch(kse.Relevant, "New Job Listing Signals Bigger Plans For Sony's Intelligent Pricing");
        Assert.DoesNotMatch(RssNewsFetcher.Excluded, "PSX sheds 3,560 points amid panic selling");
    }
}
