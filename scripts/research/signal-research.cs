#:property PublishAot=false
#:property EnableConfigurationBindingGenerator=false
#:project ../../backend/MarketAnalytics.Infrastructure/MarketAnalytics.Infrastructure.csproj
#:project ../../backend/MarketAnalytics.Application/MarketAnalytics.Application.csproj
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.ValueObjects;
using MarketAnalytics.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

// Compares every trading rule against simply holding, in ways designed to expose lucky results.
// Read-only. Run from this folder, with the database connection string in the MA_CONN environment variable:
//   dotnet run signal-research.cs

const int BootstrapRuns = 5000;
const int BootstrapBlockDays = 20;
const int RebalanceDays = 63;
const int MinHistoryDays = 252;
const double MeaningfulPoints = 1;
const double CashInterest = 0.10;

string[] reliable = { "KSE100", "KMI30", "MIF", "GOLDINT", "GOLDPK", "MGF" };
string[] tooShort = { "KMIF", "MBF", "USDPKR" };
string[] ruleNames = { "Technical", "News", "Combined (old)", "Balanced", "Both agree", "Golden cross", "Monthly trend" };

var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("MA_CONN")).Options;
using var db = new ApplicationDbContext(options);
var articles = (await db.NewsArticles.AsNoTracking().ToListAsync()).Select(a => (a, NewsSentimentAnalyzer.Analyze(a.Title, a.Summary))).ToList();
var indicators = new IndicatorCalculationService();
var backtest = new BacktestService(null!, null!, indicators, NullLogger<BacktestService>.Instance);

var data = new Dictionary<string, InstrumentData>();
foreach (var symbol in reliable.Concat(tooShort))
    data[symbol] = await LoadAsync(symbol);

// ----- 1. Full period, at each cost, with and without interest on cash
foreach (var (cost, cash) in new[] { (0.005, 0.0), (0.0, 0.0), (0.01, 0.0), (0.005, CashInterest) })
{
    Console.WriteLine($"\n=== RETURN PER YEAR. Cost {cost * 100:0.#}% per trade, cash earns {cash * 100:0}% a year. Worst fall in brackets.");
    Console.WriteLine($"{"",-9}{"Holding",13}" + string.Concat(ruleNames.Select(n => $"{n,16}")));
    var sums = new double[ruleNames.Length + 1];
    var wins = new int[ruleNames.Length];
    var ties = new int[ruleNames.Length];
    foreach (var symbol in reliable)
    {
        var d = data[symbol];
        var hold = HoldEquity(d, cost);
        var line = $"{symbol,-9}{$"{Annual(hold, d):+0.0;-0.0} ({Drawdown(hold):0})",13}";
        sums[0] += Annual(hold, d);
        for (int r = 0; r < ruleNames.Length; r++)
        {
            var run = BacktestService.Simulate(d.Dates, d.Closes, d.Rules[r], cost, cash);
            var annual = Annual(run.Equity, d);
            sums[r + 1] += annual;
            var gap = annual - Annual(hold, d);
            if (gap >= MeaningfulPoints) wins[r]++; else if (gap > -MeaningfulPoints) ties[r]++;
            line += $"{$"{annual:+0.0;-0.0} ({Drawdown(run.Equity):0})",16}";
        }
        Console.WriteLine(line);
    }
    Console.WriteLine($"{"Average",-9}{sums[0] / reliable.Length,13:+0.0;-0.0}" + string.Concat(sums.Skip(1).Select(s => $"{s / reliable.Length,16:+0.0;-0.0}")));
    Console.WriteLine($"{"Beat/tie",-9}{"",13}" + string.Concat(wins.Select((w, i) => $"{$"{w}/{ties[i]}",16}")));
}

// ----- 2. Risk: trades, time invested, return per unit of risk
Console.WriteLine("\n=== RISK, averaged over the 6 instruments. Cost 0.5%, cash earns 0%.");
Console.WriteLine($"{"",-16}{"Return",9}{"Worst fall",12}{"Sharpe",9}{"Trades/yr",11}{"Invested",10}");
{
    void Row(string name, Func<InstrumentData, (double[] Equity, double TradesPerYear, double Invested)> run)
    {
        var results = reliable.Select(s => (Data: data[s], Run: run(data[s]))).ToList();
        Console.WriteLine($"{name,-16}{results.Average(x => Annual(x.Run.Equity, x.Data)),9:+0.0;-0.0}{results.Average(x => Drawdown(x.Run.Equity)),12:0.0}{results.Average(x => Sharpe(x.Run.Equity, x.Data)),9:0.00}{results.Average(x => x.Run.TradesPerYear),11:0.0}{results.Average(x => x.Run.Invested),9:0}%");
    }
    Row("Holding", d => (HoldEquity(d, 0.005), 0, 100));
    for (int r = 0; r < ruleNames.Length; r++)
    {
        var rule = r;
        Row(ruleNames[r], d =>
        {
            var run = BacktestService.Simulate(d.Dates, d.Closes, d.Rules[rule], 0.005, 0);
            return (run.Equity, run.Trades / d.Years, (double)run.TimeInvested);
        });
    }
}

// ----- 3. Consistency: does a rule that beats holding early on also beat it later?
Console.WriteLine("\n=== CONSISTENCY: gap to holding in points per year, first half / second half. Cost 0.5%.");
Console.WriteLine($"{"",-9}" + string.Concat(ruleNames.Select(n => $"{n,16}")));
var bothHalves = new int[ruleNames.Length];
foreach (var symbol in reliable)
{
    var d = data[symbol];
    var hold = HoldEquity(d, 0.005);
    var mid = d.Dates.Count / 2;
    var line = $"{symbol,-9}";
    for (int r = 0; r < ruleNames.Length; r++)
    {
        var equity = BacktestService.Simulate(d.Dates, d.Closes, d.Rules[r], 0.005, 0).Equity;
        var first = Span(equity, d, 0, mid) - Span(hold, d, 0, mid);
        var second = Span(equity, d, mid, d.Dates.Count - 1) - Span(hold, d, mid, d.Dates.Count - 1);
        if (first > 0 && second > 0) bothHalves[r]++;
        line += $"{$"{first:+0;-0;0} / {second:+0;-0;0}",16}";
    }
    Console.WriteLine(line);
}
Console.WriteLine($"{"Both > 0",-9}" + string.Concat(bothHalves.Select(b => $"{b,16}")));

// ----- 4. No hindsight: each quarter, follow whichever rule (or holding) has done best so far
Console.WriteLine("\n=== NO HINDSIGHT: each quarter pick the rule with the best return so far. Cost 0.5%.");
Console.WriteLine($"{"",-9}{"Holding",10}{"Picker",10}{"Trades/yr",11}  Most often picked");
double holdAverage = 0, pickerAverage = 0;
foreach (var symbol in reliable)
{
    var d = data[symbol];
    var states = d.Rules.Select(InvestedStates).Append(Enumerable.Repeat(true, d.Dates.Count).ToList()).ToList();
    var equities = d.Rules.Select(r => BacktestService.Simulate(d.Dates, d.Closes, r, 0.005, 0).Equity).Append(HoldEquity(d, 0.005)).ToList();
    var chosen = new List<int>(d.Dates.Count);
    var current = states.Count - 1; // hold until there is enough history to judge
    var counts = new int[states.Count];
    for (int i = 0; i < d.Dates.Count; i++)
    {
        if (i >= MinHistoryDays && (i - MinHistoryDays) % RebalanceDays == 0)
        {
            var best = Enumerable.Range(0, equities.Count).OrderByDescending(e => equities[e][i]).First();
            current = best;
        }
        chosen.Add(current);
        counts[current]++;
    }
    var scores = Enumerable.Range(0, d.Dates.Count).Select(i => states[chosen[i]][i] ? 1 : -1).ToList();
    var picker = BacktestService.Simulate(d.Dates, d.Closes, scores, 0.005, 0);

    // Compare from when the picker can first choose, so its forced holding period doesn't flatter it
    var from = MinHistoryDays;
    var hold = HoldEquity(d, 0.005);
    var holdReturn = Span(hold, d, from, d.Dates.Count - 1);
    var pickerReturn = Span(picker.Equity, d, from, d.Dates.Count - 1);
    holdAverage += holdReturn; pickerAverage += pickerReturn;
    var names = ruleNames.Append("Holding").ToArray();
    var favourite = Enumerable.Range(0, names.Length).OrderByDescending(n => counts[n]).First();
    Console.WriteLine($"{symbol,-9}{holdReturn,10:+0.0;-0.0}{pickerReturn,10:+0.0;-0.0}{picker.Trades / d.Years,11:0.0}  {names[favourite]} ({100.0 * counts[favourite] / d.Dates.Count:0}%)");
}
Console.WriteLine($"{"Average",-9}{holdAverage / reliable.Length,10:+0.0;-0.0}{pickerAverage / reliable.Length,10:+0.0;-0.0}");

// ----- 5. Luck: reshuffle blocks of days; how often does the rule still beat holding?
Console.WriteLine($"\n=== LUCK CHECK: % of {BootstrapRuns} reshuffles where the rule beat holding. Cost 0.5%. Over 95% or under 5% would be unlikely to be luck.");
Console.WriteLine($"{"",-9}" + string.Concat(ruleNames.Select(n => $"{n,16}")));
foreach (var symbol in reliable)
{
    var d = data[symbol];
    var hold = DailyLogReturns(HoldEquity(d, 0.005));
    var line = $"{symbol,-9}";
    for (int r = 0; r < ruleNames.Length; r++)
    {
        var rule = DailyLogReturns(BacktestService.Simulate(d.Dates, d.Closes, d.Rules[r], 0.005, 0).Equity);
        var excess = rule.Zip(hold, (a, b) => a - b).ToArray();
        line += $"{BeatShare(excess, new Random(1234 + r)),15:0}%";
    }
    Console.WriteLine(line);
}

// ----- 6. Instruments with too little history, for information only
Console.WriteLine("\n=== TOO SHORT TO JUDGE, for information. Cost 0.5%, cash earns 0%.");
Console.WriteLine($"{"",-9}{"Years",7}{"Holding",9}" + string.Concat(ruleNames.Select(n => $"{n,16}")));
foreach (var symbol in tooShort)
{
    var d = data[symbol];
    var line = $"{symbol,-9}{d.Years,7:0.0}{Annual(HoldEquity(d, 0.005), d),9:+0.0;-0.0}";
    foreach (var rule in d.Rules)
        line += $"{Annual(BacktestService.Simulate(d.Dates, d.Closes, rule, 0.005, 0).Equity, d),16:+0.0;-0.0}";
    Console.WriteLine(line);
}

async Task<InstrumentData> LoadAsync(string symbol)
{
    var instrument = await db.Instruments.SingleAsync(i => i.Symbol == symbol);
    var points = await db.PricePoints.AsNoTracking().Where(p => p.InstrumentId == instrument.Id).OrderBy(p => p.DateTime).ToListAsync();
    var prices = PriceDataCleaner.RemoveSpikes(points.Select(p => new PriceDataPoint(p.DateTime, p.Price)));
    var days = backtest.ComputeDailyScores(prices);
    var news = NewsService.ScoreForSymbol(articles, symbol);
    var allDates = days.Select(d => d.Date).ToList();
    var moods = NewsService.DailyMood(allDates, news, SignalRules.NewsWindowDays);
    var start = moods.FindIndex(m => m.Articles >= SignalRules.MinNewsArticles);

    var dates = allDates.Skip(start).ToList();
    var prices2 = days.Skip(start).Select(d => d.Price).ToList();
    var closes = prices2.Select(p => (double)p).ToList();
    var technical = days.Skip(start).Select(d => d.Score * 20.0).ToList();
    var windowMoods = moods.Skip(start).ToList();
    var has = windowMoods.Select(m => m.Articles >= SignalRules.MinNewsArticles).ToList();
    var mood = windowMoods.Select(m => (double)m.Score).ToList();

    var sma50 = indicators.ComputeSma(prices, SignalRules.MediumTrendPeriod);
    var sma200 = indicators.ComputeSma(prices, SignalRules.LongTrendPeriod);
    var medium = dates.Select(d => sma50[d]!.Value).ToList();
    var longTerm = dates.Select(d => sma200[d]!.Value).ToList();

    int Position(double score) => SignalRules.PositionForScaledScore(score);
    var range = Enumerable.Range(0, dates.Count).ToList();

    // Balanced: news rescaled to swing as much as technical, using only the spread known up to that day
    var balanced = new List<int>(dates.Count);
    var technicalSeen = new List<double>();
    var moodSeen = new List<double>();
    foreach (var i in range)
    {
        if (has[i]) { technicalSeen.Add(technical[i]); moodSeen.Add(mood[i]); }
        var scale = moodSeen.Count >= 60 ? Spread(technicalSeen) / Math.Max(Spread(moodSeen), 1e-9) : 0.5;
        balanced.Add(Position(has[i] ? (technical[i] + mood[i] * scale) / 2 : technical[i]));
    }

    var rules = new List<List<int>>
    {
        technical.Select(Position).ToList(),
        range.Select(i => Position(has[i] ? mood[i] : 0)).ToList(),
        range.Select(i => Position(has[i] ? (technical[i] + mood[i]) / 2 : technical[i])).ToList(),
        balanced,
        range.Select(i => has[i] && Math.Abs(technical[i]) >= 20 && Math.Abs(mood[i]) >= 20 && Math.Sign(technical[i]) == Math.Sign(mood[i]) ? Math.Sign(technical[i]) : 0).ToList(),
        BacktestService.GoldenCrossScores(medium, longTerm),
        BacktestService.MonthlyTrendScores(dates, prices2, longTerm),
    };

    return new InstrumentData(dates, closes, rules, (dates[^1] - dates[0]).TotalDays / 365.25);
}

static double[] HoldEquity(InstrumentData d, double cost)
{
    var equity = new double[d.Closes.Count];
    equity[0] = 1;
    equity[1] = 1 - cost;
    for (int i = 2; i < d.Closes.Count; i++)
        equity[i] = equity[i - 1] * d.Closes[i] / d.Closes[i - 1];
    return equity;
}

static double Annual(double[] equity, InstrumentData d) => (Math.Pow(equity[^1], 1 / d.Years) - 1) * 100;

static double Span(double[] equity, InstrumentData d, int from, int to) =>
    (Math.Pow(equity[to] / equity[from], 365.25 / (d.Dates[to] - d.Dates[from]).TotalDays) - 1) * 100;

static double Drawdown(double[] equity)
{
    double peak = 0, worst = 0;
    foreach (var value in equity)
    {
        peak = Math.Max(peak, value);
        worst = Math.Min(worst, value / peak - 1);
    }
    return worst * 100;
}

static double Sharpe(double[] equity, InstrumentData d)
{
    var returns = DailyLogReturns(equity);
    var perYear = returns.Length / d.Years;
    return returns.Average() / Math.Max(Spread(returns), 1e-12) * Math.Sqrt(perYear);
}

static double[] DailyLogReturns(double[] equity) =>
    equity.Zip(equity.Skip(1), (a, b) => Math.Log(b / a)).ToArray();

static double Spread(IReadOnlyCollection<double> values)
{
    var mean = values.Average();
    return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / Math.Max(values.Count - 1, 1));
}

static List<bool> InvestedStates(List<int> scores)
{
    // A signal acts at the next close, matching Simulate
    var states = new List<bool>(scores.Count);
    var invested = false;
    foreach (var score in scores)
    {
        invested = score > 0 || (score == 0 && invested);
        states.Add(invested);
    }
    return states;
}

static double BeatShare(double[] excess, Random random)
{
    int beat = 0;
    for (int run = 0; run < BootstrapRuns; run++)
    {
        double sum = 0;
        for (int filled = 0; filled < excess.Length;)
        {
            var start = random.Next(excess.Length);
            for (int k = 0; k < BootstrapBlockDays && filled < excess.Length; k++, filled++)
                sum += excess[(start + k) % excess.Length];
        }
        if (sum > 0) beat++;
    }
    return 100.0 * beat / BootstrapRuns;
}

record InstrumentData(List<DateTime> Dates, List<double> Closes, List<List<int>> Rules, double Years);
