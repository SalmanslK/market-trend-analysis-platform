using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.Enums;

namespace MarketAnalytics.Infrastructure.Data.SeedData;

/// <summary>
/// Seed data for initial instruments based on Python INSTRUMENT_CONFIG.
/// </summary>
public static class InstrumentSeedData
{
    public static List<Instrument> GetSeedInstruments()
    {
        return new List<Instrument>
        {
            // Stock Indices
            new Instrument
            {
                Id = 1,
                Symbol = "KSE100",
                Name = "KSE-100 Index",
                Type = InstrumentType.StockIndex,
                DataSourceUrl = "https://beta-restapi.sarmaaya.pk/api/indices/price-history/KSE100?days=365",
                DataSourceType = DataSourceType.JsonApi,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Instrument
            {
                Id = 2,
                Symbol = "KMI30",
                Name = "KMI-30 Index",
                Type = InstrumentType.StockIndex,
                DataSourceUrl = "https://beta-restapi.sarmaaya.pk/api/indices/price-history/KMI30?days=365",
                DataSourceType = DataSourceType.JsonApi,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            
            // Mutual Funds
            new Instrument
            {
                Id = 3,
                Symbol = "KMIF",
                Name = "KMI Islamic Fund",
                Type = InstrumentType.MutualFund,
                DataSourceUrl = "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/a0aac2a4-0ec4-4718-b2e5-4580f5cd8ddd?days=365",
                DataSourceType = DataSourceType.JsonApi,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Instrument
            {
                Id = 4,
                Symbol = "MIF",
                Name = "Meezan Islamic Fund",
                Type = InstrumentType.MutualFund,
                DataSourceUrl = "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/698bac51-81aa-4e0c-ad94-93d630e1e2a4?days=365",
                DataSourceType = DataSourceType.JsonApi,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Instrument
            {
                Id = 5,
                Symbol = "MBF",
                Name = "Meezan Balanced Fund",
                Type = InstrumentType.MutualFund,
                DataSourceUrl = "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/f6bcc825-99fe-47f8-a09c-805f9e46697c?days=365",
                DataSourceType = DataSourceType.JsonApi,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Instrument
            {
                Id = 6,
                Symbol = "MGF",
                Name = "Meezan Growth Fund",
                Type = InstrumentType.MutualFund,
                DataSourceUrl = "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/7e59207d-798c-43ab-bc76-4a44d2f90a64?days=365",
                DataSourceType = DataSourceType.JsonApi,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            
            // Forex
            new Instrument
            {
                Id = 7,
                Symbol = "USDPKR",
                Name = "USD/PKR Exchange Rate",
                Type = InstrumentType.Forex,
                DataSourceUrl = "https://beta-restapi.sarmaaya.pk/api/forex/price-history/USDPKR?days=1095",
                DataSourceType = DataSourceType.JsonApi,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            
            // Commodities
            new Instrument
            {
                Id = 8,
                Symbol = "GOLDINT",
                Name = "Gold (International) - XAU/USD",
                Type = InstrumentType.Commodity,
                DataSourceUrl = "https://www.investing.com/currencies/xau-usd-historical-data",
                DataSourceType = DataSourceType.HtmlScraper,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Instrument
            {
                Id = 9,
                Symbol = "GOLDPK",
                Name = "Gold (Pakistan)",
                Type = InstrumentType.Commodity,
                DataSourceUrl = "https://beta-restapi.sarmaaya.pk/api/commodities/price/gold?days=365",
                DataSourceType = DataSourceType.JsonApi,
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        };
    }
}