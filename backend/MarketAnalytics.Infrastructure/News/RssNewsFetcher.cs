using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace MarketAnalytics.Infrastructure.News;

/// <summary>
/// A news feed to collect, e.g. ("Dawn", "https://www.dawn.com/feeds/business").
/// </summary>
public record NewsFeed(string Source, string Url);

/// <summary>
/// A per-instrument news search. Results are linked directly to <see cref="Symbols"/> only when their title
/// matches <see cref="Relevant"/>: search engines match loosely (e.g. "PSX" is also Phillips 66's US ticker).
/// </summary>
public record InstrumentNewsSearch(string Query, string[] Symbols, Regex Relevant);

/// <summary>
/// One item read from an RSS feed, before tagging.
/// </summary>
public record FeedItem(string Source, string Title, string Summary, string Url, DateTime PublishedAt);

/// <summary>
/// Reads business news from publishers' RSS feeds (free, keyless, published for automated readers).
/// Feeds only carry recent items (days to weeks), so the stored archive builds up over time.
/// </summary>
public class RssNewsFetcher
{
    public static readonly IReadOnlyList<NewsFeed> DefaultFeeds = new[]
    {
        new NewsFeed("Dawn", "https://www.dawn.com/feeds/business"),
        new NewsFeed("Business Recorder", "https://www.brecorder.com/feeds/latest-news"),
        new NewsFeed("Business Recorder", "https://www.brecorder.com/feeds/markets"),
        new NewsFeed("Express Tribune", "https://tribune.com.pk/feed/business"),
    };

    /// <summary>
    /// Google News search queries per instrument. Individual Meezan funds are rarely named in the news,
    /// so the funds share one broader query (they also follow the stock market through topics).
    /// </summary>
    public static readonly IReadOnlyList<InstrumentNewsSearch> InstrumentSearches = new[]
    {
        new InstrumentNewsSearch("\"KSE-100\" OR \"KSE 100\" OR \"Pakistan Stock Exchange\" OR PSX Pakistan", new[] { "KSE100" },
            Pattern(@"\b(KSE|PSX|Pakistan Stock Exchange|bourse)\b")),
        new InstrumentNewsSearch("\"KMI-30\" OR \"KMI 30\" OR \"Islamic index\" PSX", new[] { "KMI30" },
            Pattern(@"\b(KMI|PSX|KSE|Islamic index|Shariah)\b")),
        new InstrumentNewsSearch("Meezan OR \"mutual funds\" Pakistan OR MUFAP", new[] { "KMIF", "MIF", "MGF", "MBF" },
            Pattern(@"\b(Meezan|mutual funds?|MUFAP|asset management|AMCs?)\b")),
        new InstrumentNewsSearch("rupee dollar interbank OR \"open market\" rupee", new[] { "USDPKR" },
            Pattern(@"\b(rupee|PKR|dollar|interbank|exchange rate)\b")),
        new InstrumentNewsSearch("gold price Pakistan tola", new[] { "GOLDPK" },
            Pattern(@"\bgold\b")),
        new InstrumentNewsSearch("gold price ounce OR bullion OR spot gold", new[] { "GOLDINT" },
            Pattern(@"\b(gold|bullion|XAU)\b")),
    };

    /// <summary>
    /// Items never worth storing: other markets' "PSX" (Phillips 66 on the NYSE) and evergreen price pages
    /// such as "KMI 30 Index Today: Live Chart &amp; Price".
    /// </summary>
    public static readonly Regex Excluded = Pattern(
        @"Phillips 66|\$PSX|NYSE|Live Chart|Index Today|Price Today|Constituents|Historical Data|News & Updates|Market Roundup$|Live Gold Price|Live Price|Rates on ");

    private static Regex Pattern(string pattern) => new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Google News RSS search, limited to the last 30 days and Pakistan English editions
    private const string GoogleNewsSearchUrl = "https://news.google.com/rss/search?q={0}+when:30d&hl=en-PK&gl=PK&ceid=PK:en";

    // The same search limited to a date range, for downloading older news
    private const string GoogleNewsRangeUrl = "https://news.google.com/rss/search?q={0}+after:{1:yyyy-MM-dd}+before:{2:yyyy-MM-dd}&hl=en-PK&gl=PK&ceid=PK:en";

    private const int MaxSummaryLength = 600;

    // RFC 822 dates, with 4- or 2-digit years ("Sun, 01 Nov 2026 17:04:34 +0500" / "Sun, 01 Nov 26 07:18:13 +0500")
    private static readonly string[] DateFormats =
    {
        "ddd, dd MMM yyyy HH:mm:ss zzz", "ddd, d MMM yyyy HH:mm:ss zzz",
        "ddd, dd MMM yy HH:mm:ss zzz", "ddd, d MMM yy HH:mm:ss zzz",
    };

    private static readonly Regex HtmlTags = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    // .NET's "zzz" expects "+05:00"; feeds use "+0500"
    private static readonly Regex CompactOffset = new(@"([+-]\d{2})(\d{2})$", RegexOptions.Compiled);
    // Google News and others use named UTC zones ("... 12:30:00 GMT")
    private static readonly Regex UtcZoneName = new(@"\s(GMT|UTC|UT|Z)$", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly ILogger<RssNewsFetcher> _logger;

    public RssNewsFetcher(HttpClient httpClient, ILogger<RssNewsFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<List<FeedItem>> FetchAsync(NewsFeed feed, CancellationToken cancellationToken = default) =>
        ReadFeedAsync(feed.Url, feed.Source, cancellationToken);

    /// <summary>
    /// Runs a Google News RSS search. Each result names its publisher in a &lt;source&gt; element.
    /// </summary>
    public Task<List<FeedItem>> SearchAsync(InstrumentNewsSearch search, CancellationToken cancellationToken = default) =>
        ReadFeedAsync(string.Format(GoogleNewsSearchUrl, Uri.EscapeDataString(search.Query)), defaultSource: null, cancellationToken);

    /// <summary>
    /// Runs a Google News RSS search limited to stories between two dates.
    /// </summary>
    public Task<List<FeedItem>> SearchRangeAsync(InstrumentNewsSearch search, DateTime from, DateTime to, CancellationToken cancellationToken = default) =>
        ReadFeedAsync(string.Format(GoogleNewsRangeUrl, Uri.EscapeDataString(search.Query), from, to), defaultSource: null, cancellationToken);

    private async Task<List<FeedItem>> ReadFeedAsync(string feedUrl, string? defaultSource, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);
        request.Headers.UserAgent.ParseAdd("MarketAnalytics/1.0 (RSS reader)");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var items = new List<FeedItem>();

        foreach (var item in document.Descendants("item"))
        {
            var source = defaultSource ?? Clean(item.Element("source")?.Value);
            var title = Clean(item.Element("title")?.Value);

            // Google News titles end with " - Publisher"; drop it so the same story matches across sources
            if (defaultSource == null && source.Length > 0 && title.EndsWith($" - {source}", StringComparison.Ordinal))
                title = title[..^(source.Length + 3)].TrimEnd();
            var url = item.Element("link")?.Value.Trim();
            var publishedAt = ParseDate(item.Element("pubDate")?.Value);

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(url) || publishedAt == null)
                continue;

            var summary = Clean(item.Element("description")?.Value);
            // Google News descriptions just repeat the title and publisher
            if (defaultSource == null && summary.StartsWith(title, StringComparison.Ordinal))
                summary = string.Empty;
            if (summary.Length > MaxSummaryLength)
                summary = summary[..MaxSummaryLength].TrimEnd() + "…";

            items.Add(new FeedItem(string.IsNullOrEmpty(source) ? "Unknown" : source, title, summary, url, publishedAt.Value));
        }

        _logger.LogInformation("Read {Count} items from {Url}", items.Count, feedUrl);
        return items;
    }

    private static string Clean(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;
        var text = WebUtility.HtmlDecode(HtmlTags.Replace(html, " "));
        return Whitespace.Replace(text, " ").Trim();
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = CompactOffset.Replace(UtcZoneName.Replace(value.Trim(), " +00:00"), "$1:$2");
        return DateTimeOffset.TryParseExact(normalized, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.UtcDateTime
            : null;
    }
}
