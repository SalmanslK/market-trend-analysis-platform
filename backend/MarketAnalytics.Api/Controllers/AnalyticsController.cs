using Microsoft.AspNetCore.Mvc;
using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.Services;

namespace MarketAnalytics.Api.Controllers;

/// <summary>
/// Controller for per-instrument performance, risk and technical-signal analytics.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IMarketAnalyticsService _analyticsService;

    public AnalyticsController(IMarketAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    /// <summary>
    /// Gets analytics and technical signals for all active instruments.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InstrumentAnalyticsDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _analyticsService.GetAllAnalyticsAsync(cancellationToken));
    }

    /// <summary>
    /// Gets analytics and the technical signal for one instrument.
    /// </summary>
    [HttpGet("{symbol}")]
    public async Task<ActionResult<InstrumentAnalyticsDto>> Get(string symbol, CancellationToken cancellationToken)
    {
        var analytics = await _analyticsService.GetInstrumentAnalyticsAsync(symbol, cancellationToken);
        return analytics == null
            ? NotFound($"No analytics available for instrument '{symbol}'.")
            : Ok(analytics);
    }
}
