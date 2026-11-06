using MarketAnalytics.Application.DTOs;

namespace MarketAnalytics.Application.Interfaces.Services;

/// <summary>
/// Service interface for testing the technical signal against stored price history.
/// </summary>
public interface IBacktestService
{
    /// <summary>
    /// Backtests all active instruments.
    /// </summary>
    Task<IEnumerable<BacktestResultDto>> GetAllAsync(decimal costPercent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Backtests one instrument, or returns null if it doesn't exist.
    /// </summary>
    Task<BacktestResultDto?> GetAsync(string symbol, decimal costPercent, CancellationToken cancellationToken = default);
}
