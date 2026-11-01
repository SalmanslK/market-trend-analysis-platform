using System.Net;
using System.Text.Json;
using MarketAnalytics.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MarketAnalytics.Infrastructure.DataFetchers;

/// <summary>
/// Data fetcher for the open-source fawazahmed0 currency-api (free, keyless, served from the jsDelivr CDN).
/// Used for GOLDINT (XAU/USD) because Investing.com blocks automated requests.
/// The API publishes one snapshot per date, so the URL is a template containing a "{date}" placeholder, e.g.
/// https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@{date}/v1/currencies/xau.json
/// and each snapshot's price is read as the USD rate of the base currency named in the file (xau).
/// </summary>
public class CurrencyApiDataFetcher : IExternalDataFetcher
{
    // Matches the one-year window the other instruments load on first run
    private const int InitialHistoryDays = 365;
    private const int MaxConcurrentRequests = 6;
    private const string QuoteCurrency = "usd";

    private readonly HttpClient _httpClient;
    private readonly ILogger<CurrencyApiDataFetcher> _logger;

    public CurrencyApiDataFetcher(HttpClient httpClient, ILogger<CurrencyApiDataFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<PriceDataPoint>> FetchHistoricalDataAsync(
        string url,
        string instrumentSymbol,
        DateTime? sinceDate = null)
    {
        if (!url.Contains("{date}"))
            throw new ArgumentException("Currency API URL must contain a {date} placeholder.", nameof(url));

        // Base currency comes from the file name, e.g. ".../currencies/xau.json" -> "xau"
        var baseCurrency = Path.GetFileNameWithoutExtension(new Uri(url.Replace("{date}", "latest")).AbsolutePath);

        var today = DateTime.UtcNow.Date;
        var startDate = sinceDate.HasValue ? sinceDate.Value.Date.AddDays(1) : today.AddDays(-InitialHistoryDays);
        var dates = Enumerable.Range(0, Math.Max(0, (today - startDate).Days + 1))
            .Select(offset => startDate.AddDays(offset))
            .ToList();

        _logger.LogInformation("Fetching {Count} daily snapshots for instrument {Instrument} since {StartDate:yyyy-MM-dd}",
            dates.Count, instrumentSymbol, startDate);

        using var throttle = new SemaphoreSlim(MaxConcurrentRequests);
        var results = await Task.WhenAll(dates.Select(async date =>
        {
            await throttle.WaitAsync();
            try
            {
                return await FetchSnapshotAsync(url.Replace("{date}", date.ToString("yyyy-MM-dd")), baseCurrency);
            }
            finally
            {
                throttle.Release();
            }
        }));

        var dataPoints = results
            .Where(dp => dp != null)
            .Select(dp => dp!)
            // Snapshots carry their own publish date; skip anything already stored (incremental update filtering)
            .Where(dp => !sinceDate.HasValue || dp.DateTime > sinceDate.Value)
            .GroupBy(dp => dp.DateTime)
            .Select(g => g.First())
            .OrderBy(dp => dp.DateTime)
            .ToList();

        if (dates.Count > 0 && dataPoints.Count == 0)
        {
            _logger.LogWarning("No snapshots available for {Instrument} since {StartDate:yyyy-MM-dd}", instrumentSymbol, startDate);
        }

        _logger.LogInformation("Fetched {Count} data points for {Instrument}", dataPoints.Count, instrumentSymbol);

        return dataPoints;
    }

    private async Task<PriceDataPoint?> FetchSnapshotAsync(string snapshotUrl, string baseCurrency)
    {
        using var response = await _httpClient.GetAsync(snapshotUrl);

        // Dates without a published snapshot (e.g. today before publishing) return 404
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        // Response structure: { "date": "2026-10-30", "xau": { "usd": 4000.65, ... } }
        using var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = jsonDoc.RootElement;

        if (!root.TryGetProperty("date", out var dateElement) ||
            !DateTime.TryParse(dateElement.GetString(), out var date) ||
            !root.TryGetProperty(baseCurrency, out var rates) ||
            !rates.TryGetProperty(QuoteCurrency, out var rateElement))
        {
            _logger.LogWarning("Unexpected snapshot format from {Url}", snapshotUrl);
            return null;
        }

        return new PriceDataPoint(DateTime.SpecifyKind(date.Date, DateTimeKind.Utc), rateElement.GetDecimal());
    }
}
