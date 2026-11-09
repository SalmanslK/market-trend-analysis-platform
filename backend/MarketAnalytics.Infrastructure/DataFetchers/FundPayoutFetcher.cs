using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace MarketAnalytics.Infrastructure.DataFetchers;

/// <summary>
/// Fetches a mutual fund's cash payouts (dividends) from the same source as its NAVs.
/// Response: { "response": [{ "fundId": "...", "name": "MIF", "payoutPerUnit": 1, "payoutDate": "2026-06-24" }] }.
/// </summary>
public partial class FundPayoutFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FundPayoutFetcher> _logger;

    public FundPayoutFetcher(HttpClient httpClient, ILogger<FundPayoutFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [GeneratedRegex(@"^(?<base>https?://[^?]+/mutual-funds/)nav-history/(?<id>[0-9a-fA-F-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex NavHistoryUrl();

    /// <summary>
    /// The payouts URL for a fund whose prices come from a nav-history URL, or null for other instruments.
    /// </summary>
    public static string? PayoutUrlFor(string dataSourceUrl)
    {
        var match = NavHistoryUrl().Match(dataSourceUrl);
        return match.Success ? $"{match.Groups["base"].Value}payouts/{match.Groups["id"].Value}" : null;
    }

    public async Task<List<(DateTime Date, decimal AmountPerUnit)>> FetchAsync(string payoutUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching fund payouts from {Url}", payoutUrl);
        var response = await _httpClient.GetAsync(payoutUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        return Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    public static List<(DateTime Date, decimal AmountPerUnit)> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new List<(DateTime, decimal)>();
        if (!doc.RootElement.TryGetProperty("response", out var items) || items.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("payoutDate", out var dateElement)
                || !item.TryGetProperty("payoutPerUnit", out var amountElement)
                || amountElement.ValueKind != JsonValueKind.Number
                || !DateTime.TryParse(dateElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;

            var amount = amountElement.GetDecimal();
            if (amount > 0)
                result.Add((DateTime.SpecifyKind(date.Date, DateTimeKind.Utc), amount));
        }
        return result;
    }
}
