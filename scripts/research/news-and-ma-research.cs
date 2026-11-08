#:property PublishAot=false
#:property EnableConfigurationBindingGenerator=false
#:project ../../backend/MarketAnalytics.Infrastructure/MarketAnalytics.Infrastructure.csproj
#:project ../../backend/MarketAnalytics.Application/MarketAnalytics.Application.csproj
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.ValueObjects;
using MarketAnalytics.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

// Two questions, tested the same way as the app's backtests (no look-ahead, trade at the next close, 0.5% per trade):
// 1. Does weighting recent news more help the news and combined signals?
// 2. Do moving-average rules, or smoothing the technical score with a moving average, help the technical signal?
// Read-only. Run from this folder, with the database connection string in the MA_CONN environment variable:
//   dotnet run news-and-ma-research.cs

const double Cost = 0.005;
const double MeaningfulPoints = 1;
string[] symbols = { "KSE100", "KMI30", "MIF", "GOLDINT", "GOLDPK", "MGF" };

var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("MA_CONN")).Options;
using var db = new ApplicationDbContext(options);
var articles = (await db.NewsArticles.AsNoTracking().ToListAsync()).Select(a => (a, NewsSentimentAnalyzer.Analyze(a.Title, a.Summary))).ToList();
var indicators = new IndicatorCalculationService();

var data = new List<Data>();
foreach (var symbol in symbols)
    data.Add(await LoadAsync(symbol));

// ---------- 1. News: how recent news is weighted ----------
var newsVariants = new (string Name, int Window, double? HalfLife, bool Linear)[]
{
    ("3 days, equal", 3, null, false),
    ("7 days, equal (app)", 7, null, false),
    ("14 days, equal", 14, null, false),
    ("7 days, linear fade", 7, null, true),
    ("14 days, half-life 1 day", 14, 1, false),
    ("14 days, half-life 2 days", 14, 2, false),
    ("14 days, half-life 3 days", 14, 3, false),
    ("14 days, half-life 5 days", 14, 5, false),
};

Console.WriteLine("=== 1. NEWS WEIGHTING: average of the 6 instruments over their news period, 0.5% per trade");
Console.WriteLine($"{"",-28}{"News /yr",10}{"fall",8}{"trades",8}{"Both agree /yr",16}{"fall",8}{"trades",8}{"corr next 5d",14}");
var holdingNews = data.Average(d => d.HoldNews);
foreach (var v in newsVariants)
{
    var news = new List<(double Annual, double Fall, double Trades)>();
    var both = new List<(double Annual, double Fall, double Trades)>();
    var correlations = new List<double>();
    foreach (var d in data)
    {
        var moods = Moods(d, v.Window, v.HalfLife, v.Linear);
        var newsScores = moods.Select(m => m.Articles >= SignalRules.MinNewsArticles ? SignalRules.PositionForScaledScore(m.Score) : 0).ToList();
        var bothScores = Enumerable.Range(0, d.NewsDates.Count).Select(i =>
        {
            var m = moods[i];
            var combined = SignalRules.CombinedScore(d.NewsTechnical[i], BacktestService.MaxTechnicalScore, (int)Math.Round(m.Score), m.Articles);
            return SignalRules.PositionForScaledScore(combined);
        }).ToList();
        news.Add(Run(d.NewsDates, d.NewsCloses, newsScores));
        both.Add(Run(d.NewsDates, d.NewsCloses, bothScores));
        correlations.Add(ForwardCorrelation(d.NewsCloses, moods, 5));
    }
    Console.WriteLine($"{v.Name,-28}{news.Average(x => x.Annual),10:+0.0;-0.0}{news.Average(x => x.Fall),8:0}{news.Average(x => x.Trades),8:0.0}" +
        $"{both.Average(x => x.Annual),16:+0.0;-0.0}{both.Average(x => x.Fall),8:0}{both.Average(x => x.Trades),8:0.0}{correlations.Average(),14:+0.00;-0.00}");
}
Console.WriteLine($"{"Holding",-28}{holdingNews,10:+0.0;-0.0}{data.Average(d => d.HoldNewsFall),8:0}");

// ---------- 2. Technical: moving averages ----------
Console.WriteLine("\n=== 2. TECHNICAL AND MOVING AVERAGES: average of the 6 instruments over their full history, 0.5% per trade");
var rules = new (string Name, Func<Data, List<int>> Scores)[]
{
    ("Technical, 5 votes (app)", d => d.Technical.Select(Sign).ToList()),
    ("Technical smoothed 3 days", d => Smoothed(d.Technical, 3)),
    ("Technical smoothed 5 days", d => Smoothed(d.Technical, 5)),
    ("Technical smoothed 10 days", d => Smoothed(d.Technical, 10)),
    ("Technical smoothed 20 days", d => Smoothed(d.Technical, 20)),
    ("Technical only above 200-day", d => d.Technical.Select((t, i) => d.Closes[i] >= d.Sma200[i] ? Sign(t) : -1).ToList()),
    ("Price vs 50-day average", d => d.Closes.Select((c, i) => c >= d.Sma50[i] ? 1 : -1).ToList()),
    ("Price vs 200-day average", d => d.Closes.Select((c, i) => c >= d.Sma200[i] ? 1 : -1).ToList()),
    ("EMA 12/26 cross", d => d.Ema12.Select((e, i) => e >= d.Ema26[i] ? 1 : -1).ToList()),
    ("EMA 20/50 cross", d => d.Ema20.Select((e, i) => e >= d.Ema50[i] ? 1 : -1).ToList()),
    ("SMA 50/200 golden cross (app trend)", d => d.Sma50.Select((m, i) => m >= d.Sma200[i] ? 1 : -1).ToList()),
    ("Triple: price, 50-day, 200-day", d => d.Closes.Select((c, i) =>
        c > d.Sma50[i] && d.Sma50[i] > d.Sma200[i] ? 1 : c < d.Sma50[i] && d.Sma50[i] < d.Sma200[i] ? -1 : 0).ToList()),
};

Console.WriteLine($"{"",-38}{"/yr",8}{"fall",8}{"trades",8}{"beat hold",11}{"halves vs hold",17}");
foreach (var (name, scores) in rules)
{
    var runs = data.Select(d => (d, r: Run(d.Dates, d.Closes, scores(d)), halves: Halves(d, scores(d)))).ToList();
    var beat = runs.Count(x => x.r.Annual > x.d.Hold + MeaningfulPoints);
    var first = runs.Average(x => x.halves.First);
    var second = runs.Average(x => x.halves.Second);
    Console.WriteLine($"{name,-38}{runs.Average(x => x.r.Annual),8:+0.0;-0.0}{runs.Average(x => x.r.Fall),8:0}{runs.Average(x => x.r.Trades),8:0.0}{$"{beat}/6",11}{$"{first:+0;-0} / {second:+0;-0}",17}");
}
Console.WriteLine($"{"Holding",-38}{data.Average(d => d.Hold),8:+0.0;-0.0}{data.Average(d => d.HoldFall),8:0}");

Console.WriteLine("\nPer instrument, return per year:");
Console.WriteLine($"{"",-38}" + string.Concat(data.Select(d => $"{d.Symbol,9}")));
Console.WriteLine($"{"Holding",-38}" + string.Concat(data.Select(d => $"{d.Hold,9:+0.0;-0.0}")));
foreach (var (name, scores) in rules)
    Console.WriteLine($"{name,-38}" + string.Concat(data.Select(d => $"{Run(d.Dates, d.Closes, scores(d)).Annual,9:+0.0;-0.0}")));

// ---------- 3. Combined signal with a smoothed technical score ----------
Console.WriteLine("\n=== 3. BOTH AGREE WITH A SMOOTHED TECHNICAL SCORE: news period, 7-day news (app), 0.5% per trade");
Console.WriteLine($"{"",-28}{"/yr",8}{"fall",8}{"trades",8}{"beat hold",11}");
foreach (var days in new[] { 1, 3, 5, 10, 20 })
{
    var runs = data.Select(d =>
    {
        var moods = Moods(d, SignalRules.NewsWindowDays, null, false);
        var technical = d.NewsTechnical.Select(t => t * 100.0 / BacktestService.MaxTechnicalScore).ToList();
        var scores = Enumerable.Range(0, d.NewsDates.Count).Select(i =>
        {
            var from = Math.Max(0, i - days + 1);
            var smooth = technical.Skip(from).Take(i - from + 1).Average();
            var m = moods[i];
            var clear = Math.Abs(smooth) >= 20 && Math.Abs(m.Score) >= 20 && m.Articles >= SignalRules.MinNewsArticles;
            return clear && Math.Sign(smooth) == Math.Sign(m.Score) ? Math.Sign(smooth) : 0;
        }).ToList();
        return (d, r: Run(d.NewsDates, d.NewsCloses, scores));
    }).ToList();
    Console.WriteLine($"{(days == 1 ? "Not smoothed (app)" : $"Technical smoothed {days} days"),-28}{runs.Average(x => x.r.Annual),8:+0.0;-0.0}{runs.Average(x => x.r.Fall),8:0}{runs.Average(x => x.r.Trades),8:0.0}{$"{runs.Count(x => x.r.Annual > x.d.HoldNews + MeaningfulPoints)}/6",11}");
}
Console.WriteLine($"{"Holding",-28}{holdingNews,8:+0.0;-0.0}{data.Average(d => d.HoldNewsFall),8:0}");

// ---------- helpers ----------
static int Sign(double x) => x > 0 ? 1 : x < 0 ? -1 : 0;

static List<int> Smoothed(List<double> technical, int days) =>
    technical.Select((_, i) =>
    {
        var from = Math.Max(0, i - days + 1);
        return SignalRules.PositionForScaledScore(technical.Skip(from).Take(i - from + 1).Average());
    }).ToList();

(double Annual, double Fall, double Trades) Run(List<DateTime> dates, List<double> closes, List<int> scores)
{
    var run = BacktestService.Simulate(dates, closes, scores, Cost, 0);
    var years = (dates[^1] - dates[0]).TotalDays / 365.25;
    return (Annual(run.Equity[^1], years), MaxFall(run.Equity), run.Trades / years);
}

// The rule's gap to holding in points per year, in the first and second half of the history
(double First, double Second) Halves(Data d, List<int> scores)
{
    var mid = d.Dates.Count / 2;
    double Gap(int from, int to)
    {
        var dates = d.Dates.GetRange(from, to - from);
        var closes = d.Closes.GetRange(from, to - from);
        var years = (dates[^1] - dates[0]).TotalDays / 365.25;
        var rule = BacktestService.Simulate(dates, closes, scores.GetRange(from, to - from), Cost, 0);
        return Annual(rule.Equity[^1], years) - Annual(closes[^1] / closes[1] * (1 - Cost), years);
    }
    return (Gap(0, mid), Gap(mid, d.Dates.Count));
}

static double Annual(double growth, double years) => (Math.Pow(growth, 1 / years) - 1) * 100;

static double MaxFall(double[] equity)
{
    double peak = equity[0], worst = 0;
    foreach (var e in equity) { peak = Math.Max(peak, e); worst = Math.Min(worst, e / peak - 1); }
    return worst * 100;
}

// Each day's mood from news on or before that day: confidence-weighted, optionally fading with age
static List<(double Score, int Articles)> Moods(Data d, int window, double? halfLife, bool linear)
{
    var result = new List<(double, int)>(d.NewsDates.Count);
    foreach (var date in d.NewsDates)
    {
        double sum = 0, weights = 0;
        var count = 0;
        foreach (var n in d.News)
        {
            var age = (date.Date - n.Date).TotalDays;
            if (age < 0 || age >= window) continue;
            var fade = halfLife is double h ? Math.Pow(0.5, age / h) : linear ? 1 - age / window : 1;
            sum += n.Score * n.Weight * fade;
            weights += n.Weight * fade;
            count++;
        }
        result.Add((weights > 0 ? sum / weights : 0, count));
    }
    return result;
}

// Correlation between the mood and the change over the next 5 trading days, on days with enough news
static double ForwardCorrelation(List<double> closes, List<(double Score, int Articles)> moods, int ahead)
{
    var pairs = Enumerable.Range(0, closes.Count - ahead)
        .Where(i => moods[i].Articles >= SignalRules.MinNewsArticles)
        .Select(i => (x: moods[i].Score, y: closes[i + ahead] / closes[i] - 1)).ToList();
    var mx = pairs.Average(p => p.x);
    var my = pairs.Average(p => p.y);
    var cov = pairs.Sum(p => (p.x - mx) * (p.y - my));
    return cov / Math.Sqrt(pairs.Sum(p => (p.x - mx) * (p.x - mx)) * pairs.Sum(p => (p.y - my) * (p.y - my)));
}

async Task<Data> LoadAsync(string symbol)
{
    var instrument = await db.Instruments.SingleAsync(i => i.Symbol == symbol);
    var points = await db.PricePoints.AsNoTracking().Where(p => p.InstrumentId == instrument.Id).OrderBy(p => p.DateTime).ToListAsync();
    // Same as the app: spikes removed, fund NAVs adjusted for payouts
    var payouts = await db.FundPayouts.AsNoTracking().Where(p => p.InstrumentId == instrument.Id).ToListAsync();
    var prices = PayoutAdjuster.Adjust(PriceDataCleaner.RemoveSpikes(points.Select(p => new PriceDataPoint(p.DateTime, p.Price))), payouts);

    var days = SignalHistory.TechnicalScores(indicators, prices);
    var dates = days.Select(x => x.Date).ToList();
    var closes = days.Select(x => (double)x.Price).ToList();
    List<double> Series(Dictionary<DateTime, decimal?> values) => dates.Select(x => (double)(values[x] ?? 0)).ToList();

    var news = NewsService.ScoreForSymbol(articles, symbol);
    var appMoods = NewsService.DailyMood(dates, news, SignalRules.NewsWindowDays);
    var start = appMoods.FindIndex(m => m.Articles >= SignalRules.MinNewsArticles);
    var newsDates = dates.Skip(start).ToList();
    var newsCloses = closes.Skip(start).ToList();
    var years = (dates[^1] - dates[0]).TotalDays / 365.25;
    var newsYears = (newsDates[^1] - newsDates[0]).TotalDays / 365.25;

    return new Data(
        symbol, dates, closes,
        days.Select(x => x.Score * 100.0 / BacktestService.MaxTechnicalScore).ToList(),
        Series(indicators.ComputeSma(prices, 50)), Series(indicators.ComputeSma(prices, 200)),
        Series(indicators.ComputeEma(prices, 12)), Series(indicators.ComputeEma(prices, 26)),
        Series(indicators.ComputeEma(prices, 20)), Series(indicators.ComputeEma(prices, 50)),
        Annual(closes[^1] / closes[1] * (1 - Cost), years), MaxFall(closes.Select(c => c / closes[0]).ToArray()),
        newsDates, newsCloses, days.Skip(start).Select(x => x.Score).ToList(), news,
        Annual(newsCloses[^1] / newsCloses[1] * (1 - Cost), newsYears), MaxFall(newsCloses.Select(c => c / newsCloses[0]).ToArray()));
}

record Data(
    string Symbol, List<DateTime> Dates, List<double> Closes, List<double> Technical,
    List<double> Sma50, List<double> Sma200, List<double> Ema12, List<double> Ema26, List<double> Ema20, List<double> Ema50,
    double Hold, double HoldFall,
    List<DateTime> NewsDates, List<double> NewsCloses, List<int> NewsTechnical, List<ScoredNews> News,
    double HoldNews, double HoldNewsFall);
