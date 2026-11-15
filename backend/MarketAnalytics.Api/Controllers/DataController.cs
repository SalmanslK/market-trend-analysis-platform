using Microsoft.AspNetCore.Mvc;
using MarketAnalytics.Infrastructure.News;
using MarketAnalytics.Infrastructure.Updates;

namespace MarketAnalytics.Api.Controllers;

/// <summary>
/// Lets the website ask for fresh data now instead of waiting for the Worker's next scheduled check.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DataController : ControllerBase
{
    // One refresh at a time: a second click while one is running would only fetch the same data twice
    private static readonly SemaphoreSlim Running = new(1, 1);

    private readonly MarketDataUpdater _marketDataUpdater;
    private readonly NewsUpdater _newsUpdater;
    private readonly SignalRecorder _signalRecorder;
    private readonly ILogger<DataController> _logger;

    public DataController(MarketDataUpdater marketDataUpdater, NewsUpdater newsUpdater, SignalRecorder signalRecorder, ILogger<DataController> logger)
    {
        _marketDataUpdater = marketDataUpdater;
        _newsUpdater = newsUpdater;
        _signalRecorder = signalRecorder;
        _logger = logger;
    }

    /// <summary>
    /// Fetches new prices, fund payouts and news for every instrument, the same way the Worker does, and says what
    /// was found. Takes from a few seconds to about a minute. Returns 409 if a refresh is already running.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshResultDto>> Refresh(CancellationToken cancellationToken)
    {
        if (!await Running.WaitAsync(0, cancellationToken))
            return Conflict("A refresh is already running. Wait for it to finish.");

        try
        {
            var started = DateTime.UtcNow;
            var instruments = await _marketDataUpdater.UpdateAllAsync(cancellationToken);

            NewsUpdateResult? news = null;
            string? newsError = null;
            try
            {
                news = await _newsUpdater.UpdateAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Prices are the important part; a news failure is reported, not fatal
                _logger.LogError(ex, "News refresh failed");
                newsError = ex.Message;
            }

            string? signalRecordError = null;
            try
            {
                await _signalRecorder.RecordAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Recording signals failed");
                signalRecordError = ex.Message;
            }

            return Ok(new RefreshResultDto
            {
                StartedAt = started,
                FinishedAt = DateTime.UtcNow,
                Instruments = instruments.Select(i => new InstrumentRefreshDto
                {
                    Symbol = i.Symbol,
                    NewPrices = i.NewPrices,
                    LatestDate = i.LatestDate,
                    NewPayouts = i.NewPayouts,
                    Error = i.Error,
                }).ToList(),
                NewStories = news?.Added ?? 0,
                NewsError = newsError,
                SignalRecordError = signalRecordError,
            });
        }
        finally
        {
            Running.Release();
        }
    }
}

public class RefreshResultDto
{
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public List<InstrumentRefreshDto> Instruments { get; set; } = new();
    public int NewStories { get; set; }
    /// <summary>Set when the news sources could not be read; prices may still have been updated.</summary>
    public string? NewsError { get; set; }
    /// <summary>Set when today's signals could not be saved to the live record; prices are still updated.</summary>
    public string? SignalRecordError { get; set; }
}

public class InstrumentRefreshDto
{
    public string Symbol { get; set; } = string.Empty;
    public int NewPrices { get; set; }
    public DateTime? LatestDate { get; set; }
    public int NewPayouts { get; set; }
    /// <summary>Why this instrument could not be updated, if it couldn't.</summary>
    public string? Error { get; set; }
}
