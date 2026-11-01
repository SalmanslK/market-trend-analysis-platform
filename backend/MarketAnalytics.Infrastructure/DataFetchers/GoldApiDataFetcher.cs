using System.Text.Json;
using MarketAnalytics.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MarketAnalytics.Infrastructure.DataFetchers;

/// <summary>
/// Data fetcher for gold-api.com real-time spot prices (e.g., XAU/USD for GOLDINT).
/// Replaces the Investing.com scraper, which blocks automated requests.
/// The real-time endpoint is free and keyless but returns only the current price,
/// so history accumulates one daily point per scheduled run.
/// </summary>
public class GoldApiDataFetcher : IExternalDataFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoldApiDataFetcher> _logger;

    public GoldApiDataFetcher(HttpClient httpClient, ILogger<GoldApiDataFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<PriceDataPoint>> FetchHistoricalDataAsync(
        string url,
        string instrumentSymbol,
        DateTime? sinceDate = null)
    {
        try
        {
            _logger.LogInformation("Fetching spot price from {Url} for instrument {Instrument}", url, instrumentSymbol);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            // Response structure: { "price": 3876.39, "updatedAt": "2026-11-01T17:53:05Z", ... }
            using var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = jsonDoc.RootElement;

            var price = root.GetProperty("price").GetDecimal();
            var updatedAt = root.GetProperty("updatedAt").GetDateTime().ToUniversalTime();

            // Store one point per day (UTC midnight) so repeated runs on the same day refresh that day's price
            var date = DateTime.SpecifyKind(updatedAt.Date, DateTimeKind.Utc);

            // Unlike the history fetchers, keep the latest stored day so its price is refreshed by the upsert
            if (sinceDate.HasValue && date < sinceDate.Value)
                return Enumerable.Empty<PriceDataPoint>();

            _logger.LogInformation("Fetched spot price {Price} for {Instrument} at {UpdatedAt}", price, instrumentSymbol, updatedAt);

            return new[] { new PriceDataPoint(date, price) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching spot price from {Url} for {Instrument}", url, instrumentSymbol);
            throw;
        }
    }
}
