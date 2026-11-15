namespace MarketAnalytics.Application.Services;

/// <summary>
/// A company followed as an instrument. NamePattern finds the company in headlines (its news mood); NewsQuery is the
/// Google News search that collects its stories.
/// </summary>
public record TrackedStock(int Id, string Symbol, string Name, string Industry, string NewsQuery, string NamePattern);

/// <summary>
/// The ten Shariah-compliant companies with the best long-run records (scripts/research/stock-rankings.cs, Nov 2026),
/// followed like the funds. The one list the database seed, news collection and news topics read, so changing the
/// selection is one edit here plus a migration for the seed.
/// </summary>
public static class TrackedStocks
{
    // Name patterns avoid words used by other companies or people ("Ghandhara" alone is also Ghandhara Automobiles,
    // "Fatima" is a common name, "Systems" a common word)
    public static readonly IReadOnlyList<TrackedStock> All = new TrackedStock[]
    {
        new(14, "GHNI", "Ghandhara Industries Limited", "Automobile Assembler",
            "\"Ghandhara Industries\"", @"\b(Ghandhara Industries|GHNI)\b"),
        new(15, "SYS", "Systems Limited", "Technology and Communication",
            "\"Systems Limited\" OR \"Systems Ltd\" Pakistan", @"\b(Systems Limited|Systems Ltd)\b"),
        new(16, "SAZEW", "Sazgar Engineering Works Limited", "Automobile Assembler",
            "Sazgar", @"\b(Sazgar|SAZEW)\b"),
        new(17, "SRVI", "Service Industries Limited", "Leather and Tanneries",
            "\"Service Industries\" Pakistan OR Servis", @"\b(Service Industries|SRVI|Servis)\b"),
        new(18, "PIOC", "Pioneer Cement Limited", "Cement",
            "\"Pioneer Cement\"", @"\b(Pioneer Cement|PIOC)\b"),
        new(19, "JVDC", "Javedan Corporation Limited", "Property",
            "Javedan OR \"Naya Nazimabad\"", @"\b(Javedan|JVDC|Naya Nazimabad)\b"),
        new(20, "MARI", "Mari Energies Limited", "Oil and Gas Exploration Companies",
            "\"Mari Energies\" OR \"Mari Petroleum\"", @"\b(Mari Energies|Mari Petroleum)\b"),
        new(21, "FATIMA", "Fatima Fertilizer Company Limited", "Fertilizer",
            "\"Fatima Fertilizer\"", @"\b(Fatima Fertili[sz]er|Fatima Group)\b"),
        new(22, "MEBL", "Meezan Bank Limited", "Commercial Banks",
            "\"Meezan Bank\"", @"\b(Meezan Bank|MEBL)\b"),
        new(23, "DCR", "Dolmen City REIT", "Real Estate Investment Trust",
            "\"Dolmen City\"", @"\b(Dolmen City|Dolmen Mall)\b"),
    };

    public static TrackedStock? Find(string symbol) => All.FirstOrDefault(s => s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
}
