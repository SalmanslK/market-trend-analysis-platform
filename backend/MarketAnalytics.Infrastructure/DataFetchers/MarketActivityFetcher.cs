using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace MarketAnalytics.Infrastructure.DataFetchers;

/// <summary>
/// Fetches how much activity is behind prices, from the same source as the prices: daily shares traded for an index
/// (indices/volume-history) and monthly fund size for a fund (mutual-funds/aum-history).
/// </summary>
public partial class MarketActivityFetcher
{
    /// <summary>Until this month the source gave fund sizes in thousands of rupees; from it, in rupees.</summary>
    public static readonly DateTime FundSizeInRupeesFrom = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly HttpClient _httpClient;
    private readonly ILogger<MarketActivityFetcher> _logger;

    public MarketActivityFetcher(HttpClient httpClient, ILogger<MarketActivityFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [GeneratedRegex(@"^(?<base>https?://[^?]+/indices/)price-history/(?<symbol>[^/?]+)", RegexOptions.IgnoreCase)]
    private static partial Regex IndexPriceUrl();

    [GeneratedRegex(@"^(?<base>https?://[^?]+/mutual-funds/)nav-history/(?<id>[0-9a-fA-F-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex NavHistoryUrl();

    /// <summary>The volume URL for an index priced from an indices/price-history URL, or null for other instruments.</summary>
    public static string? VolumeUrlFor(string dataSourceUrl, int days)
    {
        var match = IndexPriceUrl().Match(dataSourceUrl);
        return match.Success ? $"{match.Groups["base"].Value}volume-history/{match.Groups["symbol"].Value}?days={days}" : null;
    }

    /// <summary>The fund-size URL for a fund priced from a nav-history URL, or null for other instruments.</summary>
    public static string? FundSizeUrlFor(string dataSourceUrl)
    {
        var match = NavHistoryUrl().Match(dataSourceUrl);
        return match.Success ? $"{match.Groups["base"].Value}aum-history/{match.Groups["id"].Value}?days=10000" : null;
    }

    public async Task<List<(DateTime Date, long Shares)>> FetchVolumesAsync(string url, CancellationToken cancellationToken = default) =>
        ParseVolumes(await GetAsync(url, cancellationToken));

    public async Task<List<(DateTime Month, decimal Rupees)>> FetchFundSizesAsync(string url, CancellationToken cancellationToken = default) =>
        ParseFundSizes(await GetAsync(url, cancellationToken));

    private async Task<string> GetAsync(string url, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching market activity from {Url}", url);
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Response: { "response": [{ "symbol": "KSE100", "volume": 195449769, "date": "2026-11-13T00:00:00.000Z" }] }.
    /// A zero means the source has no volume for that day (it has none before November 2013), so it is skipped.
    /// </summary>
    public static List<(DateTime Date, long Shares)> ParseVolumes(string json)
    {
        var result = new List<(DateTime, long)>();
        foreach (var item in Items(json))
        {
            if (TryDate(item, "date", out var date)
                && item.TryGetProperty("volume", out var volume)
                && volume.ValueKind == JsonValueKind.Number
                && volume.TryGetInt64(out var shares)
                && shares > 0)
                result.Add((date, shares));
        }
        return result.OrderBy(v => v.Item1).ToList();
    }

    /// <summary>
    /// Response: { "response": [{ "fundId": "...", "aum": 59118390000, "date": "2026-10-01" }] }. Sizes before
    /// <see cref="FundSizeInRupeesFrom"/> are in thousands of rupees and are converted to rupees.
    /// </summary>
    public static List<(DateTime Month, decimal Rupees)> ParseFundSizes(string json)
    {
        var result = new List<(DateTime, decimal)>();
        foreach (var item in Items(json))
        {
            if (!TryDate(item, "date", out var date)
                || !item.TryGetProperty("aum", out var aum)
                || aum.ValueKind != JsonValueKind.Number)
                continue;

            var month = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var rupees = aum.GetDecimal() * (month < FundSizeInRupeesFrom ? 1000 : 1);
            if (rupees > 0)
                result.Add((month, rupees));
        }
        return result.OrderBy(a => a.Item1).ToList();
    }

    private static IEnumerable<JsonElement> Items(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("response", out var items) || items.ValueKind != JsonValueKind.Array)
            return [];
        return items.EnumerateArray().Select(i => i.Clone()).ToList();
    }

    private static bool TryDate(JsonElement item, string name, out DateTime date)
    {
        date = default;
        if (!item.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String
            || !DateTime.TryParse(element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
            return false;
        date = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        return true;
    }
}
