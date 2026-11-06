using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.Entities;
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
        var results = new List<(FeedItem Item, string[] Symbols, bool IsPublisherFeed)>();

        foreach (var feed in RssNewsFetcher.DefaultFeeds)
        {
            try
            {
                results.AddRange((await fetcher.FetchAsync(feed, cancellationToken)).Select(i => (i, Array.Empty<string>(), true)));
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
                    .Select(i => (i, search.Relevant.IsMatch(i.Title) ? search.Symbols : Array.Empty<string>(), false)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not run news search {Query}", search.Query);
            }
        }

        // The same story arrives from several feeds and searches with different links, so match on the title.
        // Keep the publisher's direct link where there is one, and merge the instruments it was found for.
        var stories = results
            .Where(r => !RssNewsFetcher.Excluded.IsMatch(r.Item.Title))
            .GroupBy(r => TitleKey(r.Item.Title))
            .Select(g => new
            {
                Item = g.OrderByDescending(r => r.IsPublisherFeed).First().Item,
                Symbols = g.SelectMany(r => r.Symbols).Distinct().ToList(),
            })
            .ToList();

        // Feeds and searches only reach back ~30 days, so only recent stored articles can be duplicates
        var since = DateTime.UtcNow.AddDays(-45);
        var existing = await dbContext.NewsArticles.Where(n => n.PublishedAt >= since).ToListAsync(cancellationToken);
        var existingByTitle = existing.GroupBy(n => TitleKey(n.Title)).ToDictionary(g => g.Key, g => g.First());
        var existingUrls = existing.Select(n => n.Url).ToHashSet();

        var now = DateTime.UtcNow;
        int added = 0, updated = 0;

        foreach (var story in stories)
        {
            if (existingByTitle.TryGetValue(TitleKey(story.Item.Title), out var article) || existingUrls.Contains(story.Item.Url))
            {
                // A later search can link an already-stored story to more instruments
                if (article != null)
                {
                    var merged = article.Symbols.Split(',', StringSplitOptions.RemoveEmptyEntries).Union(story.Symbols).OrderBy(s => s).ToList();
                    var mergedText = string.Join(",", merged);
                    if (mergedText != article.Symbols)
                    {
                        article.Symbols = mergedText;
                        updated++;
                    }
                }
                continue;
            }

            var topics = NewsTopicClassifier.Classify(story.Item.Title, story.Item.Summary);
            if (topics.Count == 0 && story.Symbols.Count == 0)
                continue;

            var newArticle = new NewsArticle
            {
                Source = story.Item.Source,
                Title = story.Item.Title,
                Summary = story.Item.Summary,
                Url = story.Item.Url,
                PublishedAt = story.Item.PublishedAt,
                FetchedAt = now,
                Topics = string.Join(",", topics),
                Symbols = string.Join(",", story.Symbols.OrderBy(s => s)),
            };
            dbContext.NewsArticles.Add(newArticle);
            existingByTitle[TitleKey(newArticle.Title)] = newArticle;
            existingUrls.Add(newArticle.Url);
            added++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("News update: {Read} items read, {Stories} distinct stories, {Added} new articles stored, {Updated} linked to more instruments",
            results.Count, stories.Count, added, updated);
    }

    /// <summary>Case- and punctuation-insensitive title key for spotting the same story from different sources.</summary>
    private static string TitleKey(string title) =>
        new string(title.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
