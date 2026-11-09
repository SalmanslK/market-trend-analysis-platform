using Microsoft.AspNetCore.Mvc;
using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Application.Interfaces.Services;

namespace MarketAnalytics.Api.Controllers;

/// <summary>
/// Controller for managing instruments.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class InstrumentsController : ControllerBase
{
    private readonly IInstrumentRepository _instrumentRepository;
    private readonly IPriceDataProcessingService _priceDataProcessingService;

    public InstrumentsController(
        IInstrumentRepository instrumentRepository,
        IPriceDataProcessingService priceDataProcessingService)
    {
        _instrumentRepository = instrumentRepository;
        _priceDataProcessingService = priceDataProcessingService;
    }

    /// <summary>
    /// Gets all active instruments.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InstrumentDto>>> GetInstruments(CancellationToken cancellationToken)
    {
        var instruments = await _instrumentRepository.GetActiveInstrumentsAsync(cancellationToken);
        var lastUpdates = await _instrumentRepository.GetLastSuccessfulUpdateTimesAsync(cancellationToken);
        var latestPrices = await _instrumentRepository.GetLatestPriceDatesAsync(cancellationToken);

        var result = instruments.Select(i => new InstrumentDto
        {
            Id = i.Id,
            Symbol = i.Symbol,
            Name = i.Name,
            Type = i.Type,
            IsActive = i.IsActive,
            LastUpdateDate = lastUpdates.TryGetValue(i.Id, out var lastUpdate) ? lastUpdate : null,
            LatestPriceDate = latestPrices.TryGetValue(i.Id, out var latestPrice) ? latestPrice : null
        });

        return Ok(result);
    }

    /// <summary>
    /// Gets an instrument by symbol.
    /// </summary>
    [HttpGet("{symbol}")]
    public async Task<ActionResult<InstrumentDto>> GetInstrument(string symbol, CancellationToken cancellationToken)
    {
        var instrument = await _instrumentRepository.GetBySymbolAsync(symbol, cancellationToken);
        
        if (instrument == null)
        {
            return NotFound($"Instrument with symbol '{symbol}' not found.");
        }

        var lastUpdates = await _instrumentRepository.GetLastSuccessfulUpdateTimesAsync(cancellationToken);
        var latestPrices = await _instrumentRepository.GetLatestPriceDatesAsync(cancellationToken);

        var result = new InstrumentDto
        {
            Id = instrument.Id,
            Symbol = instrument.Symbol,
            Name = instrument.Name,
            Type = instrument.Type,
            IsActive = instrument.IsActive,
            LastUpdateDate = lastUpdates.TryGetValue(instrument.Id, out var lastUpdate) ? lastUpdate : null,
            LatestPriceDate = latestPrices.TryGetValue(instrument.Id, out var latestPrice) ? latestPrice : null
        };

        return Ok(result);
    }
}