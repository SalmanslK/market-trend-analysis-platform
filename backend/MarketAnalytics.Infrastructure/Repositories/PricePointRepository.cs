using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Infrastructure.Data;

namespace MarketAnalytics.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for PricePoint entity.
/// </summary>
public class PricePointRepository : IPricePointRepository
{
    private readonly ApplicationDbContext _context;

    public PricePointRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<PricePoint>> GetByInstrumentIdAsync(
        int instrumentId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PricePoints
            .Where(p => p.InstrumentId == instrumentId);

        if (startDate.HasValue)
        {
            query = query.Where(p => p.DateTime >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(p => p.DateTime <= endDate.Value);
        }

        return await query
            .OrderBy(p => p.DateTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<DateTime?> GetLatestDateAsync(int instrumentId, CancellationToken cancellationToken = default)
    {
        var latestPoint = await _context.PricePoints
            .Where(p => p.InstrumentId == instrumentId)
            .OrderByDescending(p => p.DateTime)
            .FirstOrDefaultAsync(cancellationToken);

        return latestPoint?.DateTime;
    }

    public async Task AddRangeAsync(IEnumerable<PricePoint> pricePoints, CancellationToken cancellationToken = default)
    {
        await _context.PricePoints.AddRangeAsync(pricePoints, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BulkUpsertAsync(IEnumerable<PricePoint> pricePoints, CancellationToken cancellationToken = default)
    {
        var pricePointsList = pricePoints.ToList();
        
        foreach (var pricePoint in pricePointsList)
        {
            var existing = await _context.PricePoints
                .FirstOrDefaultAsync(
                    p => p.InstrumentId == pricePoint.InstrumentId && 
                         p.DateTime == pricePoint.DateTime, 
                    cancellationToken);

            if (existing != null)
            {
                // Update existing (matches Python: drop_duplicates(keep="last"))
                existing.Price = pricePoint.Price;
                existing.CreatedAt = pricePoint.CreatedAt;
            }
            else
            {
                // Add new
                await _context.PricePoints.AddAsync(pricePoint, cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<PricePoint>> GetLatestForAllInstrumentsAsync(CancellationToken cancellationToken = default)
    {
        // Get the latest price point for each instrument
        var latestDates = await _context.PricePoints
            .GroupBy(p => p.InstrumentId)
            .Select(g => new { InstrumentId = g.Key, LatestDateTime = g.Max(p => p.DateTime) })
            .ToListAsync(cancellationToken);

        var result = new List<PricePoint>();
        foreach (var latest in latestDates)
        {
            var latestPoint = await _context.PricePoints
                .FirstOrDefaultAsync(
                    p => p.InstrumentId == latest.InstrumentId && 
                         p.DateTime == latest.LatestDateTime, 
                    cancellationToken);

            if (latestPoint != null)
            {
                result.Add(latestPoint);
            }
        }

        return result;
    }
}