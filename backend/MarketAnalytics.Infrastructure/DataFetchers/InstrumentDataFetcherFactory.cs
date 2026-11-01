using MarketAnalytics.Domain.Enums;

namespace MarketAnalytics.Infrastructure.DataFetchers;

/// <summary>
/// Factory for creating appropriate data fetcher based on instrument data source type.
/// </summary>
public class InstrumentDataFetcherFactory
{
    private readonly JsonApiDataFetcher _jsonApiFetcher;
    private readonly HtmlScraperDataFetcher _htmlScraperFetcher;
    private readonly CurrencyApiDataFetcher _currencyApiFetcher;

    public InstrumentDataFetcherFactory(
        JsonApiDataFetcher jsonApiFetcher,
        HtmlScraperDataFetcher htmlScraperFetcher,
        CurrencyApiDataFetcher currencyApiFetcher)
    {
        _jsonApiFetcher = jsonApiFetcher;
        _htmlScraperFetcher = htmlScraperFetcher;
        _currencyApiFetcher = currencyApiFetcher;
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
            DataSourceType.CurrencyApi => _currencyApiFetcher,
            _ => throw new ArgumentException($"Unknown data source type: {dataSourceType}", nameof(dataSourceType))
        };
    }
}