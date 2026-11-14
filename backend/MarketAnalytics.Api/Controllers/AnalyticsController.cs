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
    private readonly DataCache _cache;

    public AnalyticsController(IMarketAnalyticsService analyticsService, DataCache cache)
    {
        _analyticsService = analyticsService;
        _cache = cache;
    }

    /// <summary>
    /// Gets analytics and technical signals for all active instruments.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InstrumentAnalyticsDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _cache.GetOrComputeAsync("analytics", async () => (await _analyticsService.GetAllAnalyticsAsync(cancellationToken)).ToList(), cancellationToken));
    }

    /// <summary>
    /// Gets analytics and the technical signal for one instrument.
    /// </summary>
    [HttpGet("{symbol}")]
    public async Task<ActionResult<InstrumentAnalyticsDto>> Get(string symbol, CancellationToken cancellationToken)
    {
        var analytics = await _cache.GetOrComputeAsync($"analytics:{symbol.ToUpperInvariant()}", () => _analyticsService.GetInstrumentAnalyticsAsync(symbol, cancellationToken), cancellationToken);
        return analytics == null
            ? NotFound($"No analytics available for instrument '{symbol}'.")
            : Ok(analytics);
    }

    /// <summary>
    /// Gets the trading volume (indices) or fund size (funds) history for a chart.
    /// </summary>
    [HttpGet("{symbol}/activity")]
    public async Task<ActionResult<ActivitySeriesDto>> GetActivity(string symbol, CancellationToken cancellationToken)
    {
        var activity = await _analyticsService.GetActivityAsync(symbol, cancellationToken);
        return activity == null ? NotFound($"Unknown instrument '{symbol}'.") : Ok(activity);
    }
}
