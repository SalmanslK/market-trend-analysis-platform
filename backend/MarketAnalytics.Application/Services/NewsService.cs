using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// One article's sentiment for one instrument.
/// </summary>
public record ScoredNews(DateTime Date, int Score, double Weight, string Label);

/// <summary>
/// Links stored news to instruments and their price moves.
///
/// An article is linked to an instrument when the instrument's news search found it, or when it covers a topic
/// that affects the instrument (see <see cref="NewsTopicClassifier"/>). Dates are matched in Pakistan time:
/// business news often reports a session after the close or over the weekend, so an article is matched to the
/// latest trading day on or before its publication date.
/// </summary>
public class NewsService : INewsService
{
    // Pakistan Standard Time (no daylight saving)
    private static readonly TimeSpan PakistanOffset = TimeSpan.FromHours(5);
    private const int MaxArticlesPerMove = 8;

    private readonly INewsRepository _newsRepository;
    private readonly IInstrumentRepository _instrumentRepository;
    private readonly IPricePointRepository _pricePointRepository;
    private readonly IFundPayoutRepository? _fundPayoutRepository;

    public NewsService(
        INewsRepository newsRepository,
        IInstrumentRepository instrumentRepository,
        IPricePointRepository pricePointRepository,
        IFundPayoutRepository? fundPayoutRepository = null)
    {
        _fundPayoutRepository = fundPayoutRepository;
        _newsRepository = newsRepository;
        _instrumentRepository = instrumentRepository;
        _pricePointRepository = pricePointRepository;
    }

    public IEnumerable<NewsTopicDto> GetTopics() => NewsTopicClassifier.Topics.Select(ToDto);

    public async Task<IEnumerable<NewsArticleDto>> GetNewsAsync(
        string? symbol, string? topic, int days, int limit, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var articles = await _newsRepository.GetPublishedBetweenAsync(now.AddDays(-days), now.AddDays(1), cancellationToken);

        var filtered = articles
            .Where(a => symbol == null || LinkedSymbols(a).Contains(symbol, StringComparer.OrdinalIgnoreCase))
            .Where(a => topic == null || NewsTopicClassifier.ParseTopics(a.Topics).Contains(topic))
            .Take(limit)
            .ToList();

        var prices = await LoadDailyPricesAsync(now.AddDays(-days - 10), cancellationToken);
        return filtered.Select(a => ToDto(a, prices)).ToList();
    }

    public async Task<InstrumentMovesDto?> GetBiggestMovesAsync(string symbol, int days, int count, CancellationToken cancellationToken = default)
    {
        var instrument = await _instrumentRepository.GetBySymbolAsync(symbol, cancellationToken);
        if (instrument == null)
            return null;

        var now = DateTime.UtcNow;
        var yearAgo = now.AddYears(-1).AddDays(-7);
        var prices = await PriceHistory.LoadDailyAsync(_pricePointRepository, _fundPayoutRepository, instrument.Id, yearAgo, cancellationToken);

        var changes = prices.Skip(1)
            .Select((p, i) => (Date: p.DateTime, Price: p.Price, Change: (p.Price / prices[i].Price - 1) * 100, Index: i + 1))
            .ToList();

        var result = new InstrumentMovesDto
        {
            Symbol = instrument.Symbol,
            TypicalDailyMove = Median(changes.Select(c => Math.Abs(c.Change)).Where(c => c > 0).ToList()),
            NewsAvailableSince = await _newsRepository.GetOldestPublishedAtAsync(cancellationToken),
        };

        var windowStart = now.Date.AddDays(-days);
        var biggest = changes
            .Where(c => c.Date >= windowStart)
            .OrderByDescending(c => Math.Abs(c.Change))
            .Take(count)
            .ToList();

        if (biggest.Count == 0)
            return result;

        // News from the day before the move until the next trading day (so weekend reports of a Friday move count)
        var moves = biggest.Select(c =>
        {
            var next = c.Index + 1 < prices.Count ? prices[c.Index + 1].DateTime : (DateTime?)null;
            var from = c.Date.AddDays(-1);
            var to = next.HasValue && next.Value > c.Date.AddDays(2) ? next.Value : c.Date.AddDays(2);
            return new PriceMoveDto
            {
                Date = c.Date,
                Price = c.Price,
                Change = c.Change,
                TimesTypical = result.TypicalDailyMove is > 0 ? Math.Abs(c.Change) / result.TypicalDailyMove : null,
                // Window boundaries are midnight Pakistan time
                NewsFrom = DateTime.SpecifyKind(from - PakistanOffset, DateTimeKind.Utc),
                NewsTo = DateTime.SpecifyKind(to - PakistanOffset, DateTimeKind.Utc),
            };
        }).ToList();

        var articles = await _newsRepository.GetPublishedBetweenAsync(moves.Min(m => m.NewsFrom), moves.Max(m => m.NewsTo), cancellationToken);
        var linked = articles.Where(a => LinkedSymbols(a).Contains(instrument.Symbol, StringComparer.OrdinalIgnoreCase)).ToList();
        var allPrices = await LoadDailyPricesAsync(moves.Min(m => m.NewsFrom).AddDays(-10), cancellationToken);

        foreach (var move in moves)
        {
            move.Articles = linked
                .Where(a => a.PublishedAt >= move.NewsFrom && a.PublishedAt < move.NewsTo)
                // Articles directly about the instrument first, then background news, newest first
                .OrderByDescending(a => IsDirect(a, instrument.Symbol))
                .ThenByDescending(a => a.PublishedAt)
                .Take(MaxArticlesPerMove)
                .Select(a => ToDto(a, allPrices))
                .ToList();
        }

        result.Moves = moves;
        return result;
    }

    private async Task<Dictionary<string, List<PriceDataPoint>>> LoadDailyPricesAsync(DateTime fromUtc, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, List<PriceDataPoint>>(StringComparer.OrdinalIgnoreCase);
        foreach (var instrument in await _instrumentRepository.GetActiveInstrumentsAsync(cancellationToken))
        {
            result[instrument.Symbol] = await PriceHistory.LoadDailyAsync(_pricePointRepository, _fundPayoutRepository, instrument.Id, fromUtc, cancellationToken);
        }
        return result;
    }

    public async Task<IEnumerable<NewsSentimentSummaryDto>> GetSentimentSummaryAsync(int days, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var articles = await _newsRepository.GetPublishedBetweenAsync(now.AddDays(-days), now.AddDays(1), cancellationToken);
        var instruments = await _instrumentRepository.GetActiveInstrumentsAsync(cancellationToken);

        return instruments.Select(instrument =>
        {
            var linked = articles.Where(a => LinkedSymbols(a).Contains(instrument.Symbol, StringComparer.OrdinalIgnoreCase)).ToList();
            var scored = linked
                .Where(a => IsDirect(a, instrument.Symbol))
                .Select(a =>
                {
                    var sentiment = NewsSentimentAnalyzer.Analyze(a.Title, a.Summary);
                    var score = sentiment.Score * NewsSentimentAnalyzer.DirectionFor(instrument.Symbol, a.Title);
                    return (Score: score, Weight: sentiment.Confidence / 100.0, Label: NewsSentimentAnalyzer.LabelFor(score));
                })
                .ToList();

            var summary = Summarize(instrument.Symbol, days, scored);
            summary.BackgroundArticles = linked.Count - scored.Count;
            return summary;
        }).ToList();
    }

    /// <summary>
    /// Combines article scores into one view: a confidence-weighted average score, with a confidence that grows
    /// with the amount of evidence and with how consistently the positive/negative articles point the same way.
    /// </summary>
    public static NewsSentimentSummaryDto Summarize(string symbol, int days, List<(int Score, double Weight, string Label)> scored)
    {
        var summary = new NewsSentimentSummaryDto
        {
            Symbol = symbol,
            Days = days,
            Articles = scored.Count,
            Positive = scored.Count(s => s.Label == "Positive"),
            Neutral = scored.Count(s => s.Label == "Neutral"),
            Negative = scored.Count(s => s.Label == "Negative"),
        };

        var totalWeight = scored.Sum(s => s.Weight);
        if (totalWeight == 0)
            return summary;

        var mean = scored.Sum(s => s.Score * s.Weight) / totalWeight;
        var directional = summary.Positive + summary.Negative;
        var agreeing = mean >= 0 ? summary.Positive : summary.Negative;
        var consistency = directional == 0 ? 0 : (double)agreeing / directional;
        var evidence = 1 - Math.Exp(-totalWeight / 3);

        summary.Score = (int)Math.Round(mean);
        summary.Confidence = (int)Math.Round(consistency * evidence * 100);
        summary.Label = NewsSentimentAnalyzer.LabelFor(summary.Score);
        return summary;
    }

    /// <summary>
    /// An instrument's direct news with its sentiment from the instrument's point of view, by Pakistan-time date.
    /// </summary>
    public static List<ScoredNews> ScoreForSymbol(IEnumerable<(NewsArticle Article, SentimentResult Sentiment)> articles, string symbol) =>
        articles
            .Where(a => IsDirect(a.Article, symbol))
            .Select(a =>
            {
                var score = a.Sentiment.Score * NewsSentimentAnalyzer.DirectionFor(symbol, a.Article.Title);
                return new ScoredNews((a.Article.PublishedAt + PakistanOffset).Date, score, a.Sentiment.Confidence / 100.0, NewsSentimentAnalyzer.LabelFor(score));
            })
            .OrderBy(n => n.Date)
            .ToList();

    /// <summary>
    /// The news mood on each date, using only articles from that date and the days before it, the same way as the
    /// live news mood. Sorted dates are expected.
    /// </summary>
    public static List<NewsSentimentSummaryDto> DailyMood(IReadOnlyList<DateTime> dates, IReadOnlyList<ScoredNews> news, int windowDays)
    {
        var moods = new List<NewsSentimentSummaryDto>(dates.Count);
        int first = 0, last = 0;

        foreach (var date in dates)
        {
            var day = date.Date;
            while (last < news.Count && news[last].Date <= day)
                last++;
            while (first < last && news[first].Date <= day.AddDays(-windowDays))
                first++;

            var window = new List<(int Score, double Weight, string Label)>(last - first);
            for (int i = first; i < last; i++)
                window.Add((news[i].Score, news[i].Weight, news[i].Label));
            moods.Add(Summarize(string.Empty, windowDays, window));
        }
        return moods;
    }

    private static NewsArticleDto ToDto(NewsArticle article, Dictionary<string, List<PriceDataPoint>> prices)
    {
        var publishedDate = (article.PublishedAt + PakistanOffset).Date;
        var sentiment = NewsSentimentAnalyzer.Analyze(article.Title, article.Summary);

        return new NewsArticleDto
        {
            Id = article.Id,
            Source = article.Source,
            Title = article.Title,
            Summary = article.Summary,
            Url = article.Url,
            PublishedAt = article.PublishedAt,
            Topics = NewsTopicClassifier.ParseTopics(article.Topics)
                .Select(k => ToDto(NewsTopicClassifier.GetTopic(k)!))
                .ToList(),
            Sentiment = new SentimentDto
            {
                Score = sentiment.Score,
                Confidence = sentiment.Confidence,
                Label = sentiment.Label,
                PositiveWords = sentiment.PositiveWords,
                NegativeWords = sentiment.NegativeWords,
            },
            Instruments = LinkedSymbols(article)
                .OrderBy(s => s)
                .Select(symbol =>
                {
                    var change = ChangeOnTradingDay(symbol, publishedDate, prices);
                    change.IsDirect = IsDirect(article, symbol);
                    change.SentimentScore = change.IsDirect ? sentiment.Score * NewsSentimentAnalyzer.DirectionFor(symbol, article.Title) : 0;
                    change.SentimentLabel = change.IsDirect ? NewsSentimentAnalyzer.LabelFor(change.SentimentScore) : "Background";
                    return change;
                })
                .ToList(),
        };
    }

    /// <summary>
    /// The instrument's % change on the latest trading day on or before <paramref name="date"/>.
    /// </summary>
    private static InstrumentChangeDto ChangeOnTradingDay(string symbol, DateTime date, Dictionary<string, List<PriceDataPoint>> prices)
    {
        var dto = new InstrumentChangeDto { Symbol = symbol };
        if (!prices.TryGetValue(symbol, out var series))
            return dto;

        var index = series.FindLastIndex(p => p.DateTime.Date <= date);
        if (index < 1)
            return dto;

        dto.TradingDate = series[index].DateTime;
        dto.Change = (series[index].Price / series[index - 1].Price - 1) * 100;
        return dto;
    }

    private static IEnumerable<string> LinkedSymbols(NewsArticle article) =>
        SplitSymbols(article.Symbols)
            .Concat(NewsTopicClassifier.ParseTopics(article.Topics).SelectMany(k => NewsTopicClassifier.GetTopic(k)!.Symbols))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Directly about the instrument: found by its own news search, or on a topic that is directly about it.
    /// </summary>
    public static bool IsDirect(NewsArticle article, string symbol) =>
        SplitSymbols(article.Symbols).Contains(symbol, StringComparer.OrdinalIgnoreCase)
        || NewsTopicClassifier.IsDirectTopic(article.Topics, symbol);

    private static string[] SplitSymbols(string symbols) =>
        symbols.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static NewsTopicDto ToDto(NewsTopic topic) => new()
    {
        Key = topic.Key,
        Label = topic.Label,
        WhyItMatters = topic.WhyItMatters,
        Symbols = topic.Symbols.ToList(),
    };

    private static decimal? Median(List<decimal> values)
    {
        if (values.Count == 0)
            return null;
        var sorted = values.OrderBy(v => v).ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
