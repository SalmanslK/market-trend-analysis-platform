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

    public AlertsController(IAlertService alertService)
    {
        _alertService = alertService;
    }

    /// <summary>
    /// Signal and trend changes in the last <paramref name="days"/> days (default 30, at most 90), newest first.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<SignalAlertDto>>> GetRecent([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        return Ok(await _alertService.GetRecentAsync(days, cancellationToken));
    }
}
