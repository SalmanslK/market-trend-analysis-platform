using Microsoft.AspNetCore.Mvc;
using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.Enums;

namespace MarketAnalytics.Api.Controllers;

/// <summary>
/// Controller for retrieving price data with indicators.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PricesController : ControllerBase
{
    private readonly IPriceDataProcessingService _priceDataProcessingService;
    private readonly ILogger<PricesController> _logger;

    public PricesController(
        IPriceDataProcessingService priceDataProcessingService,
        ILogger<PricesController> logger)
    {
        _priceDataProcessingService = priceDataProcessingService;
        _logger = logger;
    }

    /// <summary>
    /// Gets processed price data for a specific instrument with indicators.
    /// Query parameters: interval (Daily, Weekly, Monthly, Quarterly, Yearly), timeframe (Day, Week, Month, Quarter, HalfYear, Year, Ytd, All)
    /// </summary>
    [HttpGet("{symbol}")]
    public async Task<ActionResult<IEnumerable<PriceDataDto>>> GetPriceData(
        string symbol,
        [FromQuery] Interval interval = Interval.Daily,
        [FromQuery] Timeframe timeframe = Timeframe.Quarter,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _priceDataProcessingService.ProcessInstrumentDataAsync(
                symbol,
                interval,
                timeframe,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for instrument {Symbol}", symbol);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing price data for instrument {Symbol}", symbol);
            return StatusCode(500, "An error occurred while processing the request.");
        }
    }
}