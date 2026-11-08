using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Tests mixes of instruments over stored prices: how a portfolio split between, say, a stock fund, a gold fund and
/// a cash fund would have done, compared with holding each part alone.
///
/// The same honesty rules as the backtests apply:
/// - Fund prices include payouts, reinvested. Every mix in one comparison uses the same dates.
/// - Rebalancing happens at the first close of each period. The trend filter uses each part's own golden cross
///   (50-period average above the 200-period) from the previous close, acted on at today's close; until a part has
///   200 prices, it counts as up.
/// - Each buy or sell pays the cost, except moves into and out of cash, which Meezan's cash fund charges nothing for.
/// - Money not in the mix, or moved out by the trend filter, earns what the cash fund earned (nothing before it has data).
/// </summary>
public class PortfolioService : IPortfolioService
{
    public const string CashSymbol = "MCF";
    public const int MaxMixes = 12;
    private static readonly string[] Schedules = { "Never", "Yearly", "HalfYearly", "Quarterly" };

    /// <summary>
    /// Mixes fixed before seeing any result: common splits between Meezan's stock, gold and cash funds.
    /// </summary>
    public static readonly IReadOnlyList<PortfolioRequestDto> Presets = new List<PortfolioRequestDto>
    {
        Mix("All in stocks (MIF)", "Never", false, ("MIF", 100)),
        Mix("Stocks 60, gold 20, cash 20", "Yearly", false, ("MIF", 60), ("MGF", 20), ("MCF", 20)),
        Mix("Stocks 40, gold 30, cash 30", "Yearly", false, ("MIF", 40), ("MGF", 30), ("MCF", 30)),
        Mix("Stocks 50, gold 50", "Yearly", false, ("MIF", 50), ("MGF", 50)),
        Mix("Stocks 20, gold 20, cash 60", "Yearly", false, ("MIF", 20), ("MGF", 20), ("MCF", 60)),
        Mix("All in gold (MGF)", "Never", false, ("MGF", 100)),
        Mix("All in cash (MCF)", "Never", false, ("MCF", 100)),
        Mix("All in stocks, with trend filter", "Never", true, ("MIF", 100)),
        Mix("Stocks 60, gold 20, cash 20, with trend filter", "Yearly", true, ("MIF", 60), ("MGF", 20), ("MCF", 20)),
    };

    private readonly IInstrumentRepository _instrumentRepository;
    private readonly IPricePointRepository _pricePointRepository;
    private readonly IFundPayoutRepository? _fundPayoutRepository;

    public PortfolioService(
        IInstrumentRepository instrumentRepository,
        IPricePointRepository pricePointRepository,
        IFundPayoutRepository? fundPayoutRepository = null)
    {
        _instrumentRepository = instrumentRepository;
        _pricePointRepository = pricePointRepository;
        _fundPayoutRepository = fundPayoutRepository;
    }

    public async Task<PortfolioComparisonDto> CompareAsync(IReadOnlyList<PortfolioRequestDto> mixes, decimal costPercent, CancellationToken cancellationToken = default)
    {
        var instruments = (await _instrumentRepository.GetActiveInstrumentsAsync(cancellationToken)).ToList();
        var prices = new Dictionary<string, List<PriceDataPoint>>(StringComparer.OrdinalIgnoreCase);
        foreach (var instrument in instruments)
        {
            var history = await PriceHistory.LoadDailyAsync(_pricePointRepository, _fundPayoutRepository, instrument.Id, cancellationToken: cancellationToken);
            if (history.Count > 1)
                prices[instrument.Symbol] = history;
        }

        var comparison = new PortfolioComparisonDto
        {
            CostPercent = costPercent,
            Instruments = instruments
                .Where(i => prices.ContainsKey(i.Symbol))
                .Select(i => new PortfolioInstrumentDto { Symbol = i.Symbol, Name = i.Name, FirstDate = prices[i.Symbol][0].DateTime, IsCash = IsCash(i.Symbol), LowRiskKind = LowRiskKind(i.Symbol) })
                .OrderBy(i => i.IsCash).ThenBy(i => i.Symbol)
                .ToList(),
        };

        var valid = mixes.Select(Normalize).Where(m => m.Weights.Count > 0 && m.Weights.Keys.All(prices.ContainsKey)).Take(MaxMixes).ToList();
        if (valid.Count == 0)
            return comparison;

        // Every mix uses the same dates: from the day all its parts have prices to the latest price
        var symbols = valid.SelectMany(m => m.Weights.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var start = symbols.Max(s => prices[s][0].DateTime);
        var dates = symbols.SelectMany(s => prices[s].Select(p => p.DateTime)).Where(d => d >= start).Distinct().OrderBy(d => d).ToList();
        if (dates.Count < 2)
            return comparison;

        var cash = prices.TryGetValue(CashSymbol, out var cashPrices) ? Align(cashPrices, dates, fillBefore: false) : null;
        comparison.StartDate = dates[0];
        comparison.EndDate = dates[^1];
        if (cash != null && cashPrices![0].DateTime <= start)
            comparison.CashAnnualReturn = Round(Annualized(cash[^1] / cash[0], dates[0], dates[^1]) * 100);

        foreach (var mix in valid)
        {
            var parts = mix.Weights.Keys.ToList();
            var series = parts.Select(s => Align(prices[s], dates, fillBefore: true)).ToList();
            var trend = parts.Select(s => mix.TrendFilter && !IsCash(s) ? TrendUp(prices[s], dates) : null).ToList();
            var run = Simulate(
                dates,
                series,
                trend,
                parts.Select(s => (double)mix.Weights[s] / 100).ToList(),
                parts.Select(s => !IsCash(s)).ToList(),
                cash,
                mix.Rebalance,
                (double)costPercent / 100);

            var result = Measure(dates, run.Equity, comparison.CashAnnualReturn);
            result.Name = mix.Name;
            result.Weights = mix.Weights;
            result.Rebalance = mix.Rebalance;
            result.TrendFilter = mix.TrendFilter;
            result.CostPercent = costPercent;
            result.Trades = run.Trades;
            comparison.Results.Add(result);
        }

        return comparison;
    }

    /// <summary>
    /// Low-risk funds whose price only creeps up: Buy/Sell signals don't apply to them, they are left out of the signal
    /// backtests, and moving money into or out of them costs nothing in a mix. MCF is the one cash earns in mixes.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> LowRiskFunds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [CashSymbol] = "Cash fund",
        ["MRAF"] = "Cash fund",
        ["MIIF"] = "Income fund",
        ["MMP-I"] = "Income fund",
    };

    public static bool IsCash(string symbol) => LowRiskFunds.ContainsKey(symbol);

    /// <summary>"Cash fund" or "Income fund" for a low-risk fund, otherwise null.</summary>
    public static string? LowRiskKind(string symbol) => LowRiskFunds.GetValueOrDefault(symbol);

    /// <summary>
    /// Runs a mix day by day. Prices are aligned to the dates; trendUp, per part, is null when not filtered.
    /// Returns the growth of 1 for each day and the number of buys and sells.
    /// </summary>
    public static (double[] Equity, int Trades) Simulate(
        IReadOnlyList<DateTime> dates,
        IReadOnlyList<double[]> prices,
        IReadOnlyList<bool[]?> trendUp,
        IReadOnlyList<double> weights,
        IReadOnlyList<bool> charged,
        double[]? cash,
        string rebalance,
        double cost)
    {
        var parts = weights.Count;
        double CostOf(int a) => charged[a] ? cost : 0;
        bool Allowed(int a, int day) => trendUp[a]?[day] ?? true;

        var holdings = new double[parts];
        var allowed = new bool[parts];
        var cashHeld = 1.0;
        var trades = 0;

        // Buy the mix at the first close
        for (int a = 0; a < parts; a++)
        {
            allowed[a] = Allowed(a, 0);
            if (!allowed[a] || weights[a] <= 0) continue;
            holdings[a] = weights[a] * (1 - CostOf(a));
            cashHeld -= weights[a];
            trades++;
        }

        var equity = new double[dates.Count];
        equity[0] = holdings.Sum() + cashHeld;

        for (int i = 1; i < dates.Count; i++)
        {
            for (int a = 0; a < parts; a++)
                holdings[a] *= prices[a][i] / prices[a][i - 1];
            if (cash != null && cash[i - 1] > 0)
                cashHeld *= cash[i] / cash[i - 1];

            var total = holdings.Sum() + cashHeld;
            var wanted = Enumerable.Range(0, parts).Select(a => Allowed(a, i - 1)).ToArray();

            if (IsNewPeriod(dates[i - 1], dates[i], rebalance))
            {
                // Back to the target weights, with parts the trend filter keeps out sitting in cash
                var target = Enumerable.Range(0, parts).Select(a => wanted[a] ? total * weights[a] : 0).ToArray();
                var paid = 0.0;
                for (int a = 0; a < parts; a++)
                {
                    var traded = Math.Abs(target[a] - holdings[a]);
                    if (traded > total * 1e-9) trades++;
                    paid += traded * CostOf(a);
                }
                var scale = 1 - paid / total;
                for (int a = 0; a < parts; a++)
                    holdings[a] = target[a] * scale;
                cashHeld = (total - target.Sum()) * scale;
            }
            else
            {
                // Between rebalances, only parts whose trend changed move, to or from cash
                for (int a = 0; a < parts; a++)
                {
                    if (wanted[a] == allowed[a] || weights[a] <= 0) continue;
                    if (!wanted[a])
                    {
                        cashHeld += holdings[a] * (1 - CostOf(a));
                        holdings[a] = 0;
                    }
                    else
                    {
                        var amount = Math.Min(cashHeld, total * weights[a]);
                        holdings[a] += amount * (1 - CostOf(a));
                        cashHeld -= amount;
                    }
                    trades++;
                }
            }

            allowed = wanted;
            equity[i] = holdings.Sum() + cashHeld;
        }

        return (equity, trades);
    }

    public static bool IsNewPeriod(DateTime previous, DateTime current, string rebalance) => rebalance switch
    {
        "Yearly" => current.Year != previous.Year,
        "HalfYearly" => current.Year * 2 + (current.Month - 1) / 6 != previous.Year * 2 + (previous.Month - 1) / 6,
        "Quarterly" => current.Year * 4 + (current.Month - 1) / 3 != previous.Year * 4 + (previous.Month - 1) / 3,
        _ => false,
    };

    /// <summary>
    /// Prices on each date, carrying the last known price forward (instruments trade on different days).
    /// Before the first price: the first price when fillBefore, otherwise 0.
    /// </summary>
    public static double[] Align(IReadOnlyList<PriceDataPoint> prices, IReadOnlyList<DateTime> dates, bool fillBefore)
    {
        var result = new double[dates.Count];
        var j = -1;
        for (int i = 0; i < dates.Count; i++)
        {
            while (j + 1 < prices.Count && prices[j + 1].DateTime <= dates[i]) j++;
            result[i] = j >= 0 ? (double)prices[j].Price : fillBefore ? (double)prices[0].Price : 0;
        }
        return result;
    }

    /// <summary>
    /// Golden cross on each date from the instrument's own full history; up until it has 200 prices.
    /// </summary>
    public static bool[] TrendUp(IReadOnlyList<PriceDataPoint> prices, IReadOnlyList<DateTime> dates)
    {
        var up = new bool[prices.Count];
        double sum50 = 0, sum200 = 0;
        for (int i = 0; i < prices.Count; i++)
        {
            var p = (double)prices[i].Price;
            sum50 += p;
            sum200 += p;
            if (i >= 50) sum50 -= (double)prices[i - 50].Price;
            if (i >= 200) sum200 -= (double)prices[i - 200].Price;
            up[i] = i < 199 || SignalRules.GoldenCross((decimal)(sum50 / 50), (decimal)(sum200 / 200)) > 0;
        }

        var result = new bool[dates.Count];
        var j = -1;
        for (int i = 0; i < dates.Count; i++)
        {
            while (j + 1 < prices.Count && prices[j + 1].DateTime <= dates[i]) j++;
            result[i] = j < 0 || up[j];
        }
        return result;
    }

    public static PortfolioResultDto Measure(IReadOnlyList<DateTime> dates, double[] equity, decimal? cashAnnualReturn)
    {
        var start = dates[0];
        var end = dates[^1];
        var years = (end - start).TotalDays / 365.25;

        double peak = equity[0], worst = 0;
        foreach (var value in equity)
        {
            peak = Math.Max(peak, value);
            worst = Math.Min(worst, value / peak - 1);
        }

        var changes = Enumerable.Range(1, equity.Length - 1).Select(i => Math.Log(equity[i] / equity[i - 1])).ToList();
        var mean = changes.Average();
        var sd = Math.Sqrt(changes.Sum(c => (c - mean) * (c - mean)) / Math.Max(1, changes.Count - 1));
        var volatility = years > 0 ? sd * Math.Sqrt(changes.Count / years) : 0;
        var annual = Annualized(equity[^1] / equity[0], start, end);

        var result = new PortfolioResultDto
        {
            StartDate = start,
            EndDate = end,
            Years = Round(years),
            TotalReturn = Round((equity[^1] / equity[0] - 1) * 100),
            AnnualReturn = Round(annual * 100),
            MaxDrawdown = Round(worst * 100),
            Volatility = Round(volatility * 100),
            Sharpe = cashAnnualReturn is decimal cashReturn && volatility > 0
                ? Round((annual * 100 - (double)cashReturn) / (volatility * 100))
                : null,
        };

        // Calendar-year returns, from each year's last close to the next
        var yearEnds = Enumerable.Range(0, dates.Count).GroupBy(i => dates[i].Year).Select(g => (Year: g.Key, Last: g.Max())).ToList();
        var previous = equity[0];
        foreach (var (year, last) in yearEnds)
        {
            result.YearlyReturns.Add(new YearReturnDto { Year = year, Return = Round((equity[last] / previous - 1) * 100) });
            previous = equity[last];
        }

        // One point a week keeps the response small
        var lastWeek = DateTime.MinValue;
        for (int i = 0; i < dates.Count; i++)
        {
            if (i == dates.Count - 1 || (dates[i] - lastWeek).TotalDays >= 7)
            {
                result.EquityCurve.Add(new PortfolioPointDto { Date = dates[i], Value = Round(equity[i] / equity[0] * 100) });
                lastWeek = dates[i];
            }
        }

        return result;
    }

    private static double Annualized(double growth, DateTime start, DateTime end)
    {
        var years = (end - start).TotalDays / 365.25;
        return years > 0 && growth > 0 ? Math.Pow(growth, 1 / years) - 1 : 0;
    }

    private static decimal Round(double value) => Math.Round((decimal)value, 2);

    /// <summary>Keeps known symbols with positive weights, scales weights over 100% down to 100%, and checks the schedule.</summary>
    private static PortfolioRequestDto Normalize(PortfolioRequestDto mix)
    {
        var weights = mix.Weights
            .Where(w => w.Value > 0)
            .GroupBy(w => w.Key.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Sum(w => w.Value));
        var sum = weights.Values.Sum();
        if (sum > 100)
            weights = weights.ToDictionary(w => w.Key, w => Math.Round(w.Value * 100 / sum, 2));

        return new PortfolioRequestDto
        {
            Name = string.IsNullOrWhiteSpace(mix.Name) ? string.Join(", ", weights.Select(w => $"{w.Key} {w.Value:0.#}%")) : mix.Name.Trim(),
            Weights = weights,
            Rebalance = Schedules.Contains(mix.Rebalance) ? mix.Rebalance : "Yearly",
            TrendFilter = mix.TrendFilter,
        };
    }

    private static PortfolioRequestDto Mix(string name, string rebalance, bool trendFilter, params (string Symbol, decimal Weight)[] parts) => new()
    {
        Name = name,
        Rebalance = rebalance,
        TrendFilter = trendFilter,
        Weights = parts.ToDictionary(p => p.Symbol, p => p.Weight),
    };
}
