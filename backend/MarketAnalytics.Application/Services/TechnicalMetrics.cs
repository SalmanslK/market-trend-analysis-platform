using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// The standard technical and risk metrics that can be calculated from one closing price per day.
/// Sources provide no daily high, low or volume, so metrics that need them (ATR, ADX, OBV, VWAP, MFI) are left out,
/// and Stochastic, Williams %R, CCI and Donchian channels use closes in place of highs and lows.
/// Metrics without enough history are omitted. Readings describe the indicator's usual meaning; they aren't advice.
/// </summary>
public static class TechnicalMetrics
{
    /// <summary>Assumed yearly return on cash for Sharpe, Sortino and alpha, since no rate history is stored.</summary>
    public const double AssumedRiskFreePercent = 10;

    public const string MovingAverages = "Moving averages";
    public const string Momentum = "Momentum";
    public const string Volatility = "Volatility";
    public const string RiskAndReturn = "Risk and return";

    private const string Positive = "Positive";
    private const string Negative = "Negative";
    private const string Neutral = "Neutral";
    private const int MinReturns = 20;
    private const int OscillatorPeriod = 14;
    private const int ChannelPeriod = 20;
    private static readonly int[] AveragePeriods = { 10, 20, 50, 100, 200 };

    public static List<MetricDto> Compute(
        IReadOnlyList<PriceDataPoint> prices,
        IIndicatorCalculationService indicators,
        IReadOnlyList<PriceDataPoint>? benchmark = null)
    {
        var sorted = prices.OrderBy(p => p.DateTime).ToList();
        var metrics = new List<MetricDto>();
        if (sorted.Count == 0)
            return metrics;

        AddMovingAverages(metrics, sorted, indicators);
        AddMomentum(metrics, sorted, indicators);
        AddVolatility(metrics, sorted, indicators);
        AddRiskAndReturn(metrics, sorted, benchmark);
        return metrics;
    }

    private static void Add(List<MetricDto> metrics, string group, string name, double? value, string unit, string reading, string note, string description)
    {
        if (value is not double number || double.IsNaN(number) || double.IsInfinity(number))
            return;

        metrics.Add(new MetricDto
        {
            Group = group,
            Name = name,
            Value = (decimal)number,
            Unit = unit,
            Reading = reading,
            Note = note,
            Description = description,
        });
    }

    private static void AddMovingAverages(List<MetricDto> metrics, List<PriceDataPoint> prices, IIndicatorCalculationService indicators)
    {
        var last = prices[^1];

        void AddAverage(string name, decimal? average, string description)
        {
            if (average is not decimal value)
                return;
            var above = last.Price >= value;
            Add(metrics, MovingAverages, name, (double)value, "price", above ? Positive : Negative, above ? "Price is above it" : "Price is below it", description);
        }

        foreach (var period in AveragePeriods.Where(p => prices.Count >= p))
        {
            AddAverage($"SMA {period}", indicators.ComputeSma(prices, period)[last.DateTime],
                $"Simple moving average: the plain average of the last {period} closing prices. Price above it suggests an upward trend over that period.");
            AddAverage($"EMA {period}", indicators.ComputeEma(prices, period)[last.DateTime],
                $"Exponential moving average over {period} periods: like the SMA, but recent prices count more, so it reacts faster.");
        }
    }

    private static void AddMomentum(List<MetricDto> metrics, List<PriceDataPoint> prices, IIndicatorCalculationService indicators)
    {
        var closes = prices.Select(p => (double)p.Price).ToList();
        var last = prices[^1].DateTime;
        var close = closes[^1];

        void AddOscillator(string name, double? value, double overbought, double oversold, string description)
        {
            var (reading, note) = value >= overbought
                ? (Negative, "Overbought: it has risen fast")
                : value <= oversold
                    ? (Positive, "Oversold: it has fallen fast")
                    : (Neutral, "In its normal range");
            Add(metrics, Momentum, name, value, "number", reading, note, description);
        }

        void AddDirection(string name, double? value, string unit, string description)
        {
            var reading = value >= 0 ? Positive : Negative;
            Add(metrics, Momentum, name, value, unit, reading, value >= 0 ? "Pointing up" : "Pointing down", description);
        }

        var rsiSeries = indicators.ComputeRsi(prices, SignalRules.RsiPeriod);
        AddOscillator("RSI (14)", (double?)rsiSeries[last], 70, 30,
            "Relative Strength Index: a 0-100 gauge of how strongly the price has been rising or falling. Above 70 is overbought, below 30 oversold.");

        // Fast stochastic of RSI over its own recent range
        var rsiValues = prices.Select(p => rsiSeries[p.DateTime]).Where(r => r.HasValue).Select(r => (double)r!.Value).ToList();
        AddOscillator("Stochastic RSI (14)", StochasticOf(rsiValues, OscillatorPeriod), 80, 20,
            "Where RSI sits within its own range over the last 14 periods, from 0 to 100. Above 80 is overbought, below 20 oversold. More jumpy than RSI.");

        var percentK = Enumerable.Range(0, 3)
            .Select(back => closes.Count - back >= OscillatorPeriod ? StochasticOf(closes.Take(closes.Count - back).ToList(), OscillatorPeriod) : null)
            .ToList();
        AddOscillator("Stochastic %K (14, closes)", percentK[0], 80, 20,
            "Where the close sits within its range over the last 14 periods, from 0 to 100. Normally uses daily highs and lows; here it uses closes.");
        AddOscillator("Stochastic %D (3, closes)", percentK.All(k => k.HasValue) ? percentK.Average(k => k!.Value) : null, 80, 20,
            "The 3-period average of Stochastic %K: a smoother version of the same reading.");

        var stochastic = percentK[0];
        AddOscillator("Williams %R (14, closes)", stochastic.HasValue ? stochastic.Value - 100 : null, -20, -80,
            "The Stochastic reading on a scale from -100 to 0. Above -20 is overbought, below -80 oversold. Normally uses highs and lows; here it uses closes.");

        AddOscillator("CCI (20, closes)", Cci(closes, ChannelPeriod), 100, -100,
            "Commodity Channel Index: how far the price is from its 20-period average, scaled by its usual distance. Above +100 is stretched up, below -100 stretched down.");

        var (macd, signal, histogram) = indicators.ComputeMacd(prices);
        AddDirection("MACD (12, 26)", (double?)macd[last], "number",
            "The 12-period EMA minus the 26-period EMA. Above zero means the shorter-term average is above the longer-term one.");
        Add(metrics, Momentum, "MACD signal (9)", (double?)signal[last], "number", "", "The line MACD is compared with",
            "A 9-period EMA of MACD. MACD crossing above it is a classic sign of rising momentum.");
        AddDirection("MACD histogram", (double?)histogram[last], "number",
            "MACD minus its signal line. Above zero means momentum is rising; below zero, fading.");

        var ema12 = (double?)indicators.ComputeEma(prices, 12)[last];
        var ema26 = (double?)indicators.ComputeEma(prices, 26)[last];
        AddDirection("PPO (12, 26)", closes.Count >= 26 ? (ema12 - ema26) / ema26 * 100 : null, "percent",
            "Percentage Price Oscillator: MACD as a % of the price, so it can be compared across instruments.");

        AddDirection("Rate of change (12)", closes.Count > 12 ? (close / closes[^13] - 1) * 100 : null, "percent",
            "How much the price has changed over the last 12 periods, in percent.");
        AddDirection("Momentum (10)", closes.Count > 10 ? close - closes[^11] : null, "number",
            "The price now minus the price 10 periods ago.");
        AddDirection("TRIX (15)", Trix(closes, 15), "percent",
            "The 1-period % change of a triple-smoothed 15-period EMA. It filters out small moves to show the underlying direction.");
    }

    private static void AddVolatility(List<MetricDto> metrics, List<PriceDataPoint> prices, IIndicatorCalculationService indicators)
    {
        var last = prices[^1];
        var closes = prices.Select(p => (double)p.Price).ToList();

        var (upper, lower) = indicators.ComputeBollingerBands(prices);
        var sma20 = indicators.ComputeSma20(prices)[last.DateTime];
        if (upper[last.DateTime] is decimal upperBand && lower[last.DateTime] is decimal lowerBand && sma20 is decimal middle)
        {
            Add(metrics, Volatility, "Bollinger upper band", (double)upperBand, "price", last.Price > upperBand ? Negative : "", last.Price > upperBand ? "Price is above the band" : "Top of the normal range",
                "The 20-period average plus 2 standard deviations. Prices usually stay below it.");
            Add(metrics, Volatility, "Bollinger lower band", (double)lowerBand, "price", last.Price < lowerBand ? Positive : "", last.Price < lowerBand ? "Price is below the band" : "Bottom of the normal range",
                "The 20-period average minus 2 standard deviations. Prices usually stay above it.");

            if (SignalRules.PercentB(last.Price, upperBand, lowerBand) is decimal percentB)
            {
                var (reading, note) = percentB > 1 ? (Negative, "Above the upper band") : percentB < 0 ? (Positive, "Below the lower band") : (Neutral, "Inside the bands");
                Add(metrics, Volatility, "Bollinger %B", (double)percentB, "number", reading, note,
                    "Where the price sits between the bands: 0 is the lower band, 1 the upper band.");
            }
            Add(metrics, Volatility, "Bollinger bandwidth", middle > 0 ? (double)((upperBand - lowerBand) / middle * 100) : null, "percent", "", "Wider means more volatile",
                "The gap between the bands as a % of the average. Very narrow bands often come before bigger moves.");
        }

        if (closes.Count >= ChannelPeriod)
        {
            var channel = closes.TakeLast(ChannelPeriod).ToList();
            Add(metrics, Volatility, "Donchian high (20, closes)", channel.Max(), "price", "", "Highest close in 20 periods",
                "The top of the 20-period price channel. A close above it is a classic breakout sign. Normally uses highs; here it uses closes.");
            Add(metrics, Volatility, "Donchian low (20, closes)", channel.Min(), "price", "", "Lowest close in 20 periods",
                "The bottom of the 20-period price channel. Normally uses lows; here it uses closes.");
        }

        if (prices.Count > ChannelPeriod)
        {
            var recent = prices.TakeLast(ChannelPeriod + 1).ToList();
            var returns = SimpleReturns(recent);
            Add(metrics, Volatility, "Volatility (20 periods)", StandardDeviation(returns) * Math.Sqrt(PeriodsPerYear(recent)) * 100, "percent", "", "Recent swings, as a yearly rate",
                "How much the price has been swinging over the last 20 periods, expressed per year. Compare it with the 1-year figure to see if things are calmer or wilder than usual.");
        }
    }

    private static void AddRiskAndReturn(List<MetricDto> metrics, List<PriceDataPoint> prices, IReadOnlyList<PriceDataPoint>? benchmark)
    {
        var last = prices[^1];

        void AddGrowth(string name, int years)
        {
            var value = Cagr(prices, years);
            Add(metrics, RiskAndReturn, name, value, "percent", value >= 0 ? Positive : Negative, "Growth per year, excluding dividends",
                $"Compound annual growth rate: the steady yearly growth that matches the actual change over {years} years.");
        }

        AddGrowth("CAGR (1 year)", 1);
        AddGrowth("CAGR (3 years)", 3);
        AddGrowth("CAGR (5 years)", 5);
        AddGrowth("CAGR (10 years)", 10);

        var allTimeDrawdown = MaxDrawdown(prices);
        Add(metrics, RiskAndReturn, "Max drawdown (all history)", allTimeDrawdown, "percent", Negative, "Worst fall from a peak",
            "The biggest fall from a high to a later low across all stored prices.");

        var threeYears = last.DateTime.AddYears(-3);
        if (prices[0].DateTime <= threeYears && Cagr(prices, 3) is double threeYearGrowth)
        {
            var drawdown = MaxDrawdown(prices.Where(p => p.DateTime >= threeYears).ToList());
            var calmar = drawdown < 0 ? threeYearGrowth / Math.Abs(drawdown) : (double?)null;
            Add(metrics, RiskAndReturn, "Calmar ratio (3 years)", calmar, "number", calmar >= 1 ? Positive : calmar < 0 ? Negative : Neutral, "Growth per unit of worst fall",
                "Yearly growth divided by the worst fall over 3 years. Above 1 means growth has been larger than the worst fall.");
        }

        var yearAgo = last.DateTime.AddYears(-1);
        var startIndex = prices.FindLastIndex(p => p.DateTime <= yearAgo);
        if (startIndex < 0)
            return;

        var year = prices.Skip(startIndex).ToList();
        var yearly = SimpleReturns(year);
        if (yearly.Count < MinReturns)
            return;

        var periodsPerYear = PeriodsPerYear(year);
        var growth = ((double)last.Price / (double)year[0].Price - 1) * 100;
        var volatility = StandardDeviation(yearly) * Math.Sqrt(periodsPerYear) * 100;

        Add(metrics, RiskAndReturn, "Volatility (1 year)", volatility, "percent", "", "Typical yearly swing",
            "How much the price typically swings in a year. Higher means bigger possible gains and losses.");

        var sharpe = volatility > 0 ? (growth - AssumedRiskFreePercent) / volatility : (double?)null;
        Add(metrics, RiskAndReturn, "Sharpe ratio (1 year)", sharpe, "number", sharpe >= 1 ? Positive : sharpe < 0 ? Negative : Neutral,
            sharpe >= 1 ? "Good return for the risk" : sharpe < 0 ? "Earned less than cash" : "Modest return for the risk",
            $"Return above cash, per unit of volatility, assuming cash earns {AssumedRiskFreePercent:0}% a year. Above 1 is generally considered good; below 0 means cash did better.");

        var dailyTarget = AssumedRiskFreePercent / 100 / periodsPerYear;
        var downsideDeviation = Math.Sqrt(yearly.Sum(r => Math.Pow(Math.Min(r - dailyTarget, 0), 2)) / yearly.Count) * Math.Sqrt(periodsPerYear) * 100;
        var sortino = downsideDeviation > 0 ? (growth - AssumedRiskFreePercent) / downsideDeviation : (double?)null;
        Add(metrics, RiskAndReturn, "Sortino ratio (1 year)", sortino, "number", sortino >= 1 ? Positive : sortino < 0 ? Negative : Neutral, "Like Sharpe, counting only falls",
            $"Like the Sharpe ratio, but only downward swings count as risk. Assumes cash earns {AssumedRiskFreePercent:0}% a year.");

        var ordered = yearly.OrderBy(r => r).ToList();
        Add(metrics, RiskAndReturn, "Value at Risk (95%, 1 period)", ordered[(int)Math.Floor(ordered.Count * 0.05)] * 100, "percent", "", "On 1 day in 20 it fell at least this much",
            "Over the last year, about 1 period in 20 was this bad or worse. It isn't a limit: falls can be larger.");
        Add(metrics, RiskAndReturn, "Best period (1 year)", ordered[^1] * 100, "percent", Positive, "Biggest one-period rise", "The largest single rise over the last year.");
        Add(metrics, RiskAndReturn, "Worst period (1 year)", ordered[0] * 100, "percent", Negative, "Biggest one-period fall", "The largest single fall over the last year.");
        Add(metrics, RiskAndReturn, "Periods up (1 year)", (double)yearly.Count(r => r > 0) / yearly.Count * 100, "percent", "", "Share of periods that rose", "The share of periods over the last year when the price rose.");

        if (benchmark != null)
            AddBenchmarkMetrics(metrics, year, benchmark, growth);
    }

    private static void AddBenchmarkMetrics(List<MetricDto> metrics, List<PriceDataPoint> year, IReadOnlyList<PriceDataPoint> benchmark, double growth)
    {
        var benchmarkByDate = benchmark.GroupBy(p => p.DateTime.Date).ToDictionary(g => g.Key, g => g.Last().Price);
        var shared = year.Where(p => benchmarkByDate.ContainsKey(p.DateTime.Date)).ToList();
        if (shared.Count <= MinReturns)
            return;

        var own = SimpleReturns(shared);
        var market = SimpleReturns(shared.Select(p => new PriceDataPoint(p.DateTime, benchmarkByDate[p.DateTime.Date])).ToList());
        var ownMean = own.Average();
        var marketMean = market.Average();
        var covariance = own.Zip(market, (a, b) => (a - ownMean) * (b - marketMean)).Sum() / (own.Count - 1);
        var marketDeviation = StandardDeviation(market);
        var ownDeviation = StandardDeviation(own);
        if (marketDeviation == 0)
            return;

        var beta = covariance / (marketDeviation * marketDeviation);
        var correlation = ownDeviation > 0 ? covariance / (ownDeviation * marketDeviation) : (double?)null;
        var marketGrowth = ((double)benchmarkByDate[shared[^1].DateTime.Date] / (double)benchmarkByDate[shared[0].DateTime.Date] - 1) * 100;
        var alpha = growth - (AssumedRiskFreePercent + beta * (marketGrowth - AssumedRiskFreePercent));

        Add(metrics, RiskAndReturn, "Beta vs KSE-100 (1 year)", beta, "number", "", beta > 1.1 ? "Moves more than the market" : beta < 0.9 ? "Moves less than the market" : "Moves about with the market",
            "How much it tends to move when the KSE-100 moves. 1 means the same amount, 2 twice as much, 0 no link.");
        Add(metrics, RiskAndReturn, "Correlation vs KSE-100 (1 year)", correlation, "number", "", "1 = moves together, 0 = no link",
            "How closely it moves with the KSE-100, from -1 (opposite) to +1 (together). Low correlation helps spread risk.");
        Add(metrics, RiskAndReturn, "Alpha vs KSE-100 (1 year)", alpha, "percent", alpha >= 0 ? Positive : Negative, alpha >= 0 ? "Did better than its beta implies" : "Did worse than its beta implies",
            $"Return beyond what its beta and the KSE-100 would explain, assuming cash earns {AssumedRiskFreePercent:0}% a year.");
    }

    private static double? StochasticOf(IReadOnlyList<double> values, int period)
    {
        if (values.Count < period)
            return null;
        var window = values.Skip(values.Count - period).ToList();
        var high = window.Max();
        var low = window.Min();
        return high > low ? (values[^1] - low) / (high - low) * 100 : 50;
    }

    private static double? Cci(IReadOnlyList<double> closes, int period)
    {
        if (closes.Count < period)
            return null;
        var window = closes.Skip(closes.Count - period).ToList();
        var mean = window.Average();
        var meanDeviation = window.Average(c => Math.Abs(c - mean));
        return meanDeviation > 0 ? (closes[^1] - mean) / (0.015 * meanDeviation) : 0;
    }

    private static double? Trix(IReadOnlyList<double> closes, int period)
    {
        if (closes.Count < period * 3)
            return null;
        var triple = Ema(Ema(Ema(closes, period), period), period);
        return (triple[^1] / triple[^2] - 1) * 100;
    }

    private static List<double> Ema(IReadOnlyList<double> values, int period)
    {
        var alpha = 2.0 / (period + 1);
        var result = new List<double>(values.Count) { values[0] };
        for (int i = 1; i < values.Count; i++)
            result.Add(alpha * values[i] + (1 - alpha) * result[i - 1]);
        return result;
    }

    private static double? Cagr(List<PriceDataPoint> prices, int years)
    {
        var last = prices[^1];
        var target = last.DateTime.AddYears(-years);
        var start = prices.LastOrDefault(p => p.DateTime <= target);
        return start == null ? null : (Math.Pow((double)last.Price / (double)start.Price, 1.0 / years) - 1) * 100;
    }

    private static double MaxDrawdown(IReadOnlyList<PriceDataPoint> prices)
    {
        double peak = 0, worst = 0;
        foreach (var point in prices)
        {
            peak = Math.Max(peak, (double)point.Price);
            worst = Math.Min(worst, (double)point.Price / peak - 1);
        }
        return worst * 100;
    }

    private static List<double> SimpleReturns(IReadOnlyList<PriceDataPoint> prices) =>
        prices.Zip(prices.Skip(1), (a, b) => (double)b.Price / (double)a.Price - 1).ToList();

    private static double StandardDeviation(List<double> values)
    {
        if (values.Count < 2)
            return 0;
        var mean = values.Average();
        return Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1));
    }

    // Calendars differ: stock indices trade ~250 days a year, gold and forex snapshots run every day
    private static double PeriodsPerYear(IReadOnlyList<PriceDataPoint> prices)
    {
        var days = (prices[^1].DateTime - prices[0].DateTime).TotalDays;
        return days > 0 ? (prices.Count - 1) * 365.25 / days : 0;
    }
}
