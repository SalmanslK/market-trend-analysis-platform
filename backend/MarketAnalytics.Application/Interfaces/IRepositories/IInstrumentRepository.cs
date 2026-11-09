using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Application.Interfaces.IRepositories;

/// <summary>
/// Repository interface for Instrument entity.
/// </summary>
public interface IInstrumentRepository
{
    Task<IEnumerable<Instrument>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Instrument?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default);
    Task<Instrument?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Instrument>> GetActiveInstrumentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the time of the most recent successful data update per instrument id, from the update logs.
    /// </summary>
    Task<Dictionary<int, DateTime>> GetLastSuccessfulUpdateTimesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the date of the newest stored price per instrument id. A check can succeed without finding new prices,
    /// so this, not the last update time, says whether the data is current.
    /// </summary>
    Task<Dictionary<int, DateTime>> GetLatestPriceDatesAsync(CancellationToken cancellationToken = default);
}