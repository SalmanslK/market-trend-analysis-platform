using MarketAnalytics.Infrastructure.Data;
using MarketAnalytics.Infrastructure.News;

namespace MarketAnalytics.Worker;

/// <summary>
/// Background service that collects business news from RSS feeds every hour.
/// Only articles matching at least one market topic are stored; feeds keep only recent items,
/// so polling regularly is what builds up the news history.
/// </summary>
public class NewsUpdateBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NewsUpdateBackgroundService> _logger;
    private readonly TimeSpan _updateInterval = TimeSpan.FromHours(1);

    public NewsUpdateBackgroundService(IServiceProvider serviceProvider, ILogger<NewsUpdateBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("News Update Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateNewsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during scheduled news update");
            }

            await Task.Delay(_updateInterval, stoppingToken);
        }
    }

    private async Task UpdateNewsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fetcher = scope.ServiceProvider.GetRequiredService<RssNewsFetcher>();

        // Publisher feeds (direct links, tagged by topic) and per-instrument searches (linked to their symbols)
        var results = new List<CollectedNews>();

        foreach (var feed in RssNewsFetcher.DefaultFeeds)
        {
            try
            {
                results.AddRange((await fetcher.FetchAsync(feed, cancellationToken)).Select(i => new CollectedNews(i, Array.Empty<string>(), true)));
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
                results.AddRange((await fetcher.SearchAsync(search, cancellationToken))
                    .Select(i => new CollectedNews(i, search.Relevant.IsMatch(i.Title) ? search.Symbols : Array.Empty<string>(), false)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not run news search {Query}", search.Query);
            }
        }

        // Feeds and searches only reach back ~30 days, so only recent stored articles can be duplicates
        var (stories, added, updated) = await NewsStore.SaveAsync(dbContext, results, DateTime.UtcNow.AddDays(-45), cancellationToken);

        _logger.LogInformation("News update: {Read} items read, {Stories} distinct stories, {Added} new articles stored, {Updated} linked to more instruments",
            results.Count, stories, added, updated);
    }
}
