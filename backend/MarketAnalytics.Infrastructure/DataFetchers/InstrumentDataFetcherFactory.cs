using MarketAnalytics.Domain.Enums;

namespace MarketAnalytics.Infrastructure.DataFetchers;

/// <summary>
/// Factory for creating appropriate data fetcher based on instrument data source type.
/// </summary>
public class InstrumentDataFetcherFactory
{
    private readonly JsonApiDataFetcher _jsonApiFetcher;
    private readonly HtmlScraperDataFetcher _htmlScraperFetcher;

    public InstrumentDataFetcherFactory(
        JsonApiDataFetcher jsonApiFetcher,
        HtmlScraperDataFetcher htmlScraperFetcher)
    {
        _jsonApiFetcher = jsonApiFetcher;
        _htmlScraperFetcher = htmlScraperFetcher;
    }

    /// <summary>
    /// Gets the appropriate data fetcher for the given data source type.
    /// </summary>
    public IExternalDataFetcher GetFetcher(DataSourceType dataSourceType)
    {
        return dataSourceType switch
        {
            DataSourceType.JsonApi => _jsonApiFetcher,
            DataSourceType.HtmlScraper => _htmlScraperFetcher,
            _ => throw new ArgumentException($"Unknown data source type: {dataSourceType}", nameof(dataSourceType))
        };
    }
}