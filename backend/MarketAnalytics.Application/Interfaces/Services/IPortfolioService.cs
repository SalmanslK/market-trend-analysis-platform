using MarketAnalytics.Application.DTOs;

namespace MarketAnalytics.Application.Interfaces.Services;

/// <summary>
/// Service interface for testing mixes of instruments against stored price history.
/// </summary>
public interface IPortfolioService
{
    /// <summary>
    /// Tests the mixes over the same dates. Mixes with unknown instruments or no weights are left out.
    /// </summary>
    Task<PortfolioComparisonDto> CompareAsync(IReadOnlyList<PortfolioRequestDto> mixes, decimal costPercent, CancellationToken cancellationToken = default);
}
