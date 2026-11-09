using Microsoft.AspNetCore.Mvc;
using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.Services;

namespace MarketAnalytics.Api.Controllers;

/// <summary>
/// Controller for recent changes of the Buy/Sell signals and long-term trends.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;
    private readonly FeatureSettings _features;

    public AlertsController(IAlertService alertService, Microsoft.Extensions.Options.IOptions<FeatureSettings> features)
    {
        _alertService = alertService;
        _features = features.Value;
    }

    /// <summary>
    /// Signal and trend changes in the last <paramref name="days"/> days (default 30, at most 90), newest first.
    /// technical: steady (default, the 20-day average) or daily (today's 5 votes).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<SignalAlertDto>>> GetRecent(
        [FromQuery] int days = 30,
        [FromQuery] string technical = "steady",
        [FromQuery] bool? news = null,
        CancellationToken cancellationToken = default)
    {
        var steady = !technical.Equals("daily", StringComparison.OrdinalIgnoreCase);
        // News changes only when news is switched on (Features:News), unless the caller says otherwise
        return Ok(await _alertService.GetRecentAsync(days, steady, news ?? _features.News, cancellationToken));
    }
}
