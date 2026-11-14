using Microsoft.AspNetCore.Mvc;
using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Application.Services;

namespace MarketAnalytics.Api.Controllers;

/// <summary>
/// The market brief: the latest one written, and writing a new one on request (a paid Claude call).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BriefController : ControllerBase
{
    private readonly DailyBriefService _briefs;

    public BriefController(DailyBriefService briefs)
    {
        _briefs = briefs;
    }

    /// <summary>The latest brief (204 if none yet) and whether a new one can be written.</summary>
    [HttpGet("latest")]
    public async Task<ActionResult<BriefStatusDto>> GetLatest(CancellationToken cancellationToken) =>
        Ok(new BriefStatusDto { CanWrite = _briefs.CanWrite, Latest = await _briefs.GetLatestAsync(cancellationToken) });

    /// <summary>Writes a new brief from today's figures. Takes a minute or two.</summary>
    [HttpPost]
    public async Task<ActionResult<DailyBriefDto>> Write(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _briefs.WriteAsync(cancellationToken));
        }
        catch (BriefUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }
}

public class BriefStatusDto
{
    /// <summary>False when Claude Code is not available on this computer.</summary>
    public bool CanWrite { get; set; }
    public DailyBriefDto? Latest { get; set; }
}
