using Microsoft.AspNetCore.Mvc;
using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.Enums;

namespace MarketAnalytics.Api.Controllers;

/// <summary>
/// Controller for cross-instrument summary data.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SummaryController : ControllerBase
{
    private readonly IPriceDataProcessingService _priceDataProcessingService;
    private readonly ILogger<SummaryController> _logger;

    public SummaryController(
        IPriceDataProcessingService priceDataProcessingService,
        ILogger<SummaryController> logger)
    {
        _priceDataProcessingService = priceDataProcessingService;
        _logger = logger;
    }

    /// <summary>
    /// Gets summary data for all active instruments.
    /// Query parameters: interval (Daily, Weekly, Monthly, Quarterly, Yearly), timeframe (Day, Week, Month, Quarter, HalfYear, Year, Ytd, All)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SummaryDto>>> GetSummary(
        [FromQuery] Interval interval = Interval.Daily,
        [FromQuery] Timeframe timeframe = Timeframe.Quarter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _priceDataProcessingService.GetSummaryAsync(
                interval,
                timeframe,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting summary data");
            return StatusCode(500, "An error occurred while processing the request.");
        }
    }
}