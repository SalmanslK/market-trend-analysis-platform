using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Infrastructure.Data;

namespace MarketAnalytics.Infrastructure.Repositories;

public class BriefRepository : IBriefRepository
{
    private readonly ApplicationDbContext _context;

    public BriefRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DailyBrief brief, CancellationToken cancellationToken = default)
    {
        _context.DailyBriefs.Add(brief);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<DailyBrief?> GetLatestAsync(CancellationToken cancellationToken = default) =>
        _context.DailyBriefs.AsNoTracking().OrderByDescending(b => b.CreatedAt).FirstOrDefaultAsync(cancellationToken);
}
