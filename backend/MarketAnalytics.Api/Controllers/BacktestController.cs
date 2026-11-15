using Microsoft.AspNetCore.Mvc;
using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Application.Services;

namespace MarketAnalytics.Api.Controllers;

/// <summary>
/// Controller for testing the technical signal against stored price history.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BacktestController : ControllerBase
{
    private const decimal MaxCostPercent = 5m;

    private readonly IBacktestService _backtestService;

    public BacktestController(IBacktestService backtestService, DataCache cache)
    {
        _backtestService = backtestService;
        _cache = cache;
    }

    private readonly DataCache _cache;

    /// <summary>
    /// Backtests all active instruments. Optional query parameter: costPercent, the cost of each trade.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BacktestResultDto>>> GetAll(
        [FromQuery] decimal costPercent = BacktestService.DefaultCostPercent,
        CancellationToken cancellationToken = default)
    {
        var cost = Math.Clamp(costPercent, 0, MaxCostPercent);
        return Ok(await _cache.GetOrComputeAsync($"backtests:{cost}", async () => (await _backtestService.GetAllAsync(cost, cancellationToken)).ToList(), cancellationToken));
    }

    /// <summary>
    /// Backtests one instrument.
    /// </summary>
    [HttpGet("{symbol}")]
    public async Task<ActionResult<BacktestResultDto>> Get(
        string symbol,
        [FromQuery] decimal costPercent = BacktestService.DefaultCostPercent,
        CancellationToken cancellationToken = default)
    {
        var result = await _backtestService.GetAsync(symbol, Math.Clamp(costPercent, 0, MaxCostPercent), cancellationToken);
        return result == null ? NotFound($"Instrument with symbol '{symbol}' not found.") : Ok(result);
    }
}
