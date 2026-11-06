namespace MarketAnalytics.Domain.Entities;

/// <summary>
/// A business news article collected from a publisher's RSS feed, tagged with the market topics it covers.
/// </summary>
public class NewsArticle
{
    public int Id { get; set; }

    /// <summary>Publisher name, e.g. "Dawn".</summary>
    public string Source { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Plain-text summary from the feed (HTML removed).</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Link to the full article on the publisher's site. Unique.</summary>
    public string Url { get; set; } = string.Empty;

    public DateTime PublishedAt { get; set; }

    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Comma-separated topic keys (e.g. "pakistan-stocks,interest-rates"); instruments are linked through topics.
    /// </summary>
    public string Topics { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated symbols whose news search returned this article (e.g. "KSE100"); these instruments are
    /// linked directly, in addition to the instruments linked through topics.
    /// </summary>
    public string Symbols { get; set; } = string.Empty;
}
