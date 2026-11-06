namespace MarketAnalytics.Application.DTOs;

public class NewsTopicDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string WhyItMatters { get; set; } = string.Empty;
    public List<string> Symbols { get; set; } = new();
}

public class NewsArticleDto
{
    public int Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public List<NewsTopicDto> Topics { get; set; } = new();
    public SentimentDto Sentiment { get; set; } = new();

    /// <summary>
    /// Each linked instrument's % change on the trading day the article covers
    /// (the latest trading day on or before its publication date, Pakistan time). Null if unknown.
    /// </summary>
    public List<InstrumentChangeDto> Instruments { get; set; } = new();
}

public class InstrumentChangeDto
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime? TradingDate { get; set; }
    public decimal? Change { get; set; }
    /// <summary>
    /// True when the article is directly about this instrument (found by its news search or on its main topic).
    /// Otherwise it is background context and its tone is not applied to the instrument.
    /// </summary>
    public bool IsDirect { get; set; }
    /// <summary>The article's sentiment for this instrument's price (flipped for USD/PKR when the rupee is the subject); 0 for background articles.</summary>
    public int SentimentScore { get; set; }
    /// <summary>Positive, Neutral, Negative, or Background when the article is only context for this instrument.</summary>
    public string SentimentLabel { get; set; } = "Neutral";
}

/// <summary>
/// One of an instrument's biggest daily moves, with the news published around it.
/// </summary>
public class PriceMoveDto
{
    public DateTime Date { get; set; }
    public decimal Price { get; set; }
    /// <summary>% change from the previous trading day.</summary>
    public decimal Change { get; set; }
    /// <summary>How many times bigger than the instrument's typical (median) daily move over the last year.</summary>
    public decimal? TimesTypical { get; set; }
    public DateTime NewsFrom { get; set; }
    public DateTime NewsTo { get; set; }
    public List<NewsArticleDto> Articles { get; set; } = new();
}

public class InstrumentMovesDto
{
    public string Symbol { get; set; } = string.Empty;
    /// <summary>Median absolute daily % change over the last year: what a "normal day" looks like.</summary>
    public decimal? TypicalDailyMove { get; set; }
    /// <summary>Oldest stored article; moves before this date can't have news.</summary>
    public DateTime? NewsAvailableSince { get; set; }
    public List<PriceMoveDto> Moves { get; set; } = new();
}

/// <summary>
/// Tone of an article's wording: Score -100 (negative) to +100 (positive), Confidence 0-100.
/// </summary>
public class SentimentDto
{
    public int Score { get; set; }
    public int Confidence { get; set; }
    /// <summary>Positive, Neutral or Negative.</summary>
    public string Label { get; set; } = "Neutral";
    public List<string> PositiveWords { get; set; } = new();
    public List<string> NegativeWords { get; set; } = new();
}

/// <summary>
/// Combined sentiment of all news linked to one instrument over a period, from the instrument's point of view.
/// </summary>
public class NewsSentimentSummaryDto
{
    public string Symbol { get; set; } = string.Empty;
    public int Days { get; set; }
    /// <summary>Articles directly about the instrument; only these are scored.</summary>
    public int Articles { get; set; }
    /// <summary>Wider news (interest rates, IMF, oil...) shown for context but not scored.</summary>
    public int BackgroundArticles { get; set; }
    public int Positive { get; set; }
    public int Neutral { get; set; }
    public int Negative { get; set; }
    /// <summary>Confidence-weighted average score, -100 to +100.</summary>
    public int Score { get; set; }
    /// <summary>0-100: higher with more articles that agree with each other.</summary>
    public int Confidence { get; set; }
    public string Label { get; set; } = "Neutral";
}
