using Microsoft.AspNetCore.Mvc;
using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Infrastructure.Updates;

namespace MarketAnalytics.Api.Controllers;

/// <summary>
/// The forward record: signals saved as they were shown each day, and what prices did afterwards.
/// </summary>
[ApiController]
[Route("api/signal-record")]
public class SignalRecordController : ControllerBase
{
    private readonly SignalRecorder _recorder;

    public SignalRecordController(SignalRecorder recorder)
    {
        _recorder = recorder;
    }

    [HttpGet]
    public async Task<ActionResult<SignalRecordDto>> Get(CancellationToken cancellationToken) =>
        Ok(await _recorder.SummarizeAsync(cancellationToken));
}
