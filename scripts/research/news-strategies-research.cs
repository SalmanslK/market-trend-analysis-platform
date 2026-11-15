#:property PublishAot=false
#:property EnableConfigurationBindingGenerator=false
#:project ../../backend/MarketAnalytics.Infrastructure/MarketAnalytics.Infrastructure.csproj
#:project ../../backend/MarketAnalytics.Application/MarketAnalytics.Application.csproj
using System.Text.RegularExpressions;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.ValueObjects;
using MarketAnalytics.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

// Can any way of reading the news predict FUTURE prices? Written down before looking at any result (Nov 9 2026):
//
// Ideas (each a daily score; positive should mean prices rise next):
//   V0 current     the app's 7-day news mood (baseline)
//   V1 events      7-day mood from event news only: price-report headlines ("PSX gains 1,000 points", "gold rate
//                  today") are left out, because they describe moves already made
//   V2 surprise    7-day mood minus its own average over the previous 60 trading days
//   V3 disagree    7-day mood on days when it disagrees with the past week's price move, otherwise 0
//   V4 contrarian  minus the 7-day mood
//   V5 attention   story count against its 60-day average (tests whether busy news days come before moves)
//   V6 slow        30-day mood
// Horizons: price change over the next 5 and next 20 trading days, starting at the NEXT day's close (no look-ahead).
// Groups: stocks (KSE100, KMI30, MIF), gold (GOLDPK, GOLDINT, MGF), rupee (USDPKR).
// Split: development = the first 70% of the news period; holdout = the last 30%, looked at once, only in phase 2.
// Pass in development: pooled correlation in a group with |t| > 2, where t counts one independent observation per
//   horizon on ONE instrument (instruments in a group share their news, so they aren't independent), and the same sign
//   on at least 2 of the 3 instruments.
// Pass in holdout: same sign, pooled t > 1.5 (the holdout is shorter).
// Usage: dotnet run news-strategies-research.cs           (development only)
//        dotnet run news-strategies-research.cs holdout   (adds the holdout, once the development result is recorded)

var showHoldout = args.Contains("holdout");
string[][] groups = { new[] { "KSE100", "KMI30", "MIF" }, new[] { "GOLDPK", "GOLDINT", "MGF" }, new[] { "USDPKR" } };
string[] groupNames = { "stocks", "gold", "rupee" };
string[] variants = { "V0 current", "V1 events", "V2 surprise", "V3 disagree", "V4 contrarian", "V5 attention", "V6 slow" };
int[] horizons = { 5, 20 };

// Headlines that report a price move or a price level rather than a cause
var priceReport = new Regex(
    @"\b(points?|pts|closes?|closed|closing|session|intraday|in early trade|bourse|bulls|bears|profit-taking|" +
    @"per tola|tola|rates? today|price today|prices today|today'?s? (gold|dollar)|interbank|open market|paisa|paise|" +
    @"(gains?|rises?|falls?|drops?|sheds?|surges?|jumps?|slips?|plunges?|tumbles?|climbs?|soars?|dips?) (by )?(rs\.?\s?)?[\d,.]+|" +
    @"record high|all-time high|lowest|highest level|weekly review|market wrap|stocks? (rally|slide))\b",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("MA_CONN")).Options;
using var db = new ApplicationDbContext(options);
var articles = await db.NewsArticles.AsNoTracking().ToListAsync();
var eventArticles = articles.Where(a => !priceReport.IsMatch(a.Title)).ToList();
Console.WriteLine($"Stories: {articles.Count}; event news (no price report in the title): {eventArticles.Count} ({100.0 * eventArticles.Count / articles.Count:0}%)");

// One cut-off date for all instruments: 70% of the way through the news period
var firstNews = articles.Min(a => a.PublishedAt);
var lastNews = articles.Max(a => a.PublishedAt);
var cutoff = firstNews + TimeSpan.FromTicks((long)((lastNews - firstNews).Ticks * 0.7));
Console.WriteLine($"Development: {firstNews:yyyy-MM-dd} to {cutoff:yyyy-MM-dd}; holdout: after {cutoff:yyyy-MM-dd}{(showHoldout ? "" : " (not shown)")}\n");

// samples[variant][group][horizon][period] -> (instrument, score, forward move)
var samples = new Dictionary<(int V, int G, int H, bool Holdout), List<(string Symbol, double X, double Y)>>();
for (int g = 0; g < groups.Length; g++)
{
    foreach (var symbol in groups[g])
    {
        var inst = await db.Instruments.SingleAsync(i => i.Symbol == symbol);
        var points = await db.PricePoints.AsNoTracking().Where(p => p.InstrumentId == inst.Id).OrderBy(p => p.DateTime).ToListAsync();
        var payouts = await db.FundPayouts.AsNoTracking().Where(p => p.InstrumentId == inst.Id).ToListAsync();
        var prices = PayoutAdjuster.Adjust(PriceDataCleaner.RemoveSpikes(points.Select(p => new PriceDataPoint(p.DateTime, p.Price))), payouts);
        var dates = prices.Select(p => p.DateTime).ToList();
        var closes = prices.Select(p => (double)p.Price).ToList();
        var all = NewsService.ScoreForSymbol(articles, symbol);
        var events = NewsService.ScoreForSymbol(eventArticles, symbol);
        var mood7 = NewsService.DailyMood(dates, all, SignalRules.NewsWindowDays);
        var moodEvents = NewsService.DailyMood(dates, events, SignalRules.NewsWindowDays);
        var mood30 = NewsService.DailyMood(dates, all, 30);
        double Ret(int from, int to) => closes[to] / closes[from] - 1;

        for (int i = 65; i < dates.Count - 22; i++)
        {
            if (mood7[i].Articles < SignalRules.MinNewsArticles) continue;
            var previous = Enumerable.Range(i - 60, 60).Where(j => mood7[j].Articles >= SignalRules.MinNewsArticles).ToList();
            if (previous.Count < 20) continue;
            var pastWeek = Ret(i - 5, i);
            double?[] x =
            {
                mood7[i].Score,
                moodEvents[i].Articles >= SignalRules.MinNewsArticles ? moodEvents[i].Score : null,
                mood7[i].Score - previous.Average(j => (double)mood7[j].Score),
                Math.Sign(mood7[i].Score) != Math.Sign(pastWeek) ? mood7[i].Score : 0,
                -mood7[i].Score,
                mood7[i].Articles - previous.Average(j => (double)mood7[j].Articles),
                mood30[i].Articles >= SignalRules.MinNewsArticles ? mood30[i].Score : null,
            };
            var holdout = dates[i] > cutoff;
            for (int v = 0; v < variants.Length; v++)
            {
                if (x[v] is not double value) continue;
                foreach (var h in horizons)
                {
                    var key = (v, g, h, holdout);
                    if (!samples.ContainsKey(key)) samples[key] = new();
                    samples[key].Add((symbol, value, Ret(i + 1, i + 1 + h)));
                }
            }
        }
    }
}

foreach (var period in showHoldout ? new[] { false, true } : new[] { false })
{
    Console.WriteLine(period ? "=== HOLDOUT (last 30%, looked at once)" : "=== DEVELOPMENT (first 70%)");
    Console.WriteLine($"{"",-15}" + string.Concat(groupNames.SelectMany(g => horizons.Select(h => $"{$"{g} {h}d",17}"))));
    for (int v = 0; v < variants.Length; v++)
    {
        var line = $"{variants[v],-15}";
        for (int g = 0; g < groups.Length; g++)
        {
            foreach (var h in horizons)
            {
                if (!samples.TryGetValue((v, g, h, period), out var list) || list.Count < 30) { line += $"{"-",17}"; continue; }
                var r = Pearson(list.Select(p => (p.X, p.Y)).ToList());
                // One independent observation per horizon on one instrument: the group's instruments share their news
                var nEff = list.Count / (double)groups[g].Length / h;
                var t = r * Math.Sqrt(Math.Max(1, nEff - 2) / Math.Max(1e-9, 1 - r * r));
                var agree = groups[g].Count(s => Math.Sign(Pearson(list.Where(p => p.Symbol == s).Select(p => (p.X, p.Y)).ToList())) == Math.Sign(r));
                var pass = period ? t * 1 > 1.5 || t < -1.5 : Math.Abs(t) > 2 && agree >= Math.Min(2, groups[g].Length);
                line += $"{$"{r:+0.00;-0.00} t{t:+0.0;-0.0}{(pass ? "*" : " ")}",17}";
            }
        }
        Console.WriteLine(line);
    }
    Console.WriteLine();
}
Console.WriteLine("r = correlation of the score with the next move; t = strength allowing for overlap and shared news; * = passes the rule set in advance.");

static double Pearson(List<(double X, double Y)> pairs)
{
    if (pairs.Count < 3) return 0;
    var mx = pairs.Average(p => p.X);
    var my = pairs.Average(p => p.Y);
    var cov = pairs.Sum(p => (p.X - mx) * (p.Y - my));
    var vx = pairs.Sum(p => (p.X - mx) * (p.X - mx));
    var vy = pairs.Sum(p => (p.Y - my) * (p.Y - my));
    return vx == 0 || vy == 0 ? 0 : cov / Math.Sqrt(vx * vy);
}
