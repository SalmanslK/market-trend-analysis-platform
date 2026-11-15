#:property PublishAot=false
#:property EnableConfigurationBindingGenerator=false
#:project ../../backend/MarketAnalytics.Infrastructure/MarketAnalytics.Infrastructure.csproj
#:project ../../backend/MarketAnalytics.Application/MarketAnalytics.Application.csproj
using System.Globalization;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.ValueObjects;
using MarketAnalytics.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

// Does the direction of the State Bank's policy rate predict Pakistani stocks? Written down before the rate history was
// fetched or any result seen (Nov 9 2026):
//
// H1  Stocks rise more over the next 120 trading days (about 6 months) when the State Bank's last rate change was a cut
//     than when it was a rise. The direction is known from the trading day AFTER the announcement (no look-ahead).
// H2  A simple rule, "hold stocks after a cut, hold cash after a rise", earns at least as much as holding stocks
//     (within 1% a year) with a smaller worst fall. Cash earns the policy rate minus 1% (a money-market fund's fees);
//     each switch costs 0.5%.
// Stocks: MIF (Meezan Islamic Fund, payout-adjusted, since 2005) is the main series; KSE100 and KMI30 (since 2020) are
//     checked in the later period only.
// Split: development = 2005-2015; holdout = 2016 onward, looked at once, only if development passes.
// Pass in development: H1 difference > 0 with t > 2, counting one independent observation per 120 days;
//     and H2 holds.
// Pass in holdout: H1 difference > 0 with t > 1.5 on MIF; same sign on KSE100 and KMI30; H2 holds on MIF.
// Rate history: scripts/research/data/sbp-policy-rate.csv (date the decision was announced, new rate %), from the
//     State Bank's published policy rate history.
// Usage: dotnet run policy-rate-research.cs           (development only)
//        dotnet run policy-rate-research.cs holdout   (adds the holdout)

var showHoldout = args.Contains("holdout");
const int Horizon = 120;
const double SwitchCost = 0.005, CashFee = 0.01;
var holdoutStart = new DateTime(2016, 1, 1);

var decisions = File.ReadAllLines(Path.Combine("data", "sbp-policy-rate.csv"))
    .Where(l => l.Length > 0 && char.IsDigit(l[0]))
    .Select(l => l.Split(','))
    .Select(f => (Date: DateTime.Parse(f[0], CultureInfo.InvariantCulture), Rate: double.Parse(f[1], CultureInfo.InvariantCulture)))
    .OrderBy(d => d.Date).ToList();

var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("MA_CONN")).Options;
using var db = new ApplicationDbContext(options);

foreach (var symbol in showHoldout ? new[] { "MIF", "KSE100", "KMI30" } : new[] { "MIF" })
{
    var inst = await db.Instruments.SingleAsync(i => i.Symbol == symbol);
    var points = await db.PricePoints.AsNoTracking().Where(p => p.InstrumentId == inst.Id).OrderBy(p => p.DateTime).ToListAsync();
    var payouts = await db.FundPayouts.AsNoTracking().Where(p => p.InstrumentId == inst.Id).ToListAsync();
    var prices = PayoutAdjuster.Adjust(PriceDataCleaner.RemoveSpikes(points.Select(p => new PriceDataPoint(p.DateTime, p.Price))), payouts);
    var dates = prices.Select(p => p.DateTime.Date).ToList();
    var closes = prices.Select(p => (double)p.Price).ToList();

    // As known at each day's close: the rate in force and the direction of the last change (announced before that day)
    var rate = new double[dates.Count];
    var easing = new bool?[dates.Count];
    for (int i = 0; i < dates.Count; i++)
    {
        var known = decisions.Where(d => d.Date < dates[i]).ToList();
        if (known.Count == 0) continue;
        rate[i] = known[^1].Rate;
        var changes = known.Zip(known.Skip(1), (a, b) => b.Rate - a.Rate).Where(c => c != 0).ToList();
        if (changes.Count > 0) easing[i] = changes[^1] < 0;
    }

    foreach (var holdout in showHoldout ? new[] { false, true } : new[] { false })
    {
        var days = Enumerable.Range(0, dates.Count).Where(i => easing[i] is not null && (dates[i] >= holdoutStart) == holdout).ToList();
        if (days.Count < Horizon * 2) { Console.WriteLine($"{symbol} {(holdout ? "holdout" : "development")}: not enough data"); continue; }
        Console.WriteLine($"=== {symbol} {(holdout ? "HOLDOUT 2016 on" : "DEVELOPMENT to 2015")}: {dates[days[0]]:yyyy-MM-dd} to {dates[days[^1]]:yyyy-MM-dd}");

        // H1: next-120-day move (from the next close) after a cut vs after a rise
        double Ret(int a, int b) => closes[b] / closes[a] - 1;
        var usable = days.Where(i => i + 1 + Horizon < closes.Count).ToList();
        var afterCut = usable.Where(i => easing[i] == true).Select(i => Ret(i + 1, i + 1 + Horizon)).ToList();
        var afterRise = usable.Where(i => easing[i] == false).Select(i => Ret(i + 1, i + 1 + Horizon)).ToList();
        if (afterCut.Count < 20 || afterRise.Count < 20) { Console.WriteLine("  H1: one side has too few days to compare"); }
        else
        {
            var diff = afterCut.Average() - afterRise.Average();
            // One independent observation per horizon on each side
            double Var(List<double> x) => x.Sum(v => (v - x.Average()) * (v - x.Average())) / (x.Count - 1);
            var se = Math.Sqrt(Var(afterCut) / Math.Max(1, afterCut.Count / (double)Horizon) + Var(afterRise) / Math.Max(1, afterRise.Count / (double)Horizon));
            var t = diff / se;
            Console.WriteLine($"  H1 next 6 months: after a cut {afterCut.Average() * 100:+0.0;-0.0}% ({afterCut.Count} days), after a rise {afterRise.Average() * 100:+0.0;-0.0}% ({afterRise.Count} days)");
            Console.WriteLine($"     difference {diff * 100:+0.0;-0.0} points, t = {t:+0.0;-0.0} -> {(t > (holdout ? 1.5 : 2) ? "PASS" : "fail")}");
        }

        // H2: stocks after a cut, cash after a rise; decision at today's close takes effect from the next day
        double hold = 1, rule = 1, holdPeak = 1, rulePeak = 1, holdFall = 0, ruleFall = 0;
        int switches = 0;
        bool? inStocks = null;
        for (int k = 0; k < days.Count - 1; k++)
        {
            int i = days[k], j = days[k + 1];
            var want = easing[i] == true;
            if (inStocks is bool was && was != want) { rule *= 1 - SwitchCost; switches++; }
            inStocks = want;
            var stockMove = Ret(i, j);
            var cashMove = Math.Pow(1 + Math.Max(0, rate[i] / 100 - CashFee), (dates[j] - dates[i]).Days / 365.0) - 1;
            hold *= 1 + stockMove;
            rule *= 1 + (want ? stockMove : cashMove);
            holdPeak = Math.Max(holdPeak, hold); rulePeak = Math.Max(rulePeak, rule);
            holdFall = Math.Min(holdFall, hold / holdPeak - 1); ruleFall = Math.Min(ruleFall, rule / rulePeak - 1);
        }
        var years = (dates[days[^1]] - dates[days[0]]).Days / 365.25;
        double Yearly(double growth) => (Math.Pow(growth, 1 / years) - 1) * 100;
        var h2 = Yearly(rule) >= Yearly(hold) - 1 && ruleFall > holdFall;
        Console.WriteLine($"  H2 holding stocks {Yearly(hold):+0.0;-0.0}%/yr, worst fall {holdFall * 100:0}%;  rate rule {Yearly(rule):+0.0;-0.0}%/yr, worst fall {ruleFall * 100:0}%, {switches} switches -> {(h2 ? "PASS" : "fail")}");
        Console.WriteLine();
    }
}
