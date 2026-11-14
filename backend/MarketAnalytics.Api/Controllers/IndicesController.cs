using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MarketAnalytics.Infrastructure.DataFetchers;

namespace MarketAnalytics.Api.Controllers;

/// <summary>
/// Index membership, for marking companies that are in the KMI-30. Read from the source and kept for a day:
/// indices are rebalanced only twice a year.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class IndicesController : ControllerBase
{
    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase) { "KMI30", "KSE100" };

    private readonly IndexMembersFetcher _fetcher;
    private readonly IMemoryCache _cache;
    private readonly ILogger<IndicesController> _logger;

    public IndicesController(IndexMembersFetcher fetcher, IMemoryCache cache, ILogger<IndicesController> logger)
    {
        _fetcher = fetcher;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>The symbols of the companies in an index (KMI30 or KSE100).</summary>
    [HttpGet("{index}/members")]
    public async Task<ActionResult<List<string>>> GetMembers(string index, CancellationToken cancellationToken)
    {
        if (!Known.Contains(index))
            return NotFound($"Unknown index '{index}'.");

        var key = $"members:{index.ToUpperInvariant()}";
        if (_cache.TryGetValue(key, out List<string>? members) && members != null)
            return Ok(members);
        try
        {
            members = await _fetcher.GetMembersAsync(index.ToUpperInvariant(), cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not read the {Index} members", index);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "The index members could not be read from the source.");
        }
        if (members.Count > 0)
            _cache.Set(key, members, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1), Size = 1 });
        return Ok(members);
    }
}
