using Microsoft.Extensions.Logging;
using MarketAnalytics.Infrastructure.Data;

namespace MarketAnalytics.Infrastructure.News;

/// <summary>What one news update found.</summary>
public record NewsUpdateResult(int ItemsRead, int Stories, int Added, int Updated);

/// <summary>
/// Collects business news from the publisher feeds and the per-instrument searches. Used by the Worker every hour
/// and by the API when someone asks for a refresh. Only articles matching at least one market topic or search are
/// stored; feeds keep only recent items, so polling regularly is what builds up the news history.
/// </summary>
public class NewsUpdater
{
    private readonly ApplicationDbContext _dbContext;
    private readonly RssNewsFetcher _fetcher;
    private readonly ILogger<NewsUpdater> _logger;

    public NewsUpdater(ApplicationDbContext dbContext, RssNewsFetcher fetcher, ILogger<NewsUpdater> logger)
    {
        _dbContext = dbContext;
        _fetcher = fetcher;
        _logger = logger;
    }

    public async Task<NewsUpdateResult> UpdateAsync(CancellationToken cancellationToken = default)
    {
        // Publisher feeds (direct links, tagged by topic) and per-instrument searches (linked to their symbols)
        var results = new List<CollectedNews>();

        foreach (var feed in RssNewsFetcher.DefaultFeeds)
        {
            try
            {
                results.AddRange((await _fetcher.FetchAsync(feed, cancellationToken)).Select(i => new CollectedNews(i, Array.Empty<string>(), true)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One unavailable feed shouldn't stop the others
                _logger.LogWarning(ex, "Could not read news feed {Url}", feed.Url);
            }
        }

        foreach (var search in RssNewsFetcher.InstrumentSearches)
        {
            try
            {
                // Only link results whose title is actually about the instrument; others can still be stored via topics
                results.AddRange((await _fetcher.SearchAsync(search, cancellationToken))
                    .Select(i => new CollectedNews(i, search.Relevant.IsMatch(i.Title) ? search.Symbols : Array.Empty<string>(), false)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not run news search {Query}", search.Query);
            }
        }

        // Feeds and searches only reach back ~30 days, so only recent stored articles can be duplicates
        var (stories, added, updated) = await NewsStore.SaveAsync(_dbContext, results, DateTime.UtcNow.AddDays(-45), cancellationToken);

        _logger.LogInformation("News update: {Read} items read, {Stories} distinct stories, {Added} new articles stored, {Updated} linked to more instruments",
            results.Count, stories, added, updated);
        return new NewsUpdateResult(results.Count, stories, added, updated);
    }
}
