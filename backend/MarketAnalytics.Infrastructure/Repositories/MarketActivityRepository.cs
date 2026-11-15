using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Infrastructure.Data;

namespace MarketAnalytics.Infrastructure.Repositories;

public class MarketActivityRepository : IMarketActivityRepository
{
    private readonly ApplicationDbContext _context;

    public MarketActivityRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<List<TradingVolume>> GetVolumesAsync(int instrumentId, DateTime since, CancellationToken cancellationToken = default) =>
        _context.TradingVolumes.AsNoTracking()
            .Where(v => v.InstrumentId == instrumentId && v.Date >= since)
            .OrderBy(v => v.Date)
            .ToListAsync(cancellationToken);

    public Task<List<FundAssets>> GetFundSizesAsync(int instrumentId, CancellationToken cancellationToken = default) =>
        _context.FundAssets.AsNoTracking()
            .Where(a => a.InstrumentId == instrumentId)
            .OrderBy(a => a.Month)
            .ToListAsync(cancellationToken);
}
