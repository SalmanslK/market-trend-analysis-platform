using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Infrastructure.Data;
using MarketAnalytics.Infrastructure.Data.Configurations;
using MarketAnalytics.Infrastructure.News;

namespace MarketAnalytics.Worker;

/// <summary>
/// One collected news item and how it was found.
/// </summary>
public record CollectedNews(FeedItem Item, string[] Symbols, bool IsPublisherFeed);

/// <summary>
/// Stores collected news: drops non-news pages, tags topics, and merges the same story from different sources.
/// </summary>
public static class NewsStore
{
    /// <summary>
    /// Saves new stories published since <paramref name="since"/>; earlier stories can't be duplicates of them.
    /// </summary>
    public static async Task<(int Stories, int Added, int Updated)> SaveAsync(
        ApplicationDbContext dbContext,
        IEnumerable<CollectedNews> results,
        DateTime since,
        CancellationToken cancellationToken)
    {
        // The same story arrives from several feeds and searches with different links, so match on title and day.
        // Keep the publisher's direct link where there is one, and merge the instruments it was found for.
        var stories = results
            .Where(r => !RssNewsFetcher.Excluded.IsMatch(r.Item.Title))
            // A link too long to store can't be opened from the app anyway, and mustn't fail the whole batch
            .Where(r => r.Item.Url.Length <= NewsArticleConfiguration.MaxUrlLength)
            .GroupBy(r => StoryKey(r.Item.Title, r.Item.PublishedAt))
            .Select(g => new
            {
                Item = g.OrderByDescending(r => r.IsPublisherFeed).First().Item,
                Symbols = g.SelectMany(r => r.Symbols).Distinct().ToList(),
            })
            .ToList();

        var existing = await dbContext.NewsArticles.Where(n => n.PublishedAt >= since).ToListAsync(cancellationToken);
        var existingByKey = existing.GroupBy(n => StoryKey(n.Title, n.PublishedAt)).ToDictionary(g => g.Key, g => g.First());
        var existingUrls = existing.Select(n => n.Url).ToHashSet();

        var now = DateTime.UtcNow;
        int added = 0, updated = 0;

        foreach (var story in stories)
        {
            var key = StoryKey(story.Item.Title, story.Item.PublishedAt);
            if (existingByKey.TryGetValue(key, out var article) || existingUrls.Contains(story.Item.Url))
            {
                // A later search can link an already-stored story to more instruments
                if (article != null)
                {
                    var merged = string.Join(",", article.Symbols.Split(',', StringSplitOptions.RemoveEmptyEntries).Union(story.Symbols).OrderBy(s => s));
                    if (merged != article.Symbols)
                    {
                        article.Symbols = merged;
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
                Source = Shorten(story.Item.Source, NewsArticleConfiguration.MaxSourceLength),
                Title = Shorten(story.Item.Title, NewsArticleConfiguration.MaxTitleLength),
                Summary = story.Item.Summary,
                Url = story.Item.Url,
                PublishedAt = story.Item.PublishedAt,
                FetchedAt = now,
                Topics = string.Join(",", topics),
                Symbols = string.Join(",", story.Symbols.OrderBy(s => s)),
            };
            dbContext.NewsArticles.Add(newArticle);
            existingByKey[key] = newArticle;
            existingUrls.Add(newArticle.Url);
            added++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (stories.Count, added, updated);
    }

    private static string Shorten(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];

    /// <summary>
    /// Case- and punctuation-insensitive key for the same story from different sources. It includes the day
    /// because the same headline recurs over the years ("Gold price rises Rs1,200 per tola").
    /// </summary>
    public static string StoryKey(string title, DateTime publishedAt) =>
        $"{publishedAt:yyyyMMdd}:{new string(title.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray())}";
}
