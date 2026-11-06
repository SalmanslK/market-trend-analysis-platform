using MarketAnalytics.Application.DTOs;

namespace MarketAnalytics.Application.Interfaces.Services;

/// <summary>
/// Service interface for market news and its link to instruments' price moves.
/// </summary>
public interface INewsService
{
    IEnumerable<NewsTopicDto> GetTopics();

    /// <summary>
    /// Recent articles, newest first, optionally only those linked to one instrument or about one topic.
    /// </summary>
    Task<IEnumerable<NewsArticleDto>> GetNewsAsync(string? symbol, string? topic, int days, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// The instrument's biggest daily moves over the last <paramref name="days"/> days, each with the linked news
    /// published from the day before the move to the day after it.
    /// </summary>
    Task<InstrumentMovesDto?> GetBiggestMovesAsync(string symbol, int days, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Combined news sentiment per active instrument over the last <paramref name="days"/> days.
    /// </summary>
    Task<IEnumerable<NewsSentimentSummaryDto>> GetSentimentSummaryAsync(int days, CancellationToken cancellationToken = default);
}
