using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MarketAnalytics.Infrastructure.DataFetchers;

/// <summary>
/// The current members of a PSX index (Sarmaaya indices/sunbrust/{index}: sectors, each with its companies).
/// Membership changes when the index is rebalanced, so it is read rather than kept in code.
/// </summary>
public class IndexMembersFetcher
{
    private const string Url = "https://beta-restapi.sarmaaya.pk/api/indices/sunbrust/{0}";

    private readonly HttpClient _httpClient;
    private readonly ILogger<IndexMembersFetcher> _logger;

    public IndexMembersFetcher(HttpClient httpClient, ILogger<IndexMembersFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<string>> GetMembersAsync(string index, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching {Index} members", index);
        var json = await _httpClient.GetStringAsync(string.Format(Url, Uri.EscapeDataString(index)), cancellationToken);
        return Parse(json);
    }

    public static List<string> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Object
            || !response.TryGetProperty("sectors", out var sectors) || sectors.ValueKind != JsonValueKind.Array)
            return new();

        return sectors.EnumerateArray()
            .Where(s => s.TryGetProperty("companies", out var c) && c.ValueKind == JsonValueKind.Array)
            .SelectMany(s => s.GetProperty("companies").EnumerateArray())
            .Select(c => c.TryGetProperty("symbol", out var symbol) ? symbol.GetString() : null)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
    }
}
