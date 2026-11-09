using System.Net;
using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Infrastructure.Data;
using MarketAnalytics.Infrastructure.News;

namespace MarketAnalytics.Worker;

/// <summary>
/// One-off download of older news, needed to test news-based signals. Searches Google News RSS one week at a time,
/// slowly, and stops rather than pushes on if the service starts refusing requests. Safe to run again: it re-tags
/// stored news with the current rules and skips stories it already has.
/// Run with: dotnet run -- --backfill-news 2021-05-01
/// </summary>
public class NewsBackfill
{
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromSeconds(1.5);
    // Funds and KMI-30 are covered through the Pakistan stocks topic of the KSE-100 search's results
    private static readonly string[] BackfillSymbols = { "KSE100", "USDPKR", "GOLDPK", "GOLDINT" };

    private readonly ApplicationDbContext _dbContext;
    private readonly RssNewsFetcher _fetcher;
    private readonly ILogger<NewsBackfill> _logger;

    public NewsBackfill(ApplicationDbContext dbContext, RssNewsFetcher fetcher, ILogger<NewsBackfill> logger)
    {
        _dbContext = dbContext;
        _fetcher = fetcher;
        _logger = logger;
    }

    public async Task RunAsync(DateTime from, CancellationToken cancellationToken = default)
    {
        await RetagStoredNewsAsync(cancellationToken);

        var searches = RssNewsFetcher.InstrumentSearches.Where(s => s.Symbols.Intersect(BackfillSymbols).Any()).ToList();
        var end = DateTime.UtcNow.Date;
        int totalAdded = 0;

        for (var weekStart = from.Date; weekStart < end; weekStart = weekStart.AddDays(7))
        {
            var weekEnd = weekStart.AddDays(7);
            foreach (var search in searches)
            {
                List<FeedItem> items;
                try
                {
                    items = await _fetcher.SearchRangeAsync(search, weekStart, weekEnd, cancellationToken);
                }
                catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or HttpStatusCode.Forbidden)
                {
                    _logger.LogError("News backfill stopped: the service refused a request ({Status}) at week {Week:yyyy-MM-dd}. Resume later from this date.", ex.StatusCode, weekStart);
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Skipped week {Week:yyyy-MM-dd} of search {Query}", weekStart, search.Query);
                    await Task.Delay(DelayBetweenRequests, cancellationToken);
                    continue;
                }

                // Search engines sometimes date pages outside the requested week; those dates can't be trusted
                var collected = items
                    .Where(i => i.PublishedAt >= weekStart && i.PublishedAt < weekEnd.AddDays(1))
                    .Select(i => new CollectedNews(i, search.Relevant.IsMatch(i.Title) ? search.Symbols : Array.Empty<string>(), false))
                    .ToList();

                var (_, added, _) = await NewsStore.SaveAsync(_dbContext, collected, weekStart.AddDays(-2), cancellationToken);
                totalAdded += added;
                await Task.Delay(DelayBetweenRequests, cancellationToken);
            }

            _logger.LogInformation("News backfill: reached {Week:yyyy-MM-dd}, {Added} articles added so far", weekEnd, totalAdded);
        }

        _logger.LogInformation("News backfill complete: {Added} articles added", totalAdded);
    }

    /// <summary>
    /// Applies the current topic and exclusion rules to already stored news, removing articles no longer relevant.
    /// </summary>
    private async Task RetagStoredNewsAsync(CancellationToken cancellationToken)
    {
        var articles = await _dbContext.NewsArticles.ToListAsync(cancellationToken);
        int changed = 0, removed = 0;

        foreach (var article in articles)
        {
            var topics = string.Join(",", NewsTopicClassifier.Classify(article.Title, article.Summary));
            if (RssNewsFetcher.Excluded.IsMatch(article.Title) || (topics.Length == 0 && article.Symbols.Length == 0))
            {
                _dbContext.NewsArticles.Remove(article);
                removed++;
            }
            else if (topics != article.Topics)
            {
                article.Topics = topics;
                changed++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Re-tagged stored news: {Changed} changed, {Removed} removed, of {Total}", changed, removed, articles.Count);
    }
}
