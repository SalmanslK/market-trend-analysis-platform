using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Application.Interfaces.IRepositories;

/// <summary>
/// Repository interface for NewsArticle entity.
/// </summary>
public interface INewsRepository
{
    /// <summary>
    /// Articles published in [fromUtc, toUtc), newest first.
    /// </summary>
    Task<List<NewsArticle>> GetPublishedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publication time of the oldest stored article, or null if there is none.
    /// </summary>
    Task<DateTime?> GetOldestPublishedAtAsync(CancellationToken cancellationToken = default);
}
