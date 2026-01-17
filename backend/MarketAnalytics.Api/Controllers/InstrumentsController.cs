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
        
        var result = instruments.Select(i => new InstrumentDto
        {
            Id = i.Id,
            Symbol = i.Symbol,
            Name = i.Name,
            Type = i.Type,
            IsActive = i.IsActive,
            LastUpdateDate = null // TODO: Get from latest price point or update log
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

        var result = new InstrumentDto
        {
            Id = instrument.Id,
            Symbol = instrument.Symbol,
            Name = instrument.Name,
            Type = instrument.Type,
            IsActive = instrument.IsActive,
            LastUpdateDate = null // TODO: Get from latest price point or update log
        };

        return Ok(result);
    }
}