using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Application.Interfaces.IRepositories;

/// <summary>
/// Repository interface for PricePoint entity.
/// </summary>
public interface IPricePointRepository
{
    Task<IEnumerable<PricePoint>> GetByInstrumentIdAsync(
        int instrumentId, 
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        CancellationToken cancellationToken = default);
    
    Task<DateTime?> GetLatestDateAsync(int instrumentId, CancellationToken cancellationToken = default);
    
    Task AddRangeAsync(IEnumerable<PricePoint> pricePoints, CancellationToken cancellationToken = default);
    
    Task BulkUpsertAsync(IEnumerable<PricePoint> pricePoints, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<PricePoint>> GetLatestForAllInstrumentsAsync(CancellationToken cancellationToken = default);
}