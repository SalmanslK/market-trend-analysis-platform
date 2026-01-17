using HtmlAgilityPack;
using MarketAnalytics.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MarketAnalytics.Infrastructure.DataFetchers;

/// <summary>
/// Data fetcher for HTML scraping (e.g., Investing.com for GOLDINT).
/// Matches Python logic: fetch_goldint_data() using BeautifulSoup.
/// </summary>
public class HtmlScraperDataFetcher : IExternalDataFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HtmlScraperDataFetcher> _logger;

    public HtmlScraperDataFetcher(HttpClient httpClient, ILogger<HtmlScraperDataFetcher> logger)
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
            _logger.LogInformation("Scraping data from {Url} for instrument {Instrument}", url, instrumentSymbol);

            // Set user agent to avoid blocking
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var htmlContent = await _httpClient.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var dataPoints = new List<PriceDataPoint>();

            // Find table rows: table tbody tr (matches Python: soup.select("table tbody tr"))
            var rows = htmlDoc.DocumentNode.SelectNodes("//table//tbody//tr");
            if (rows == null)
            {
                _logger.LogWarning("No table rows found in HTML for {Instrument}", instrumentSymbol);
                return Enumerable.Empty<PriceDataPoint>();
            }

            foreach (var row in rows)
            {
                var cols = row.SelectNodes("td");
                if (cols == null || cols.Count < 2)
                    continue;

                var dateText = cols[0].InnerText.Trim();
                var priceText = cols[1].InnerText.Trim().Replace(",", "");

                // Skip rows where date_text is not a proper date (matches Python logic)
                if (!DateTime.TryParse(dateText, out var dateTime))
                {
                    try
                    {
                        // Try format: "Jan 01, 2024"
                        dateTime = DateTime.ParseExact(dateText, "MMM dd, yyyy", null);
                    }
                    catch
                    {
                        continue; // Skip invalid dates
                    }
                }

                if (!decimal.TryParse(priceText, out var price))
                    continue;

                // Normalize to UTC
                if (dateTime.Kind == DateTimeKind.Unspecified)
                {
                    dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                }

                // Skip if before sinceDate (incremental update filtering)
                if (sinceDate.HasValue && dateTime <= sinceDate.Value)
                    continue;

                dataPoints.Add(new PriceDataPoint(dateTime, price));
            }

            _logger.LogInformation("Scraped {Count} data points for {Instrument}", dataPoints.Count, instrumentSymbol);

            return dataPoints.OrderBy(dp => dp.DateTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping data from {Url} for {Instrument}", url, instrumentSymbol);
            throw;
        }
    }
}