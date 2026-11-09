using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Application.Interfaces.IRepositories;

/// <summary>
/// Repository interface for FundPayout entity.
/// </summary>
public interface IFundPayoutRepository
{
    /// <summary>
    /// The instrument's payouts, oldest first. Empty for instruments that are not funds.
    /// </summary>
    Task<List<FundPayout>> GetByInstrumentIdAsync(int instrumentId, CancellationToken cancellationToken = default);
}
