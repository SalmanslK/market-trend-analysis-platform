#:property PublishAot=false
#:property EnableConfigurationBindingGenerator=false
#:project ../../backend/MarketAnalytics.Infrastructure/MarketAnalytics.Infrastructure.csproj
#:project ../../backend/MarketAnalytics.Application/MarketAnalytics.Application.csproj
using System.Globalization;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.ValueObjects;
using MarketAnalytics.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

// Two well-known ways of deciding HOW MUCH to hold in stocks, tested on Meezan Islamic Fund (MIF, payouts included,
// 2005 on). Written down before running (Nov 9 2026). Neither rule has a setting chosen from this data: both use the
// textbook defaults.
//
// R1 calm-or-jumpy (volatility targeting): each day, compare the last 30 days' volatility of MIF's daily returns with
//    its median over the past year (both measured only on data up to that day). Stocks share = past-year median / recent,
//    capped at 100%, rounded down to a 25% step; the rest is cash. The share changes only when the step changes.
// R2 stronger-than-cash (12-month momentum): on the first trading day of each month, hold MIF if its return over the
//    past 12 months beat cash over the same 12 months, otherwise hold cash until the next month.
// Cash earns the State Bank policy rate minus 1% (data/sbp-policy-rate.csv). Trading costs 0.5% of the amount moved.
// Decisions use that day's close and take effect from the next day (no look-ahead).
// Compared with holding MIF throughout, from the first day each rule can act.
// Split: development = to the end of 2015; holdout = 2016 onward, looked at once, only for a rule that passes development.
// Pass (each period): the rule's worst fall is at least 25% smaller than holding's (e.g. -45% or better against -60%)
//    AND its yearly return is no more than 2 points below holding's.
// Information only (no pass or fail, too short): R2 extended to the strongest of stocks (KSE-100 fund proxy MIF), gold
//    (GOLDPK) and cash, from 2021 when gold has 12 months of history.
// Usage: dotnet run risk-rules-research.cs           (development only)
//        dotnet run risk-rules-research.cs holdout   (adds the holdout)

var showHoldout = args.Contains("holdout");
const double Cost = 0.005, CashFee = 0.01;
var holdoutStart = new DateTime(2016, 1, 1);

var decisions = File.ReadAllLines(Path.Combine("data", "sbp-policy-rate.csv"))
    .Where(l => l.Length > 0 && char.IsDigit(l[0])).Select(l => l.Split(','))
    .Select(f => (Date: DateTime.Parse(f[0], CultureInfo.InvariantCulture), Rate: double.Parse(f[1], CultureInfo.InvariantCulture)))
    .OrderBy(d => d.Date).ToList();
double RateOn(DateTime day) => decisions.LastOrDefault(d => d.Date <= day).Rate;

var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("MA_CONN")).Options;
using var db = new ApplicationDbContext(options);

async Task<(List<DateTime> Dates, List<double> Closes)> Load(string symbol)
{
    var inst = await db.Instruments.SingleAsync(i => i.Symbol == symbol);
    var points = await db.PricePoints.AsNoTracking().Where(p => p.InstrumentId == inst.Id).OrderBy(p => p.DateTime).ToListAsync();
    var payouts = await db.FundPayouts.AsNoTracking().Where(p => p.InstrumentId == inst.Id).ToListAsync();
    var prices = PayoutAdjuster.Adjust(PriceDataCleaner.RemoveSpikes(points.Select(p => new PriceDataPoint(p.DateTime, p.Price))), payouts);
    return (prices.Select(p => p.DateTime.Date).ToList(), prices.Select(p => (double)p.Price).ToList());
}

var (dates, closes) = await Load("MIF");
double CashGrowth(DateTime from, DateTime to) => Math.Pow(1 + Math.Max(0, RateOn(from) / 100 - CashFee), (to - from).Days / 365.0);
int IndexOnOrBefore(List<DateTime> ds, DateTime day) { var i = ds.BinarySearch(day); return i >= 0 ? i : ~i - 1; }

// R1: stocks share per day
var share1 = new double?[dates.Count];
var recentVol = new double[dates.Count];
for (int i = 1; i < dates.Count; i++)
{
    var from = IndexOnOrBefore(dates, dates[i].AddDays(-30)) + 1;
    var rets = Enumerable.Range(Math.Max(1, from), i - Math.Max(1, from) + 1).Select(k => Math.Log(closes[k] / closes[k - 1])).ToList();
    recentVol[i] = rets.Count > 5 ? Math.Sqrt(rets.Sum(r => r * r) / rets.Count) : double.NaN;
    if (dates[i] < dates[0].AddDays(395)) continue;
    var yearFrom = IndexOnOrBefore(dates, dates[i].AddDays(-365)) + 1;
    var past = Enumerable.Range(yearFrom, i - yearFrom + 1).Select(k => recentVol[k]).Where(v => !double.IsNaN(v) && v > 0).OrderBy(v => v).ToList();
    if (past.Count < 100 || double.IsNaN(recentVol[i]) || recentVol[i] <= 0) continue;
    var raw = Math.Min(1, past[past.Count / 2] / recentVol[i]);
    share1[i] = Math.Floor(raw * 4) / 4;
}

// R2: stocks (1) or cash (0), decided on the first trading day of each month
var share2 = new double?[dates.Count];
double? current = null;
for (int i = 1; i < dates.Count; i++)
{
    if (dates[i].Month != dates[i - 1].Month && dates[i] >= dates[0].AddDays(370))
    {
        var j = IndexOnOrBefore(dates, dates[i].AddDays(-365));
        var stocks = closes[i] / closes[j];
        var cash = CashGrowth(dates[j], dates[i]);
        current = stocks > cash ? 1 : 0;
    }
    share2[i] = current;
}

void Run(string name, double?[] share, bool holdout)
{
    var days = Enumerable.Range(0, dates.Count).Where(i => share[i] is not null && (dates[i] >= holdoutStart) == holdout).ToList();
    double hold = 1, rule = 1, holdPeak = 1, rulePeak = 1, holdFall = 0, ruleFall = 0, held = share[days[0]]!.Value, moved = 0;
    for (int k = 0; k < days.Count - 1; k++)
    {
        int i = days[k], j = days[k + 1];
        var want = share[i]!.Value;
        if (want != held) { rule *= 1 - Cost * Math.Abs(want - held); moved += Math.Abs(want - held); held = want; }
        var stockMove = closes[j] / closes[i];
        var cashMove = CashGrowth(dates[i], dates[j]);
        hold *= stockMove;
        rule *= held * stockMove + (1 - held) * cashMove;
        holdPeak = Math.Max(holdPeak, hold); rulePeak = Math.Max(rulePeak, rule);
        holdFall = Math.Min(holdFall, hold / holdPeak - 1); ruleFall = Math.Min(ruleFall, rule / rulePeak - 1);
    }
    var years = (dates[days[^1]] - dates[days[0]]).Days / 365.25;
    double Yearly(double g) => (Math.Pow(g, 1 / years) - 1) * 100;
    var avgShare = days.Average(i => share[i]!.Value) * 100;
    var pass = ruleFall >= holdFall * 0.75 && Yearly(rule) >= Yearly(hold) - 2;
    Console.WriteLine($"{name,-24} {dates[days[0]]:yyyy-MM} to {dates[days[^1]]:yyyy-MM}: holding {Yearly(hold),5:+0.0;-0.0}%/yr worst {holdFall * 100,4:0}% | " +
        $"rule {Yearly(rule),5:+0.0;-0.0}%/yr worst {ruleFall * 100,4:0}%, in stocks {avgShare:0}% of the time, {moved / years:0.0} full switches/yr -> {(pass ? "PASS" : "fail")}");
}

foreach (var holdout in showHoldout ? new[] { false, true } : new[] { false })
{
    Console.WriteLine(holdout ? "=== HOLDOUT 2016 on" : "=== DEVELOPMENT to 2015");
    Run("R1 calm-or-jumpy", share1, holdout);
    Run("R2 stronger-than-cash", share2, holdout);
    Console.WriteLine();
}

// Information only: the strongest of stocks, gold and cash over 12 months, monthly, from when gold has a year of data
var (gDates, gCloses) = await Load("GOLDPK");
var start = gDates[0].AddDays(370);
var span = Enumerable.Range(0, dates.Count).Where(i => dates[i] >= start).ToList();
double v3 = 1, holdS = 1, peak3 = 1, fall3 = 0, peakS = 1, fallS = 0;
var pick = "cash";
var picks = new Dictionary<string, int> { ["stocks"] = 0, ["gold"] = 0, ["cash"] = 0 };
for (int k = 0; k < span.Count - 1; k++)
{
    int i = span[k], j = span[k + 1];
    if (k == 0 || dates[i].Month != dates[span[k - 1]].Month)
    {
        var back = dates[i].AddDays(-365);
        var s = closes[i] / closes[IndexOnOrBefore(dates, back)];
        var g = gCloses[IndexOnOrBefore(gDates, dates[i])] / gCloses[IndexOnOrBefore(gDates, back)];
        var c = CashGrowth(back, dates[i]);
        var next = s >= g && s >= c ? "stocks" : g >= c ? "gold" : "cash";
        if (next != pick) v3 *= 1 - Cost;
        pick = next;
        picks[pick]++;
    }
    var gi = IndexOnOrBefore(gDates, dates[i]); var gj = IndexOnOrBefore(gDates, dates[j]);
    v3 *= pick == "stocks" ? closes[j] / closes[i] : pick == "gold" ? gCloses[gj] / gCloses[gi] : CashGrowth(dates[i], dates[j]);
    holdS *= closes[j] / closes[i];
    peak3 = Math.Max(peak3, v3); fall3 = Math.Min(fall3, v3 / peak3 - 1);
    peakS = Math.Max(peakS, holdS); fallS = Math.Min(fallS, holdS / peakS - 1);
}
var yrs = (dates[span[^1]] - dates[span[0]]).Days / 365.25;
Console.WriteLine($"Information only, strongest of stocks/gold/cash {dates[span[0]]:yyyy-MM} to {dates[span[^1]]:yyyy-MM}: " +
    $"{(Math.Pow(v3, 1 / yrs) - 1) * 100:+0.0}%/yr worst {fall3 * 100:0}% against holding MIF {(Math.Pow(holdS, 1 / yrs) - 1) * 100:+0.0}%/yr worst {fallS * 100:0}% " +
    $"(months in stocks {picks["stocks"]}, gold {picks["gold"]}, cash {picks["cash"]})");

// Added AFTER seeing the results above (Nov 9 2026), as a robustness check only; it does not change any verdict:
// is R2 sensitive to its 12-month lookback, and does it behave the same on KSE100 and KMI30 (from 2021)?
Console.WriteLine("\nRobustness check (added after the results): R2 with other lookbacks and instruments, whole period");
foreach (var symbol in new[] { "MIF", "KSE100", "KMI30" })
{
    var (sd, sc) = await Load(symbol);
    foreach (var months in new[] { 6, 9, 12 })
    {
        var first = sd[0].AddMonths(months).AddDays(5);
        var idx = Enumerable.Range(0, sd.Count).Where(i => sd[i] >= first).ToList();
        double r = 1, h = 1, rp = 1, hp = 1, rf = 0, hf = 0, inStocks = 0, switches = 0;
        double? held = null;
        for (int k = 0; k < idx.Count - 1; k++)
        {
            int i = idx[k], j = idx[k + 1];
            if (held is null || sd[i].Month != sd[idx[k - 1]].Month)
            {
                var back = sd[i].AddMonths(-months);
                double want = sc[i] / sc[IndexOnOrBefore(sd, back)] > CashGrowth(back, sd[i]) ? 1 : 0;
                if (held is double was && was != want) { r *= 1 - Cost; switches++; }
                held = want;
            }
            r *= held == 1 ? sc[j] / sc[i] : CashGrowth(sd[i], sd[j]);
            h *= sc[j] / sc[i];
            inStocks += held!.Value;
            rp = Math.Max(rp, r); hp = Math.Max(hp, h); rf = Math.Min(rf, r / rp - 1); hf = Math.Min(hf, h / hp - 1);
        }
        var y = (sd[idx[^1]] - sd[idx[0]]).Days / 365.25;
        Console.WriteLine($"  {symbol,-7} {months,2} months {sd[idx[0]]:yyyy-MM} on: holding {(Math.Pow(h, 1 / y) - 1) * 100,5:+0.0;-0.0}%/yr worst {hf * 100,4:0}% | " +
            $"rule {(Math.Pow(r, 1 / y) - 1) * 100,5:+0.0;-0.0}%/yr worst {rf * 100,4:0}%, in stocks {100 * inStocks / idx.Count:0}%, {switches / y:0.0} switches/yr");
    }
}
