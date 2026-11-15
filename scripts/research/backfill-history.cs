#:property PublishAot=false
#:property EnableConfigurationBindingGenerator=false
#:project ../../backend/MarketAnalytics.Infrastructure/MarketAnalytics.Infrastructure.csproj
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Infrastructure.Data;
using MarketAnalytics.Infrastructure.DataFetchers;
using MarketAnalytics.Infrastructure.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

// One-off: adds the older prices a source has from before the first price stored, for instruments added with only
// recent history. The regular update only ever fetches forward. First checks that the source agrees with the prices
// already stored where the two overlap.
// Usage: dotnet run backfill-history.cs          (report only)
//        dotnet run backfill-history.cs write    (adds the older prices)

var write = args.Contains("write");
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("MA_CONN")).Options;
using var db = new ApplicationDbContext(options);
var fetcher = new JsonApiDataFetcher(new HttpClient { Timeout = TimeSpan.FromMinutes(3) }, NullLogger<JsonApiDataFetcher>.Instance);

foreach (var instrument in await db.Instruments.Where(i => i.IsActive && i.DataSourceType == DataSourceType.JsonApi).OrderBy(i => i.Symbol).ToListAsync())
{
    var stored = await db.PricePoints.AsNoTracking().Where(p => p.InstrumentId == instrument.Id)
        .ToDictionaryAsync(p => p.DateTime, p => p.Price);
    if (stored.Count == 0) continue;
    var first = stored.Keys.Min();

    List<MarketAnalytics.Domain.ValueObjects.PriceDataPoint> source;
    try { source = (await fetcher.FetchHistoricalDataAsync(MarketDataUpdater.FullHistoryUrl(instrument.DataSourceUrl), instrument.Symbol)).ToList(); }
    catch (Exception ex) { Console.WriteLine($"{instrument.Symbol,-7} fetch failed: {ex.Message}"); continue; }

    // Agreement where both have a price: share of days within 0.5%
    var overlap = source.Where(s => stored.ContainsKey(s.DateTime)).ToList();
    var agree = overlap.Count(s => Math.Abs((double)(s.Price / stored[s.DateTime]) - 1) <= 0.005);
    // Days before the first stored price, and any day missing in between
    var older = source.Where(s => !stored.ContainsKey(s.DateTime) && s.DateTime <= stored.Keys.Max() && s.Price > 0).GroupBy(s => s.DateTime).Select(g => g.Last()).OrderBy(s => s.DateTime).ToList();
    var perYear = string.Join(" ", older.GroupBy(s => s.DateTime.Year).Select(g => $"{g.Key}:{g.Count()}"));
    Console.WriteLine($"{instrument.Symbol,-7} stored from {first:yyyy-MM-dd}; overlap {overlap.Count} days, {(overlap.Count == 0 ? 0 : 100.0 * agree / overlap.Count):0.0}% agree; older days available {older.Count}");
    if (older.Count > 0) Console.WriteLine($"        {perYear}");

    // Only daily history: a monthly series (gold before 2019) would distort averages and signals built for days
    // (at least 10 prices a month, so a partial first year still counts)
    older = older.Where(s => older.Count(o => o.DateTime.Year == s.DateTime.Year && o.DateTime.Month == s.DateTime.Month) >= 10).ToList();
    if (!write || older.Count == 0) continue;
    if (overlap.Count < 20 || agree < 0.95 * overlap.Count)
    {
        Console.WriteLine("        skipped: the source doesn't agree well enough with the stored prices");
        continue;
    }
    db.PricePoints.AddRange(older.Select(s => new PricePoint { InstrumentId = instrument.Id, DateTime = s.DateTime, Price = s.Price }));
    await db.SaveChangesAsync();
    Console.WriteLine($"        added {older.Count} older prices");
}
