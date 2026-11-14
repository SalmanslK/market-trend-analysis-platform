using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Application.Interfaces.IRepositories;

/// <summary>Trading volumes (indices) and fund sizes (funds), oldest first; empty when an instrument has none.</summary>
public interface IMarketActivityRepository
{
    Task<List<TradingVolume>> GetVolumesAsync(int instrumentId, DateTime since, CancellationToken cancellationToken = default);
    Task<List<FundAssets>> GetFundSizesAsync(int instrumentId, CancellationToken cancellationToken = default);
}
