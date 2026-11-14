using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Application.Interfaces.IRepositories;

public interface IBriefRepository
{
    Task AddAsync(DailyBrief brief, CancellationToken cancellationToken = default);

    /// <summary>The most recently written brief, or null if none has been written.</summary>
    Task<DailyBrief?> GetLatestAsync(CancellationToken cancellationToken = default);
}
