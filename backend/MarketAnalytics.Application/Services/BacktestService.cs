using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Tests the technical signal against stored history, using the same rules as the live signal (<see cref="SignalRules"/>).
///
/// Guards against flattering results:
/// - No look-ahead: every indicator only uses prices up to its own day, and a signal from one day's close
///   is acted on at the next day's close.
/// - Costs: every buy or sell pays <c>costPercent</c>.
/// - No tuning: the rules use standard textbook settings and are not fitted to this data.
///
/// Limits: prices exclude dividends (understating holding funds and stocks), and the simple strategy only
/// goes between fully invested and cash. Past results don't guarantee future ones.
/// </summary>
public class BacktestService : IBacktestService
{
    public const decimal DefaultCostPercent = 0.5m;
    public const decimal AssumedCashInterestPercent = 10m;

    // A year of trading days beyond the 200-period warm-up, or there is too little to judge
    private const int MinTradingDays = 250;
    private const int MaxCurvePoints = 300;
    // A drawdown must shrink by at least this share to count as meaningfully lower risk
    private const double MeaningfulDrawdownReduction = 1.0 / 3;
    private static readonly int[] Horizons = { 7, 30, 90 };
    private static readonly string[] Actions = { "StrongBuy", "Buy", "Hold", "Sell", "StrongSell" };

    private readonly IInstrumentRepository _instrumentRepository;
    private readonly IPricePointRepository _pricePointRepository;
    private readonly IIndicatorCalculationService _indicators;
    private readonly ILogger<BacktestService> _logger;

    public BacktestService(
        IInstrumentRepository instrumentRepository,
        IPricePointRepository pricePointRepository,
        IIndicatorCalculationService indicators,
        ILogger<BacktestService> logger)
    {
        _instrumentRepository = instrumentRepository;
        _pricePointRepository = pricePointRepository;
        _indicators = indicators;
        _logger = logger;
    }

    public async Task<IEnumerable<BacktestResultDto>> GetAllAsync(decimal costPercent, CancellationToken cancellationToken = default)
    {
        var results = new List<BacktestResultDto>();
        foreach (var instrument in await _instrumentRepository.GetActiveInstrumentsAsync(cancellationToken))
        {
            try
            {
                results.Add(await BuildAsync(instrument, costPercent, cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Skip instruments that fail so one bad instrument doesn't break the whole page
                _logger.LogError(ex, "Error backtesting instrument {Symbol}", instrument.Symbol);
            }
        }
        return results;
    }

    public async Task<BacktestResultDto?> GetAsync(string symbol, decimal costPercent, CancellationToken cancellationToken = default)
    {
        var instrument = await _instrumentRepository.GetBySymbolAsync(symbol, cancellationToken);
        return instrument == null ? null : await BuildAsync(instrument, costPercent, cancellationToken);
    }

    private async Task<BacktestResultDto> BuildAsync(Instrument instrument, decimal costPercent, CancellationToken cancellationToken)
    {
        var points = await _pricePointRepository.GetByInstrumentIdAsync(instrument.Id, cancellationToken: cancellationToken);
        var prices = PriceDataCleaner.RemoveSpikes(points.Select(p => new PriceDataPoint(p.DateTime, p.Price)));

        var result = Run(prices, costPercent);
        result.Symbol = instrument.Symbol;
        result.Name = instrument.Name;
        result.Type = instrument.Type;
        return result;
    }

    /// <summary>
    /// The signal score for each day from the first day all five rules can be evaluated (exposed for testing).
    /// Uses the same rules as the live signal; each day's score only depends on prices up to that day.
    /// </summary>
    public List<(DateTime Date, decimal Price, int Score)> ComputeDailyScores(IReadOnlyList<PriceDataPoint> prices)
    {
        var sorted = prices.OrderBy(p => p.DateTime).ToList();
        var sma200 = _indicators.ComputeSma(sorted, SignalRules.LongTrendPeriod);
        var sma50 = _indicators.ComputeSma(sorted, SignalRules.MediumTrendPeriod);
        var rsi = _indicators.ComputeRsi(sorted, SignalRules.RsiPeriod);
        var (_, _, histogram) = _indicators.ComputeMacd(sorted);
        var (upper, lower) = _indicators.ComputeBollingerBands(sorted);

        var scores = new List<(DateTime, decimal, int)>();
        foreach (var point in sorted)
        {
            var date = point.DateTime;
            if (sma200[date] is not decimal longAverage || sma50[date] is not decimal mediumAverage
                || rsi[date] is not decimal rsiValue || histogram[date] is not decimal macdHistogram
                || upper[date] is not decimal upperBand || lower[date] is not decimal lowerBand)
                continue;

            var percentB = SignalRules.PercentB(point.Price, upperBand, lowerBand);
            var score = SignalRules.Trend(point.Price, longAverage)
                + SignalRules.Trend(point.Price, mediumAverage)
                + SignalRules.Momentum(macdHistogram)
                + SignalRules.Rsi(rsiValue)
                + (percentB is decimal b ? SignalRules.Bollinger(b) : 0);

            scores.Add((date, point.Price, score));
        }
        return scores;
    }

    /// <summary>
    /// Runs the full backtest on daily prices (exposed for testing).
    /// </summary>
    public BacktestResultDto Run(IReadOnlyList<PriceDataPoint> prices, decimal costPercent = DefaultCostPercent)
    {
        var result = new BacktestResultDto { CostPercent = costPercent, CashInterestPercent = AssumedCashInterestPercent };

        var days = ComputeDailyScores(prices);
        if (days.Count < MinTradingDays)
            return result;

        var dates = days.Select(d => d.Date).ToList();
        var closes = days.Select(d => (double)d.Price).ToList();
        var scores = days.Select(d => d.Score).ToList();
        var cost = (double)costPercent / 100;

        var strategy = Simulate(dates, closes, scores, cost, cashInterest: 0);
        var withInterest = Simulate(dates, closes, scores, cost, (double)AssumedCashInterestPercent / 100);

        // Holding from the same first possible trade (the second day's close), paying one purchase cost
        var buyAndHold = new double[closes.Count];
        buyAndHold[0] = 1;
        buyAndHold[1] = 1 - cost;
        for (int i = 2; i < closes.Count; i++)
            buyAndHold[i] = buyAndHold[i - 1] * closes[i] / closes[i - 1];

        var years = (dates[^1] - dates[0]).TotalDays / 365.25;

        result.HasEnoughData = true;
        result.StartDate = dates[0];
        result.EndDate = dates[^1];
        result.Years = (decimal)years;
        result.TradingDays = days.Count;
        result.BuyAndHold = Performance(buyAndHold, years, trades: 1, timeInvested: 100);
        result.Strategy = Performance(strategy.Equity, years, strategy.Trades, strategy.TimeInvested);
        result.StrategyWithInterest = Performance(withInterest.Equity, years, withInterest.Trades, withInterest.TimeInvested);
        result.Verdict = VerdictFor(result.Strategy, result.BuyAndHold);
        result.Outcomes = BuildOutcomes(dates, closes, scores);

        var middle = dates.Count / 2;
        result.BuyMinusSell30D = BuyMinusSell(dates, closes, scores, 0, dates.Count);
        result.BuyMinusSell30DFirstHalf = BuyMinusSell(dates, closes, scores, 0, middle);
        result.BuyMinusSell30DSecondHalf = BuyMinusSell(dates, closes, scores, middle, dates.Count);

        result.EquityCurve = ThinCurve(dates, buyAndHold, strategy.Equity);
        return result;
    }

    /// <summary>
    /// Invested after a positive score, in cash after a negative one, unchanged on a zero score. A signal from
    /// one close is acted on at the next close, so it never benefits from the move that produced it.
    /// Returns the growth of 1 for each day.
    /// </summary>
    public static (double[] Equity, int Trades, decimal TimeInvested) Simulate(
        IReadOnlyList<DateTime> dates, IReadOnlyList<double> closes, IReadOnlyList<int> scores, double cost, double cashInterest)
    {
        var equity = new double[closes.Count];
        equity[0] = 1;
        var invested = false;
        var trades = 0;
        var investedPeriods = 0;

        for (int i = 1; i < closes.Count; i++)
        {
            // First earn the move since yesterday's close with the position held through it
            if (invested)
            {
                equity[i] = equity[i - 1] * closes[i] / closes[i - 1];
                investedPeriods++;
            }
            else
            {
                var elapsedYears = (dates[i] - dates[i - 1]).TotalDays / 365.25;
                equity[i] = equity[i - 1] * Math.Pow(1 + cashInterest, elapsedYears);
            }

            // Then trade at today's close on yesterday's signal
            var previousScore = scores[i - 1];
            var shouldInvest = previousScore > 0 || (previousScore == 0 && invested);
            if (shouldInvest != invested)
            {
                equity[i] *= 1 - cost;
                invested = shouldInvest;
                trades++;
            }
        }

        return (equity, trades, (decimal)investedPeriods / (closes.Count - 1) * 100);
    }

    private static PerformanceDto Performance(double[] equity, double years, int trades, decimal timeInvested)
    {
        double peak = 0, maxDrawdown = 0;
        foreach (var value in equity)
        {
            peak = Math.Max(peak, value);
            maxDrawdown = Math.Min(maxDrawdown, value / peak - 1);
        }

        return new PerformanceDto
        {
            TotalReturn = (decimal)((equity[^1] - 1) * 100),
            AnnualReturn = (decimal)((Math.Pow(equity[^1], 1 / years) - 1) * 100),
            MaxDrawdown = (decimal)(maxDrawdown * 100),
            Trades = trades,
            TimeInvested = timeInvested,
        };
    }

    /// <summary>
    /// Helped: earned at least as much as holding. LowerRisk: earned less, but its worst fall was at least a third
    /// smaller and it earned more per unit of that fall. Otherwise it did not help.
    /// </summary>
    private static string VerdictFor(PerformanceDto strategy, PerformanceDto buyAndHold)
    {
        if (strategy.AnnualReturn >= buyAndHold.AnnualReturn)
            return "Helped";

        var strategyDrawdown = (double)Math.Abs(strategy.MaxDrawdown);
        var holdDrawdown = (double)Math.Abs(buyAndHold.MaxDrawdown);
        var muchSmallerFall = strategyDrawdown <= holdDrawdown * (1 - MeaningfulDrawdownReduction);
        var betterPerFall = strategyDrawdown > 0 && holdDrawdown > 0
            && (double)strategy.AnnualReturn / strategyDrawdown > (double)buyAndHold.AnnualReturn / holdDrawdown;

        return muchSmallerFall && betterPerFall ? "LowerRisk" : "DidNotHelp";
    }

    private static List<SignalOutcomeDto> BuildOutcomes(List<DateTime> dates, List<double> closes, List<int> scores)
    {
        var actions = scores.Select(SignalRules.ActionFor).ToList();
        var forward = Horizons.ToDictionary(h => h, h => ForwardChanges(dates, closes, h));

        var outcomes = Actions.Append("All").Select(action =>
        {
            var indexes = Enumerable.Range(0, actions.Count).Where(i => action == "All" || actions[i] == action).ToList();
            var periods = action == "All" ? 1 : Enumerable.Range(0, actions.Count).Count(i => actions[i] == action && (i == 0 || actions[i - 1] != action));

            return new SignalOutcomeDto
            {
                Action = action,
                Days = indexes.Count,
                Periods = periods,
                Horizons = Horizons.Select(h =>
                {
                    var changes = indexes.Where(i => forward[h][i].HasValue).Select(i => forward[h][i]!.Value).ToList();
                    return new HorizonOutcomeDto
                    {
                        HorizonDays = h,
                        Samples = changes.Count,
                        AverageChange = changes.Count > 0 ? (decimal)changes.Average() : 0,
                        MedianChange = changes.Count > 0 ? (decimal)Median(changes) : 0,
                        PercentPositive = changes.Count > 0 ? (decimal)changes.Count(c => c > 0) / changes.Count * 100 : 0,
                    };
                }).ToList(),
            };
        }).ToList();

        return outcomes.Where(o => o.Days > 0).ToList();
    }

    /// <summary>
    /// For each day's signal, the % change from the next close (when it could be acted on) to the first close at
    /// least <paramref name="horizonDays"/> later. Empty near the end of history or across a large data gap.
    /// </summary>
    private static double?[] ForwardChanges(List<DateTime> dates, List<double> closes, int horizonDays)
    {
        const int gapToleranceDays = 10;
        var changes = new double?[dates.Count];
        var end = 0;

        for (int i = 0; i + 1 < dates.Count; i++)
        {
            var entry = i + 1;
            var target = dates[entry].AddDays(horizonDays);
            end = Math.Max(end, entry);
            while (end < dates.Count && dates[end] < target)
                end++;
            if (end >= dates.Count)
                break;
            if ((dates[end] - target).TotalDays <= gapToleranceDays)
                changes[i] = (closes[end] / closes[entry] - 1) * 100;
        }
        return changes;
    }

    private static decimal? BuyMinusSell(List<DateTime> dates, List<double> closes, List<int> scores, int from, int to)
    {
        var forward = ForwardChanges(dates, closes, 30);
        var buys = new List<double>();
        var sells = new List<double>();
        for (int i = from; i < to; i++)
        {
            if (forward[i] is not double change)
                continue;
            if (scores[i] > 0) buys.Add(change);
            else if (scores[i] < 0) sells.Add(change);
        }
        return buys.Count > 0 && sells.Count > 0 ? (decimal)(buys.Average() - sells.Average()) : null;
    }

    private static List<EquityPointDto> ThinCurve(List<DateTime> dates, double[] buyAndHold, double[] strategy)
    {
        var step = Math.Max(1, dates.Count / MaxCurvePoints);
        var curve = new List<EquityPointDto>();
        for (int i = 0; i < dates.Count; i++)
        {
            if (i % step == 0 || i == dates.Count - 1)
                curve.Add(new EquityPointDto { Date = dates[i], BuyAndHold = (decimal)(buyAndHold[i] * 100), Strategy = (decimal)(strategy[i] * 100) });
        }
        return curve;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
