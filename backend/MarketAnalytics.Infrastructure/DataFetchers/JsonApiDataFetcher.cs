using System.Text.Json;
using MarketAnalytics.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MarketAnalytics.Infrastructure.DataFetchers;

/// <summary>
/// Data fetcher for JSON API endpoints (indices, mutual funds, forex, commodities).
/// Matches Python logic: fetch_new_data() for JSON APIs.
/// </summary>
public class JsonApiDataFetcher : IExternalDataFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JsonApiDataFetcher> _logger;

    public JsonApiDataFetcher(HttpClient httpClient, ILogger<JsonApiDataFetcher> logger)
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
            _logger.LogInformation("Fetching data from {Url} for instrument {Instrument}", url, instrumentSymbol);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(jsonContent);

            // Parse response structure: { "response": [...] }
            var responseArray = jsonDoc.RootElement.GetProperty("response").EnumerateArray();

            var dataPoints = new List<PriceDataPoint>();

            foreach (var item in responseArray)
            {
                // JSON structure: { "date": "...", "nav": ... } or { "date": "...", "price": ... }
                if (!item.TryGetProperty("date", out var dateElement))
                    continue;

                var price = 0m;
                if (item.TryGetProperty("nav", out var navElement))
                {
                    price = navElement.GetDecimal();
                }
                else if (item.TryGetProperty("price", out var priceElement))
                {
                    price = priceElement.GetDecimal();
                }
                else
                {
                    continue;
                }

                // Parse date and convert to UTC (matches Python: utc=True, then tz_localize(None))
                var dateString = dateElement.GetString();
                if (string.IsNullOrEmpty(dateString))
                    continue;

                if (DateTime.TryParse(dateString, out var dateTime))
                {
                    // Normalize to UTC and remove timezone info (matches Python logic)
                    if (dateTime.Kind == DateTimeKind.Unspecified)
                    {
                        dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                    }
                    else if (dateTime.Kind == DateTimeKind.Local)
                    {
                        dateTime = dateTime.ToUniversalTime();
                    }

                    // Skip if before sinceDate (incremental update filtering)
                    if (sinceDate.HasValue && dateTime <= sinceDate.Value)
                        continue;

                    dataPoints.Add(new PriceDataPoint(dateTime, price));
                }
            }

            _logger.LogInformation("Fetched {Count} data points for {Instrument}", dataPoints.Count, instrumentSymbol);

            return dataPoints.OrderBy(dp => dp.DateTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching data from {Url} for {Instrument}", url, instrumentSymbol);
            throw;
        }
    }
}