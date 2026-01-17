using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Infrastructure.Data;

namespace MarketAnalytics.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Instrument entity.
/// </summary>
public class InstrumentRepository : IInstrumentRepository
{
    private readonly ApplicationDbContext _context;

    public InstrumentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Instrument>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Instruments
            .OrderBy(i => i.Symbol)
            .ToListAsync(cancellationToken);
    }

    public async Task<Instrument?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        return await _context.Instruments
            .FirstOrDefaultAsync(i => i.Symbol == symbol, cancellationToken);
    }

    public async Task<Instrument?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Instruments
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Instrument>> GetActiveInstrumentsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Instruments
            .Where(i => i.IsActive)
            .OrderBy(i => i.Symbol)
            .ToListAsync(cancellationToken);
    }
}