#:property PublishAot=false
#:property EnableConfigurationBindingGenerator=false
#:project ../../backend/MarketAnalytics.Application/MarketAnalytics.Application.csproj
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.ValueObjects;

// Best past performers among Shariah-compliant PSX companies: top 5 per industry, the best industry, and a final 10.
// Criteria written down before running (Nov 14 2026), chosen by the user's brief ("all-time knowledge", Shariah only).
//
// Universe: the KMI All-Share index's current members (Shariah-compliant today), with their PSX sectors (Sarmaaya
//   indices/sunbrust/KMIALLSHR). Prices: Sarmaaya stocks/price-history, as far back as it goes (2002 at most).
// Cleaning: zero prices dropped; one-day spikes removed (the app's PriceDataCleaner); then any day-to-day move below
//   -40% or above +67% is treated as a split, bonus issue or data change (PSX's daily price limits make such moves
//   impossible in normal trading) and earlier prices are rescaled to remove it. Cash dividends are NOT included
//   (the source lists them only from about 2022), so every return here is price only, like the KSE-100 index used as
//   the yardstick, which also leaves out dividends. High-dividend companies are therefore understated.
// Eligible: at least 5 years of prices (first price on or before 2021-11-14) AND enough trading to buy and sell:
//   median daily traded value (price x shares) over the last 250 trading days of at least Rs 10 million.
// Measures, over each company's own full history, against the KSE-100 over exactly the same dates:
//   A. extra return a year: the company's yearly growth minus the KSE-100's;
//   B. consistency: the share of full calendar years in which it beat the KSE-100;
//   C. return for the pain: its yearly growth divided by the size of its worst fall.
// Score: the average of its percentile ranks (0-100, within all eligible companies) on A, B and C, equal weights.
// Top 5 per industry: the 5 highest scores among the industry's eligible companies (fewer if it has fewer).
// Best industry: the highest median score among industries with at least 3 eligible companies.
// Final 10: the highest scores overall, at most 2 from any one industry.
// This ranks the past. Past winners don't reliably keep winning; today's Shariah list leaves out companies that
// failed or were delisted (survivorship), which flatters every figure. The list is a starting point to study, not a
// recommendation.
// Output: console report, and frontend/public/data/stock-rankings.json for the Rankings page.
// Usage: dotnet run stock-rankings.cs

const string Base = "https://beta-restapi.sarmaaya.pk/api";
var asOf = DateTime.UtcNow.Date;
var minFirstDate = new DateTime(2021, 11, 14);
const double MinTradedValue = 10_000_000, SplitDown = 0.6, SplitUp = 1.67;

var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

async Task<JsonNode?> Get(string path)
{
    for (int attempt = 1; ; attempt++)
    {
        try { return JsonNode.Parse(await http.GetStringAsync(Base + path))?["response"]; }
        catch (Exception) when (attempt < 3) { await Task.Delay(2000 * attempt); }
    }
}

// Universe and sectors
var members = new List<(string Symbol, string Sector)>();
foreach (var sector in (await Get("/indices/sunbrust/KMIALLSHR"))!["sectors"]!.AsArray())
    foreach (var company in sector!["companies"]!.AsArray())
        members.Add((company!["symbol"]!.GetValue<string>(), Title(sector["sectorName"]!.GetValue<string>())));
Console.WriteLine($"Universe: {members.Count} Shariah-compliant companies in {members.Select(m => m.Sector).Distinct().Count()} industries");

List<(DateTime Date, double Price, double Volume)> Parse(JsonNode? rows) =>
    rows!.AsArray()
        .Select(r => (Date: DateTime.Parse(r!["date"]!.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal).Date,
                      Price: r["price"]?.GetValue<double>() ?? 0,
                      Volume: r["volume"]?.GetValue<double>() ?? 0))
        .Where(r => r.Price > 0)
        .GroupBy(r => r.Date).Select(g => g.Last())
        .OrderBy(r => r.Date).ToList();

var index = Parse(await Get("/indices/price-history/KSE100?days=10000"));
var indexByDate = index.ToDictionary(r => r.Date, r => r.Price);
var indexDates = index.Select(r => r.Date).ToList();
double IndexOn(DateTime day) { var i = indexDates.BinarySearch(day); i = i >= 0 ? i : ~i - 1; return i < 0 ? double.NaN : indexByDate[indexDates[i]]; }

var results = new List<Result>();
var skipped = new List<string>();
foreach (var (symbol, sector) in members)
{
    List<(DateTime Date, double Price, double Volume)> rows;
    try { rows = Parse(await Get($"/stocks/price-history/{symbol}?days=10000")); }
    catch (Exception ex) { skipped.Add($"{symbol}: fetch failed ({ex.Message})"); continue; }
    if (rows.Count < 250) { skipped.Add($"{symbol}: {rows.Count} prices"); continue; }

    // Spikes out, then rescale across splits and bonus issues
    var cleaned = PriceDataCleaner.RemoveSpikes(rows.Select(r => new PriceDataPoint(r.Date, (decimal)r.Price))).ToList();
    var dates = cleaned.Select(p => p.DateTime.Date).ToList();
    var prices = cleaned.Select(p => (double)p.Price).ToArray();
    var adjustments = 0;
    for (int i = prices.Length - 1; i > 0; i--)
    {
        var ratio = prices[i] / prices[i - 1];
        if (ratio is > SplitDown and < SplitUp) continue;
        for (int k = 0; k < i; k++) prices[k] *= ratio;
        adjustments++;
    }

    var traded = rows.TakeLast(250).Select(r => r.Price * r.Volume).OrderBy(v => v).ToList();
    var medianTraded = traded[traded.Count / 2];
    var first = dates[0]; var last = dates[^1];
    var years = (last - first).TotalDays / 365.25;

    var stockGrowth = Math.Pow(prices[^1] / prices[0], 1 / years) - 1;
    var indexGrowth = Math.Pow(IndexOn(last) / IndexOn(first), 1 / years) - 1;

    double peak = 0, worst = 0;
    foreach (var p in prices) { peak = Math.Max(peak, p); worst = Math.Min(worst, p / peak - 1); }

    // Full calendar years only
    int yearsCounted = 0, yearsBeat = 0;
    for (var y = first.Year + 1; y < last.Year; y++)
    {
        var s0 = PriceOnOrBefore(dates, prices, new DateTime(y - 1, 12, 31)); var s1 = PriceOnOrBefore(dates, prices, new DateTime(y, 12, 31));
        var i0 = IndexOn(new DateTime(y - 1, 12, 31)); var i1 = IndexOn(new DateTime(y, 12, 31));
        if (double.IsNaN(s0) || double.IsNaN(i0)) continue;
        yearsCounted++;
        if (s1 / s0 > i1 / i0) yearsBeat++;
    }

    results.Add(new Result
    {
        Symbol = symbol, Sector = sector, FirstDate = first, LastDate = last, Years = Math.Round(years, 1),
        GrowthPercent = Math.Round(stockGrowth * 100, 1), IndexGrowthPercent = Math.Round(indexGrowth * 100, 1),
        ExtraPercent = Math.Round((stockGrowth - indexGrowth) * 100, 1),
        YearsCounted = yearsCounted, YearsBeat = yearsBeat,
        BeatSharePercent = yearsCounted == 0 ? 0 : Math.Round(100.0 * yearsBeat / yearsCounted, 0),
        WorstFallPercent = Math.Round(worst * 100, 1),
        ReturnForPain = worst < 0 ? Math.Round(stockGrowth / -worst, 3) : 0,
        MedianTradedRupees = Math.Round(medianTraded),
        Adjustments = adjustments,
        Eligible = first <= minFirstDate && medianTraded >= MinTradedValue && yearsCounted > 0,
    });
    await Task.Delay(150); // be gentle with the source
}

// Percentile scores within the eligible set
var eligible = results.Where(r => r.Eligible).ToList();
double Pct(IEnumerable<double> all, double v) { var l = all.ToList(); return 100.0 * l.Count(x => x < v) / Math.Max(1, l.Count - 1); }
foreach (var r in eligible)
    r.Score = Math.Round((Pct(eligible.Select(e => e.ExtraPercent), r.ExtraPercent)
        + Pct(eligible.Select(e => e.BeatSharePercent), r.BeatSharePercent)
        + Pct(eligible.Select(e => e.ReturnForPain), r.ReturnForPain)) / 3, 1);

var industries = eligible.GroupBy(r => r.Sector)
    .Select(g => new Industry
    {
        Name = g.Key,
        Eligible = g.Count(),
        Members = results.Count(r => r.Sector == g.Key),
        MedianScore = g.Count() >= 3 ? Math.Round(Median(g.Select(r => r.Score)), 1) : null,
        MedianExtraPercent = Math.Round(Median(g.Select(r => r.ExtraPercent)), 1),
        Top = g.OrderByDescending(r => r.Score).Take(5).ToList(),
    })
    .OrderByDescending(i => i.MedianScore ?? -1).ThenByDescending(i => i.Top.Max(t => t.Score))
    .ToList();

var final = new List<Result>();
foreach (var r in eligible.OrderByDescending(r => r.Score))
    if (final.Count < 10 && final.Count(f => f.Sector == r.Sector) < 2)
        final.Add(r);

// Company names for everything shown
var shown = industries.SelectMany(i => i.Top).Concat(final).DistinctBy(r => r.Symbol).ToList();
foreach (var r in shown)
{
    try
    {
        var page = await http.GetStringAsync($"https://sarmaaya.pk/stocks/{r.Symbol}");
        var m = Regex.Match(page, @"<title>[^|]*\|\s*([^|<]+?)\s*\|");
        r.Name = m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value) : r.Symbol;
    }
    catch (HttpRequestException) { r.Name = r.Symbol; }
    await Task.Delay(150);
}

Console.WriteLine($"Prices for {results.Count}; eligible {eligible.Count}; skipped {skipped.Count}");
Console.WriteLine($"Adjusted for splits/bonus issues: {results.Count(r => r.Adjustments > 0)} companies; 4+ adjustments: {string.Join(", ", results.Where(r => r.Adjustments >= 4).Select(r => $"{r.Symbol}({r.Adjustments})"))}");
Console.WriteLine("\nINDUSTRIES (median score, eligible/members, median extra return a year)");
foreach (var i in industries)
{
    Console.WriteLine($"{(i.MedianScore?.ToString("0.0") ?? "  -  "),6}  {i.Name} ({i.Eligible}/{i.Members}, {i.MedianExtraPercent:+0.0;-0.0})");
    foreach (var t in i.Top)
        Console.WriteLine($"        {t.Score,5:0.0} {t.Symbol,-8} {t.Name}: {t.GrowthPercent:+0.0;-0.0}%/yr vs index {t.IndexGrowthPercent:+0.0}% since {t.FirstDate:yyyy}, beat it {t.YearsBeat}/{t.YearsCounted} yrs, worst fall {t.WorstFallPercent}%");
}
Console.WriteLine("\nFINAL 10");
foreach (var t in final)
    Console.WriteLine($"  {t.Score,5:0.0} {t.Symbol,-8} {t.Name} ({t.Sector}): {t.GrowthPercent:+0.0;-0.0}%/yr vs {t.IndexGrowthPercent:+0.0}% since {t.FirstDate:yyyy}, beat {t.YearsBeat}/{t.YearsCounted} yrs, worst {t.WorstFallPercent}%");

var output = new
{
    asOf = asOf.ToString("yyyy-MM-dd"),
    universe = members.Count,
    eligible = eligible.Count,
    criteria = new[]
    {
        "Shariah-compliant companies in the KMI All-Share index today.",
        "At least 5 years of prices, and at least Rs 10 million traded on a typical day over the last year.",
        "Scored equally on: extra growth a year over the KSE-100 (same dates); share of calendar years it beat the KSE-100; growth for the size of its worst fall.",
        "Prices are adjusted for splits and bonus shares but leave out cash dividends, like the KSE-100 itself.",
    },
    industries = industries.Select(i => new
    {
        i.Name, i.Eligible, i.Members, i.MedianScore, i.MedianExtraPercent,
        top = i.Top.Select(Row),
    }),
    final = final.Select(Row),
};
var path = Path.Combine("..", "..", "frontend", "public", "data", "stock-rankings.json");
Directory.CreateDirectory(Path.GetDirectoryName(path)!);
File.WriteAllText(path, JsonSerializer.Serialize(output, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
Console.WriteLine($"\nWrote {path}");

object Row(Result r) => new
{
    r.Symbol, r.Name, r.Sector, r.Score, firstYear = r.FirstDate.Year, r.Years, r.GrowthPercent, r.IndexGrowthPercent,
    r.ExtraPercent, r.YearsBeat, r.YearsCounted, r.WorstFallPercent,
};

static double PriceOnOrBefore(List<DateTime> dates, double[] prices, DateTime day)
{
    var i = dates.BinarySearch(day); i = i >= 0 ? i : ~i - 1;
    return i < 0 ? double.NaN : prices[i];
}

static double Median(IEnumerable<double> values)
{
    var l = values.OrderBy(v => v).ToList();
    return l.Count == 0 ? double.NaN : l.Count % 2 == 1 ? l[l.Count / 2] : (l[l.Count / 2 - 1] + l[l.Count / 2]) / 2;
}

static string Title(string sector) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(sector.ToLowerInvariant())
    .Replace(" & ", " and ").Replace("Inv. ", "Investment ").Replace("Cos.", "Companies");

class Result
{
    public string Symbol = "", Sector = "", Name = "";
    public DateTime FirstDate, LastDate;
    public double Years, GrowthPercent, IndexGrowthPercent, ExtraPercent, BeatSharePercent, WorstFallPercent, ReturnForPain, MedianTradedRupees, Score;
    public int YearsCounted, YearsBeat, Adjustments;
    public bool Eligible;
}

class Industry
{
    public string Name = "";
    public int Eligible, Members;
    public double? MedianScore;
    public double MedianExtraPercent;
    public List<Result> Top = new();
}
