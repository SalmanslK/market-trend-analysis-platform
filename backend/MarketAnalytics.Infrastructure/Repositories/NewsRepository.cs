using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Infrastructure.Data;

namespace MarketAnalytics.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for NewsArticle entity.
/// </summary>
public class NewsRepository : INewsRepository
{
    private readonly ApplicationDbContext _context;

    public NewsRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<NewsArticle>> GetPublishedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return await _context.NewsArticles
            .AsNoTracking()
            .Where(n => n.PublishedAt >= fromUtc && n.PublishedAt < toUtc)
            .OrderByDescending(n => n.PublishedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<DateTime?> GetOldestPublishedAtAsync(CancellationToken cancellationToken = default)
    {
        return await _context.NewsArticles
            .OrderBy(n => n.PublishedAt)
            .Select(n => (DateTime?)n.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
