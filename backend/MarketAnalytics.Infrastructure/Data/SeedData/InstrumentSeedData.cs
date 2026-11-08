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
                Name = "KSE Meezan Index Fund",
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
                Name = "Meezan Gold Fund",
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
                DataSourceUrl = "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@{date}/v1/currencies/xau.json",
                DataSourceType = DataSourceType.CurrencyApi,
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
            },
            // Islamic money market fund: what cash earns, for backtests and the portfolio mix
            new Instrument
            {
                Id = 10,
                Symbol = "MCF",
                Name = "Meezan Cash Fund",
                Type = InstrumentType.MutualFund,
                DataSourceUrl = "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/a0674272-8a64-4fb8-b708-4e60a1e7f135?days=365",
                DataSourceType = DataSourceType.JsonApi,
                IsActive = true,
                CreatedAt = new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc)
            },
            // More low-risk Meezan funds: Rozana Amdani keeps its price at Rs50 and pays its return out daily
            new Instrument
            {
                Id = 11,
                Symbol = "MRAF",
                Name = "Meezan Rozana Amdani Fund",
                Type = InstrumentType.MutualFund,
                DataSourceUrl = "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/5385b72f-44fe-4f98-bee3-0f275a05b148?days=365",
                DataSourceType = DataSourceType.JsonApi,
                IsActive = true,
                CreatedAt = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc)
            },
            new Instrument
            {
                Id = 12,
                Symbol = "MIIF",
                Name = "Meezan Islamic Income Fund",
                Type = InstrumentType.MutualFund,
                DataSourceUrl = "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/b4323cd2-b729-4122-9678-7150f95c868a?days=365",
                DataSourceType = DataSourceType.JsonApi,
                IsActive = true,
                CreatedAt = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc)
            },
            new Instrument
            {
                Id = 13,
                Symbol = "MMP-I",
                Name = "Meezan Daily Income Fund - Meezan Munafa Plan I",
                Type = InstrumentType.MutualFund,
                DataSourceUrl = "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/4021c591-4da4-48f5-ba76-5b7b6059298c?days=365",
                DataSourceType = DataSourceType.JsonApi,
                IsActive = true,
                CreatedAt = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc)
            }
        };
    }
}