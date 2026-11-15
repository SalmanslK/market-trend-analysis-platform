#:property PublishAot=false
#:property EnableConfigurationBindingGenerator=false
#:project ../../backend/MarketAnalytics.Infrastructure/MarketAnalytics.Infrastructure.csproj
#:project ../../backend/MarketAnalytics.Application/MarketAnalytics.Application.csproj
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.ValueObjects;
using MarketAnalytics.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

// Does the news mood tell us anything about FUTURE prices, or only describe past ones?
// Uses the app's current sentiment (NewsService.ScoreForSymbol). Forward moves start at the NEXT trading day's close,
// so a story published after the close can't be acted on at that day's close (no look-ahead).
// Read-only. Run from this folder, with the database connection string in the MA_CONN environment variable:
//   dotnet run news-forward-research.cs

string[] symbols = { "KSE100", "KMI30", "MIF", "GOLDPK", "GOLDINT", "MGF", "USDPKR" };
int[] horizons = { 1, 5, 20 };

var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("MA_CONN")).Options;
using var db = new ApplicationDbContext(options);
var articles = await db.NewsArticles.AsNoTracking().ToListAsync();

Console.WriteLine("Correlation of the 7-day news mood with price moves (r; * = unlikely to be luck, |t| > 2 after allowing for overlapping periods)");
Console.WriteLine($"{"",-8}{"days",6} | {"past week",10}{"same day",10} | {"next 1d",9}{"next 5d",9}{"next 20d",10} | {"mood change vs next 5d",24} | {"next 5d: 1st half / 2nd half",30}");

var pooled = new Dictionary<string, List<(double X, double Y)>>();
var hits = new List<string>();
var momentum = new List<string>();
foreach (var symbol in symbols)
{
    var inst = await db.Instruments.SingleAsync(i => i.Symbol == symbol);
    var points = await db.PricePoints.AsNoTracking().Where(p => p.InstrumentId == inst.Id).OrderBy(p => p.DateTime).ToListAsync();
    var payouts = await db.FundPayouts.AsNoTracking().Where(p => p.InstrumentId == inst.Id).ToListAsync();
    var prices = PayoutAdjuster.Adjust(PriceDataCleaner.RemoveSpikes(points.Select(p => new PriceDataPoint(p.DateTime, p.Price))), payouts);
    var dates = prices.Select(p => p.DateTime).ToList();
    var closes = prices.Select(p => (double)p.Price).ToList();
    var news = NewsService.ScoreForSymbol(articles, symbol);
    var moods = NewsService.DailyMood(dates, news, SignalRules.NewsWindowDays);

    // Days with a usable reading (at least 3 stories), leaving room to look 20 days ahead from the next close
    var days = Enumerable.Range(6, dates.Count - 6 - 21).Where(i => moods[i].Articles >= SignalRules.MinNewsArticles).ToList();
    double Ret(int from, int to) => closes[to] / closes[from] - 1;

    (double R, bool Sure) Corr(List<(double X, double Y)> pairs, int overlap)
    {
        var r = Pearson(pairs);
        var n = pairs.Count / (double)Math.Max(1, overlap);
        var t = r * Math.Sqrt(Math.Max(1, n - 2) / Math.Max(1e-9, 1 - r * r));
        return (r, Math.Abs(t) > 2);
    }
    string Show((double R, bool Sure) c) => $"{c.R,7:+0.00;-0.00}{(c.Sure ? "*" : " ")}";

    var pastWeek = Corr(days.Select(i => ((double)moods[i].Score, Ret(i - 5, i))).ToList(), 5);
    var sameDay = Corr(days.Select(i => ((double)moods[i].Score, Ret(i - 1, i))).ToList(), 1);
    var forward = horizons.ToDictionary(h => h, h => Corr(days.Select(i => ((double)moods[i].Score, Ret(i + 1, i + 1 + h))).ToList(), h));
    var change = Corr(days.Where(i => moods[i - 5].Articles >= SignalRules.MinNewsArticles)
        .Select(i => ((double)(moods[i].Score - moods[i - 5].Score), Ret(i + 1, i + 6))).ToList(), 5);
    var half = days.Count / 2;
    var first = Corr(days.Take(half).Select(i => ((double)moods[i].Score, Ret(i + 1, i + 6))).ToList(), 5);
    var second = Corr(days.Skip(half).Select(i => ((double)moods[i].Score, Ret(i + 1, i + 6))).ToList(), 5);

    Console.WriteLine($"{symbol,-8}{days.Count,6} | {Show(pastWeek),10}{Show(sameDay),10} | {Show(forward[1]),9}{Show(forward[5]),9}{Show(forward[20]),10} | {Show(change),24} | {Show(first),15} / {Show(second),-12}");

    // Is a link with the next month just momentum? News is positive because prices rose, and rises often continue.
    // Compare the past week's own price move with the next 20 days, and the news once that move is taken out.
    var month = days.Where(i => i + 21 < closes.Count).ToList();
    var moodVsMonth = Pearson(month.Select(i => ((double)moods[i].Score, Ret(i + 1, i + 21))).ToList());
    var priceVsMonth = Pearson(month.Select(i => (Ret(i - 5, i), Ret(i + 1, i + 21))).ToList());
    var moodVsPrice = Pearson(month.Select(i => ((double)moods[i].Score, Ret(i - 5, i))).ToList());
    var partial = (moodVsMonth - priceVsMonth * moodVsPrice) / Math.Sqrt((1 - priceVsMonth * priceVsMonth) * (1 - moodVsPrice * moodVsPrice));
    momentum.Add($"{symbol,-8} news vs next 20d {moodVsMonth,6:+0.00;-0.00}   past week's price move vs next 20d {priceVsMonth,6:+0.00;-0.00}   news with the price move taken out {partial,6:+0.00;-0.00}");

    foreach (var (key, list) in new[] { ("next 5d", days.Select(i => ((double)moods[i].Score, Ret(i + 1, i + 6)))), ("same day", days.Select(i => ((double)moods[i].Score, Ret(i - 1, i)))) })
    {
        if (!pooled.ContainsKey(key)) pooled[key] = new();
        pooled[key].AddRange(list);
    }

    // The practical question: after news says Buy or Sell, what happened next?
    string After(Func<int, bool> when, int h)
    {
        var moves = days.Where(when).Select(i => Ret(i + 1, i + 1 + h) * 100).ToList();
        return moves.Count == 0 ? "-" : $"{moves.Average(),6:+0.00;-0.00}% ({100.0 * moves.Count(m => m > 0) / moves.Count,3:0}% up, n={moves.Count})";
    }
    hits.Add($"{symbol,-8} 5d after Buy {After(i => moods[i].Score >= 20, 5),-30} after Sell {After(i => moods[i].Score <= -20, 5),-30} any day {After(_ => true, 5)}");
    hits.Add($"{"",-8} 20d after Buy {After(i => moods[i].Score >= 20, 20),-29} after Sell {After(i => moods[i].Score <= -20, 20),-30} any day {After(_ => true, 20)}");
}

Console.WriteLine($"\nAll instruments pooled: same day r = {Pearson(pooled["same day"]):+0.00;-0.00}, next 5 days r = {Pearson(pooled["next 5d"]):+0.00;-0.00}");
Console.WriteLine("\nAverage move after the news says Buy or Sell (from the next close), against any day with news:");
hits.ForEach(Console.WriteLine);
Console.WriteLine("\nIs the next-month link just momentum?");
momentum.ForEach(Console.WriteLine);

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
