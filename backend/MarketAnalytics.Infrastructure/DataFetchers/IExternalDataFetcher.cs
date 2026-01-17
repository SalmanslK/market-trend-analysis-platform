using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Infrastructure.DataFetchers;

/// <summary>
/// Interface for fetching external market data.
/// </summary>
public interface IExternalDataFetcher
{
    /// <summary>
    /// Fetches historical data from the external source.
    /// </summary>
    /// <param name="url">Data source URL</param>
    /// <param name="instrumentSymbol">Instrument symbol for identification</param>
    /// <param name="sinceDate">Optional date to fetch data since (for incremental updates)</param>
    /// <returns>Collection of price data points</returns>
    Task<IEnumerable<PriceDataPoint>> FetchHistoricalDataAsync(
        string url, 
        string instrumentSymbol, 
        DateTime? sinceDate = null);
}