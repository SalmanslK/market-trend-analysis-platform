using Microsoft.AspNetCore.Mvc;
using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Application.Services;

namespace MarketAnalytics.Api.Controllers;

/// <summary>
/// Controller for testing mixes of instruments (e.g. stocks, gold and cash) against stored price history.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    private const decimal MaxCostPercent = 5m;

    private readonly IPortfolioService _portfolioService;
    private readonly DataCache _cache;

    public PortfolioController(IPortfolioService portfolioService, DataCache cache)
    {
        _portfolioService = portfolioService;
        _cache = cache;
    }

    /// <summary>
    /// The preset mixes, fixed before seeing their results.
    /// </summary>
    [HttpGet("presets")]
    public ActionResult<IEnumerable<PortfolioRequestDto>> GetPresets() => Ok(PortfolioService.Presets);

    /// <summary>
    /// Tests the preset mixes. Optional query parameter: costPercent, the cost of each buy or sell.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PortfolioComparisonDto>> ComparePresets(
        [FromQuery] decimal costPercent = BacktestService.DefaultCostPercent,
        CancellationToken cancellationToken = default)
    {
        var cost = Math.Clamp(costPercent, 0, MaxCostPercent);
        return Ok(await _cache.GetOrComputeAsync($"portfolio:presets:{cost}",
            () => _portfolioService.CompareAsync(PortfolioService.Presets, cost, cancellationToken), cancellationToken));
    }

    /// <summary>
    /// Tests the given mixes over the same dates, at most 12.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PortfolioComparisonDto>> Compare(
        [FromBody] List<PortfolioRequestDto> mixes,
        [FromQuery] decimal costPercent = BacktestService.DefaultCostPercent,
        CancellationToken cancellationToken = default)
    {
        if (mixes.Count == 0)
            return BadRequest("Send at least one mix.");
        if (mixes.Count > PortfolioService.MaxMixes)
            return BadRequest($"Send at most {PortfolioService.MaxMixes} mixes.");

        var cost = Math.Clamp(costPercent, 0, MaxCostPercent);
        // The same mixes asked again (changing the cost back, reopening the page) are answered from memory
        var key = $"portfolio:{cost}:{System.Text.Json.JsonSerializer.Serialize(mixes)}";
        return Ok(await _cache.GetOrComputeAsync(key, () => _portfolioService.CompareAsync(mixes, cost, cancellationToken), cancellationToken));
    }
}
