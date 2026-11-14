#:property PublishAot=false
#:property EnableConfigurationBindingGenerator=false
#:project ../../backend/MarketAnalytics.Infrastructure/MarketAnalytics.Infrastructure.csproj
#:project ../../backend/MarketAnalytics.Application/MarketAnalytics.Application.csproj
using System.Globalization;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.ValueObjects;
using MarketAnalytics.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

// Does trading volume (or money flowing into funds) add anything to the price rules tested before?
// Written down before running (Nov 14 2026). No setting is chosen from this data: all are textbook defaults.
//
// Data: KMI-30 daily shares traded (from Nov 2013) and price decide; Meezan Islamic Fund (MIF, payouts included) is what
// is held, as in the earlier tests. Cash earns the State Bank policy rate minus 1% (data/sbp-policy-rate.csv). Moving
// money costs 0.5%. A decision at one day's close acts from the next close (no look-ahead). Volume is "confirming" when
// the last 20 days' average volume is at least the last 100 days' average (the crowd is joining in).
//
// H1 volume-confirmed entries. Three price rules tested before, each with and without the volume filter:
//    golden cross (KMI-30 50-day average >= 200-day), stronger than cash (monthly: MIF's 12-month return beat cash's),
//    technical signal (the app's score: in after a positive score, out after a negative one, unchanged on zero).
//    The volume version moves into stocks only on a day the price rule says "in" AND volume confirms; moves out
//    exactly when the price rule does.
//    Pass (per rule): against its own price-only version, EITHER return per year at least 1 point higher with a worst
//    fall no more than 2 points deeper, OR a worst fall at least 5 points smaller with return per year no more than
//    1 point lower.
// H2 on-balance volume (Granville): add the day's volume when KMI-30 closes up, subtract it when down; hold MIF while
//    that running total is at or above its own 20-day average, otherwise cash.
//    Pass: against holding MIF, a worst fall at least 25% smaller AND return per year no more than 2 points lower
//    (the same mark as the earlier risk rules).
// H3 selling climax: a day when KMI-30 falls 2% or more on at least twice the median volume of the previous 50 days.
//    Events within 10 trading days of the previous one count as the same event. Measure MIF's return over the
//    60 trading days from the next close, against the average 60-day return from every day of the same period.
//    Pass: at least 8 events and an average at least 3 points above the all-days average.
//    Information only: the same for climbs of 2% or more on twice the volume.
// H4 fund flows (contrarian): money put into or taken out of MIF in a month = change in fund size minus change in
//    price. The source's month label may mean either end of that month, so a month's flow is used only from the
//    first trading day two months later. Then: MIF's next 6-month return after the third of months with the biggest
//    outflows, against the third with the biggest inflows.
//    Pass: the outflow third's average is at least 5 points higher. (Overlapping 6-month windows: few independent
//    observations, so even a pass is weak evidence.)
//
// Split: development = to the end of 2019; holdout = 2020 onward, looked at once, only for ideas that pass development.
// H1-H3 start when 100 days of volume exist (about April 2014). H4: development = decision months to the end of 2017,
// holdout = 2018 on (flows start in 2010).
// Information only (no pass or fail): H1-H3 again with KSE-100 price and volume deciding.
// Six ideas are tested (three in H1), so one passing development by luck is quite possible; the holdout decides.
// Usage: dotnet run volume-research.cs           (development only)
//        dotnet run volume-research.cs holdout   (adds the holdout)

var showHoldout = args.Contains("holdout");
const double Cost = 0.005, CashFee = 0.01;
var holdoutStart = new DateTime(2020, 1, 1);
var flowHoldoutStart = new DateTime(2018, 1, 1);

var rates = File.ReadAllLines(Path.Combine("data", "sbp-policy-rate.csv"))
    .Where(l => l.Length > 0 && char.IsDigit(l[0])).Select(l => l.Split(','))
    .Select(f => (Date: DateTime.Parse(f[0], CultureInfo.InvariantCulture), Rate: double.Parse(f[1], CultureInfo.InvariantCulture)))
    .OrderBy(d => d.Date).ToList();
double CashYearly(DateTime day) => Math.Max(0, rates.LastOrDefault(d => d.Date <= day).Rate / 100 - CashFee);

var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("MA_CONN")).Options;
using var db = new ApplicationDbContext(options);

async Task<List<PriceDataPoint>> Prices(string symbol)
{
    var inst = await db.Instruments.SingleAsync(i => i.Symbol == symbol);
    var points = await db.PricePoints.AsNoTracking().Where(p => p.InstrumentId == inst.Id).OrderBy(p => p.DateTime).ToListAsync();
    var payouts = await db.FundPayouts.AsNoTracking().Where(p => p.InstrumentId == inst.Id).ToListAsync();
    return PayoutAdjuster.Adjust(PriceDataCleaner.RemoveSpikes(points.Select(p => new PriceDataPoint(p.DateTime.Date, p.Price))), payouts)
        .Select(p => new PriceDataPoint(p.DateTime.Date, p.Price)).ToList();
}

var mifPrices = await Prices("MIF");
var mifByDate = mifPrices.GroupBy(p => p.DateTime).ToDictionary(g => g.Key, g => (double)g.Last().Price);
var mifDates = mifPrices.Select(p => p.DateTime).Distinct().OrderBy(d => d).ToList();
double MifOnOrBefore(DateTime day) { var i = mifDates.BinarySearch(day); i = i >= 0 ? i : ~i - 1; return i < 0 ? double.NaN : mifByDate[mifDates[i]]; }

Console.WriteLine("VOLUME RESEARCH (pre-registered Nov 14 2026). Development to end 2019" + (showHoldout ? "; HOLDOUT 2020 on shown." : "; holdout hidden."));
await RunDaily("KMI30", decides: true);
Console.WriteLine("\n\n######## INFORMATION ONLY: the same with KSE-100 deciding ########");
await RunDaily("KSE100", decides: false);
await RunFlows();

async Task RunDaily(string symbol, bool decides)
{
    var inst = await db.Instruments.SingleAsync(i => i.Symbol == symbol);
    var prices = await Prices(symbol);
    var volumes = (await db.TradingVolumes.AsNoTracking().Where(v => v.InstrumentId == inst.Id).ToListAsync())
        .ToDictionary(v => v.Date.Date, v => (double)v.Shares);

    // Decision series on the index's own dates, with MIF's close on the same day (or the last before it)
    var technical = SignalHistory.TechnicalScores(new IndicatorCalculationService(), prices).ToDictionary(t => t.Date.Date, t => t.Score);
    var dates = prices.Select(p => p.DateTime).ToList();
    var closes = prices.Select(p => (double)p.Price).ToList();
    int n = dates.Count;
    var vol = dates.Select(d => volumes.TryGetValue(d, out var v) ? v : double.NaN).ToArray();
    var mif = dates.Select(MifOnOrBefore).ToArray();

    double Mean(IEnumerable<double> xs) { var l = xs.ToList(); return l.Count == 0 ? double.NaN : l.Average(); }
    double Sma(double[] xs, int end, int len) => end + 1 < len ? double.NaN : Mean(Enumerable.Range(end - len + 1, len).Select(k => xs[k]));
    var close = closes.ToArray();

    var confirm = new bool?[n];
    for (int i = 0; i < n; i++)
    {
        var v20 = Sma(vol, i, 20); var v100 = Sma(vol, i, 100);
        confirm[i] = double.IsNaN(v20) || double.IsNaN(v100) ? null : v20 >= v100;
    }
    int start = Array.FindIndex(confirm, c => c != null);
    Console.WriteLine($"\n=== {symbol}: volume from {dates[start]:yyyy-MM-dd}, {n - start} days to {dates[^1]:yyyy-MM-dd}");

    // Price rules: 1 = in stocks, 0 = cash, decided at each close
    var gc = new int[n];
    for (int i = 0; i < n; i++) { var a = Sma(close, i, 50); var b = Sma(close, i, 200); gc[i] = !double.IsNaN(b) && a >= b ? 1 : 0; }

    var monthly = new int[n];
    for (int i = 1; i < n; i++)
    {
        monthly[i] = monthly[i - 1];
        if (dates[i].Month == dates[i - 1].Month) continue;
        var yearAgo = dates[i].AddDays(-365);
        var then = MifOnOrBefore(yearAgo);
        if (double.IsNaN(then) || double.IsNaN(mif[i])) continue;
        var cashGrowth = 1.0; // cash over the same 12 months, month by month
        for (var d = yearAgo; d < dates[i]; d = d.AddMonths(1)) cashGrowth *= Math.Pow(1 + CashYearly(d), Math.Min(1, (dates[i] - d).TotalDays / 365.0 * 12) / 12);
        monthly[i] = mif[i] / then > cashGrowth ? 1 : 0;
    }

    var tech = new int[n];
    for (int i = 0; i < n; i++)
    {
        var prev = i > 0 ? tech[i - 1] : 0;
        tech[i] = technical.TryGetValue(dates[i], out var s) ? (s > 0 ? 1 : s < 0 ? 0 : prev) : prev;
    }

    int[] Filter(int[] rule)
    {
        var f = new int[n];
        for (int i = 1; i < n; i++)
            f[i] = rule[i] == 0 ? 0 : f[i - 1] == 1 ? 1 : confirm[i] == true ? 1 : 0;
        return f;
    }

    var obv = new double[n];
    for (int i = 1; i < n; i++)
        obv[i] = obv[i - 1] + (double.IsNaN(vol[i]) ? 0 : Math.Sign(close[i] - close[i - 1]) * vol[i]);
    var obvRule = new int[n];
    for (int i = 0; i < n; i++) { var avg = i >= start + 20 ? Sma(obv, i, 20) : double.NaN; obvRule[i] = !double.IsNaN(avg) && obv[i] >= avg ? 1 : 0; }
    var hold = Enumerable.Repeat(1, n).ToArray();

    void Report(string period, int from, int to)
    {
        Console.WriteLine($"\n--- {period}: {dates[from]:yyyy-MM-dd} to {dates[to]:yyyy-MM-dd}  (return per year / worst fall / moves)");
        var h = Run(hold, from, to);
        Console.WriteLine($"  {"Holding MIF",-34}{Fmt(h)}");
        foreach (var (name, rule) in new[] { ("Golden cross", gc), ("Stronger than cash", monthly), ("Technical signal", tech) })
        {
            var p = Run(rule, from, to); var v = Run(Filter(rule), from, to);
            var pass = (v.Annual - p.Annual >= 1 && v.Worst - p.Worst >= -2) || (v.Worst - p.Worst >= 5 && v.Annual - p.Annual >= -1);
            Console.WriteLine($"  {name,-34}{Fmt(p)}");
            Console.WriteLine($"  {"  + volume confirms",-34}{Fmt(v)}   H1 {(decides ? (pass ? "PASS" : "fail") : (pass ? "(would pass)" : "(would fail)"))}");
        }
        var o = Run(obvRule, from, to);
        var obvPass = o.Worst >= 0.75 * h.Worst && o.Annual >= h.Annual - 2;
        Console.WriteLine($"  {"On-balance volume",-34}{Fmt(o)}   H2 {(decides ? (obvPass ? "PASS" : "fail") : (obvPass ? "(would pass)" : "(would fail)"))}");

        // H3: 60-day MIF return from the next close after a climax day
        double Forward(int i) => i + 61 <= to ? (mif[i + 61] / mif[i + 1] - 1) * 100 : double.NaN;
        var all = Enumerable.Range(from, to - from + 1).Select(Forward).Where(x => !double.IsNaN(x)).ToList();
        foreach (var (label, down) in new[] { ("Selling climax (H3)", true), ("Buying climax (info)", false) })
        {
            var events = new List<int>(); int last = -100;
            for (int i = Math.Max(from, 50); i <= to; i++)
            {
                var past = Enumerable.Range(i - 50, 50).Select(k => vol[k]).Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList();
                if (past.Count < 50 || double.IsNaN(vol[i])) continue;
                var ret = close[i] / close[i - 1] - 1;
                if ((down ? ret <= -0.02 : ret >= 0.02) && vol[i] >= 2 * past[25] && i - last > 10) { events.Add(i); last = i; }
                else if ((down ? ret <= -0.02 : ret >= 0.02) && vol[i] >= 2 * past[25]) last = i;
            }
            var fwd = events.Select(Forward).Where(x => !double.IsNaN(x)).ToList();
            var excess = fwd.Count == 0 ? double.NaN : fwd.Average() - all.Average();
            var verdict = !down ? "" : decides ? (fwd.Count >= 8 && excess >= 3 ? "   H3 PASS" : "   H3 fail") : (fwd.Count >= 8 && excess >= 3 ? "   (would pass)" : "   (would fail)");
            Console.WriteLine($"  {label,-34}{fwd.Count,3} events, next 60 days {(fwd.Count == 0 ? double.NaN : fwd.Average()),6:+0.0;-0.0}% vs all days {all.Average():+0.0;-0.0}% (difference {excess:+0.0;-0.0}){verdict}");
            if (fwd.Count > 0) Console.WriteLine($"  {"",-34}dates: {string.Join(" ", events.Take(30).Select(i => dates[i].ToString("yyyy-MM-dd")))}");
        }
    }

    (double Annual, double Worst, int Moves) Run(int[] rule, int from, int to)
    {
        double eq = 1, peak = 1, worst = 0; int pos = rule[from - 1 >= 0 ? from - 1 : from], moves = 0;
        for (int i = from + 1; i <= to; i++)
        {
            eq *= pos == 1 ? mif[i] / mif[i - 1] : Math.Pow(1 + CashYearly(dates[i - 1]), (dates[i] - dates[i - 1]).TotalDays / 365.25);
            var want = rule[i - 1];
            if (want != pos) { eq *= 1 - Cost; pos = want; moves++; }
            peak = Math.Max(peak, eq); worst = Math.Min(worst, eq / peak - 1);
        }
        var years = (dates[to] - dates[from]).TotalDays / 365.25;
        return ((Math.Pow(eq, 1 / years) - 1) * 100, worst * 100, moves);
    }

    static string Fmt((double Annual, double Worst, int Moves) r) => $"{r.Annual,7:+0.0;-0.0}%  {r.Worst,7:0.0}%  {r.Moves,4}";

    int devEnd = dates.FindLastIndex(d => d < holdoutStart);
    Report("DEVELOPMENT", start, devEnd);
    if (showHoldout) Report("HOLDOUT", devEnd + 1, n - 1);
}

async Task RunFlows()
{
    var inst = await db.Instruments.SingleAsync(i => i.Symbol == "MIF");
    var sizes = await db.FundAssets.AsNoTracking().Where(a => a.InstrumentId == inst.Id).OrderBy(a => a.Month).ToListAsync();
    Console.WriteLine("\n\n=== H4 fund flows (MIF): money in or out, used two months later, against the next 6 months' return");
    var rows = new List<(DateTime Decide, double Flow, double Next6)>();
    for (int k = 1; k < sizes.Count; k++)
    {
        var (m0, m1) = (sizes[k - 1].Month, sizes[k].Month);
        if (m1 != m0.AddMonths(1)) continue;
        var priceChange = MifOnOrBefore(m1) / MifOnOrBefore(m0) - 1;
        var flow = (double)(sizes[k].Rupees / sizes[k - 1].Rupees) - 1 - priceChange;
        var decideIdx = mifDates.FindIndex(d => d >= m1.AddMonths(2));
        if (decideIdx < 0 || double.IsNaN(priceChange)) continue;
        var decide = mifDates[decideIdx];
        var endIdx = mifDates.FindIndex(d => d >= decide.AddMonths(6));
        if (endIdx < 0) continue;
        var next6 = (mifByDate[mifDates[endIdx]] / mifByDate[decide] - 1) * 100;
        rows.Add((decide, flow * 100, next6));
    }

    void Thirds(string period, List<(DateTime Decide, double Flow, double Next6)> set)
    {
        if (set.Count < 9) { Console.WriteLine($"  {period}: too few months ({set.Count})"); return; }
        var sorted = set.OrderBy(r => r.Flow).ToList();
        int third = sorted.Count / 3;
        var outflow = sorted.Take(third).ToList(); var inflow = sorted.Skip(sorted.Count - third).ToList();
        var diff = outflow.Average(r => r.Next6) - inflow.Average(r => r.Next6);
        Console.WriteLine($"  {period}: {set.Count} months ({set[0].Decide:yyyy-MM} to {set[^1].Decide:yyyy-MM})");
        Console.WriteLine($"    biggest outflows: flow {outflow.Average(r => r.Flow),6:+0.0;-0.0}% a month, next 6 months {outflow.Average(r => r.Next6),6:+0.0;-0.0}%");
        Console.WriteLine($"    middle:                                     next 6 months {sorted.Skip(third).Take(sorted.Count - 2 * third).Average(r => r.Next6),6:+0.0;-0.0}%");
        Console.WriteLine($"    biggest inflows:  flow {inflow.Average(r => r.Flow),6:+0.0;-0.0}% a month, next 6 months {inflow.Average(r => r.Next6),6:+0.0;-0.0}%");
        Console.WriteLine($"    difference (outflows minus inflows): {diff:+0.0;-0.0} points   H4 {(diff >= 5 ? "PASS" : "fail")}");
    }

    Thirds("DEVELOPMENT", rows.Where(r => r.Decide < flowHoldoutStart).ToList());
    if (showHoldout) Thirds("HOLDOUT", rows.Where(r => r.Decide >= flowHoldoutStart).ToList());
}
