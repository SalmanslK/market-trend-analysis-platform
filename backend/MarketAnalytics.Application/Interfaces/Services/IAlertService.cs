using MarketAnalytics.Application.DTOs;

namespace MarketAnalytics.Application.Interfaces.Services;

/// <summary>
/// Service interface for listing recent changes of the signals and trends.
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Changes in the last <paramref name="days"/> days (at most 90), newest first. The technical and combined changes
    /// use the steady technical signal unless <paramref name="steady"/> is false.
    /// </summary>
    Task<List<SignalAlertDto>> GetRecentAsync(int days, bool steady = true, CancellationToken cancellationToken = default);
}
