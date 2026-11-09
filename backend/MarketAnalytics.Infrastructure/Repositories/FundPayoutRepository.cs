using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Infrastructure.Data;

namespace MarketAnalytics.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for FundPayout entity.
/// </summary>
public class FundPayoutRepository : IFundPayoutRepository
{
    private readonly ApplicationDbContext _context;

    public FundPayoutRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<FundPayout>> GetByInstrumentIdAsync(int instrumentId, CancellationToken cancellationToken = default)
    {
        return await _context.FundPayouts
            .AsNoTracking()
            .Where(p => p.InstrumentId == instrumentId)
            .OrderBy(p => p.PayoutDate)
            .ToListAsync(cancellationToken);
    }
}
