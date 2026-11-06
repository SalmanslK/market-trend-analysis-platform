using Microsoft.AspNetCore.Mvc;
using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.Services;

namespace MarketAnalytics.Api.Controllers;

/// <summary>
/// Controller for market news, its sentiment, and its link to instruments' price moves.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class NewsController : ControllerBase
{
    private readonly INewsService _newsService;

    public NewsController(INewsService newsService)
    {
        _newsService = newsService;
    }

    /// <summary>
    /// Gets recent news, newest first. Optional filters: symbol (linked instrument), topic (topic key).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NewsArticleDto>>> GetNews(
        [FromQuery] string? symbol,
        [FromQuery] string? topic,
        [FromQuery] int days = 30,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _newsService.GetNewsAsync(symbol, topic, Math.Clamp(days, 1, 365), Math.Clamp(limit, 1, 500), cancellationToken));
    }

    /// <summary>
    /// Gets the news topics and the instruments each affects.
    /// </summary>
    [HttpGet("topics")]
    public ActionResult<IEnumerable<NewsTopicDto>> GetTopics() => Ok(_newsService.GetTopics());

    /// <summary>
    /// Gets combined news sentiment per instrument over the last N days.
    /// </summary>
    [HttpGet("sentiment")]
    public async Task<ActionResult<IEnumerable<NewsSentimentSummaryDto>>> GetSentiment(
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _newsService.GetSentimentSummaryAsync(Math.Clamp(days, 1, 365), cancellationToken));
    }

    /// <summary>
    /// Gets an instrument's biggest daily moves over the last N days with the news around each.
    /// </summary>
    [HttpGet("moves/{symbol}")]
    public async Task<ActionResult<InstrumentMovesDto>> GetMoves(
        string symbol,
        [FromQuery] int days = 90,
        [FromQuery] int count = 6,
        CancellationToken cancellationToken = default)
    {
        var moves = await _newsService.GetBiggestMovesAsync(symbol, Math.Clamp(days, 1, 365), Math.Clamp(count, 1, 20), cancellationToken);
        return moves == null ? NotFound($"Instrument with symbol '{symbol}' not found.") : Ok(moves);
    }
}
