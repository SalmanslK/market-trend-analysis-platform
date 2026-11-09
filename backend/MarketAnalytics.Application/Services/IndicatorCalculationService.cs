using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Service for calculating technical indicators.
/// Matches Python indicator computation logic exactly.
/// - SMA-20: 20-period rolling mean
/// - Bollinger Bands: SMA-20 ± 2*std (20-period rolling std dev)
/// - Interval-specific MAs: MA_4W (Weekly), MA_6M/MA_12M (Monthly)
/// </summary>
public class IndicatorCalculationService : IIndicatorCalculationService
{
    /// <summary>
    /// Computes SMA-20 (Simple Moving Average over 20 periods).
    /// Matches Python: df[price_col].rolling(20).mean()
    /// </summary>
    public Dictionary<DateTime, decimal?> ComputeSma20(IEnumerable<PriceDataPoint> dataPoints)
    {
        var sortedData = dataPoints.OrderBy(d => d.DateTime).ToList();
        var result = new Dictionary<DateTime, decimal?>();

        const int period = 20;

        for (int i = 0; i < sortedData.Count; i++)
        {
            if (i < period - 1)
            {
                // Not enough data points yet
                result[sortedData[i].DateTime] = null;
            }
            else
            {
                // Calculate rolling mean over last 20 periods
                var window = sortedData.Skip(i - period + 1).Take(period);
                var average = window.Average(d => d.Price);
                result[sortedData[i].DateTime] = average;
            }
        }

        return result;
    }

    /// <summary>
    /// Computes Bollinger Bands (SMA-20 ± 2 standard deviations).
    /// Matches Python:
    /// - std = df[price_col].rolling(20).std()
    /// - Upper_Band = SMA_20 + 2 * std
    /// - Lower_Band = SMA_20 - 2 * std
    /// </summary>
    public (Dictionary<DateTime, decimal?> UpperBand, Dictionary<DateTime, decimal?> LowerBand) 
        ComputeBollingerBands(IEnumerable<PriceDataPoint> dataPoints, int period = 20, double stdDev = 2.0)
    {
        var sortedData = dataPoints.OrderBy(d => d.DateTime).ToList();
        var sma20 = ComputeSma20(sortedData);
        var upperBand = new Dictionary<DateTime, decimal?>();
        var lowerBand = new Dictionary<DateTime, decimal?>();

        for (int i = 0; i < sortedData.Count; i++)
        {
            var date = sortedData[i].DateTime;
            var sma = sma20[date];

            if (i < period - 1 || !sma.HasValue)
            {
                upperBand[date] = null;
                lowerBand[date] = null;
            }
            else
            {
                // Calculate rolling sample standard deviation (ddof=1, matches pandas rolling().std())
                var window = sortedData.Skip(i - period + 1).Take(period).Select(d => d.Price);
                var mean = (double)sma.Value;
                var variance = window.Sum(v => Math.Pow((double)v - mean, 2)) / (period - 1);
                var std = Math.Sqrt(variance);

                // Upper and lower bands
                upperBand[date] = sma + (decimal)(stdDev * std);
                lowerBand[date] = sma - (decimal)(stdDev * std);
            }
        }

        return (upperBand, lowerBand);
    }

    /// <summary>
    /// Computes interval-specific moving averages.
    /// Matches Python logic:
    /// - Weekly: MA_4W (4-week MA)
    /// - Monthly: MA_6M (6-month MA), MA_12M (12-month MA)
    /// </summary>
    public Dictionary<string, Dictionary<DateTime, decimal?>> ComputeIntervalSpecificMovingAverages(
        IEnumerable<PriceDataPoint> dataPoints, 
        Interval interval)
    {
        var sortedData = dataPoints.OrderBy(d => d.DateTime).ToList();
        var result = new Dictionary<string, Dictionary<DateTime, decimal?>>();

        // Python: ma_windows = {"Weekly": {"MA_4W": 4}, "Monthly": {"MA_6M": 6, "MA_12M": 12}}
        if (interval == Interval.Weekly)
        {
            var ma4W = ComputeMovingAverage(sortedData, 4, "MA_4W");
            result["MA_4W"] = ma4W;
        }
        else if (interval == Interval.Monthly)
        {
            var ma6M = ComputeMovingAverage(sortedData, 6, "MA_6M");
            var ma12M = ComputeMovingAverage(sortedData, 12, "MA_12M");
            result["MA_6M"] = ma6M;
            result["MA_12M"] = ma12M;
        }

        return result;
    }

    /// <summary>
    /// Computes a simple moving average over the given number of periods.
    /// Matches pandas: rolling(period).mean()
    /// </summary>
    public Dictionary<DateTime, decimal?> ComputeSma(IEnumerable<PriceDataPoint> dataPoints, int period)
    {
        return ComputeMovingAverage(dataPoints.OrderBy(d => d.DateTime).ToList(), period, $"SMA_{period}");
    }

    /// <summary>
    /// Computes an exponential moving average seeded with the first price.
    /// Matches pandas: ewm(span=period, adjust=False).mean()
    /// </summary>
    public Dictionary<DateTime, decimal?> ComputeEma(IEnumerable<PriceDataPoint> dataPoints, int period)
    {
        var sortedData = dataPoints.OrderBy(d => d.DateTime).ToList();
        return Ema(sortedData.Select(d => (decimal?)d.Price).ToList(), period)
            .Select((value, i) => (sortedData[i].DateTime, value))
            .ToDictionary(x => x.DateTime, x => x.value);
    }

    /// <summary>
    /// Computes the Relative Strength Index with Wilder's smoothing (standard RSI).
    /// The first value appears at index <paramref name="period"/>, after <paramref name="period"/> price changes.
    /// Above 70 is conventionally overbought, below 30 oversold.
    /// </summary>
    public Dictionary<DateTime, decimal?> ComputeRsi(IEnumerable<PriceDataPoint> dataPoints, int period = 14)
    {
        var sortedData = dataPoints.OrderBy(d => d.DateTime).ToList();
        var result = sortedData.ToDictionary(d => d.DateTime, _ => (decimal?)null);
        if (sortedData.Count <= period)
            return result;

        decimal averageGain = 0, averageLoss = 0;
        for (int i = 1; i <= period; i++)
        {
            var change = sortedData[i].Price - sortedData[i - 1].Price;
            averageGain += Math.Max(change, 0);
            averageLoss += Math.Max(-change, 0);
        }
        averageGain /= period;
        averageLoss /= period;
        result[sortedData[period].DateTime] = Rsi(averageGain, averageLoss);

        for (int i = period + 1; i < sortedData.Count; i++)
        {
            var change = sortedData[i].Price - sortedData[i - 1].Price;
            averageGain = (averageGain * (period - 1) + Math.Max(change, 0)) / period;
            averageLoss = (averageLoss * (period - 1) + Math.Max(-change, 0)) / period;
            result[sortedData[i].DateTime] = Rsi(averageGain, averageLoss);
        }

        return result;
    }

    /// <summary>
    /// Computes MACD: the fast EMA minus the slow EMA, its signal-line EMA, and the histogram (MACD minus signal).
    /// Early values are left empty until the slow EMA (and then the signal line) have a full window of data.
    /// </summary>
    public (Dictionary<DateTime, decimal?> Macd, Dictionary<DateTime, decimal?> Signal, Dictionary<DateTime, decimal?> Histogram)
        ComputeMacd(IEnumerable<PriceDataPoint> dataPoints, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
    {
        var sortedData = dataPoints.OrderBy(d => d.DateTime).ToList();
        var prices = sortedData.Select(d => (decimal?)d.Price).ToList();

        var fast = Ema(prices, fastPeriod);
        var slow = Ema(prices, slowPeriod);
        var macd = prices.Select((_, i) => i >= slowPeriod - 1 ? fast[i] - slow[i] : null).ToList();
        var signal = Ema(macd, signalPeriod);

        var macdResult = new Dictionary<DateTime, decimal?>();
        var signalResult = new Dictionary<DateTime, decimal?>();
        var histogramResult = new Dictionary<DateTime, decimal?>();

        for (int i = 0; i < sortedData.Count; i++)
        {
            var date = sortedData[i].DateTime;
            var signalReady = i >= slowPeriod + signalPeriod - 2;
            macdResult[date] = macd[i];
            signalResult[date] = signalReady ? signal[i] : null;
            histogramResult[date] = signalReady ? macd[i] - signal[i] : null;
        }

        return (macdResult, signalResult, histogramResult);
    }

    /// <summary>
    /// EMA over a series that may start with empty values; seeded with the first non-empty value
    /// (pandas ewm(adjust=False) behaviour).
    /// </summary>
    private static List<decimal?> Ema(IList<decimal?> values, int period)
    {
        var alpha = 2m / (period + 1);
        var result = new List<decimal?>(values.Count);
        decimal? previous = null;

        foreach (var value in values)
        {
            if (value.HasValue)
                previous = previous.HasValue ? alpha * value.Value + (1 - alpha) * previous.Value : value.Value;
            result.Add(value.HasValue ? previous : null);
        }

        return result;
    }

    private static decimal Rsi(decimal averageGain, decimal averageLoss)
    {
        if (averageLoss == 0)
            return averageGain == 0 ? 50 : 100;
        var relativeStrength = averageGain / averageLoss;
        return 100 - 100 / (1 + relativeStrength);
    }

    /// <summary>
    /// Helper method to compute a simple moving average with specified period.
    /// Matches Python: df[price_col].rolling(window).mean()
    /// </summary>
    private Dictionary<DateTime, decimal?> ComputeMovingAverage(
        IList<PriceDataPoint> sortedData, 
        int period, 
        string label)
    {
        var result = new Dictionary<DateTime, decimal?>(sortedData.Count);

        // A running total: add the newest price and drop the one leaving the window. Decimal sums are exact,
        // so this gives the same averages as summing each window, without re-adding 200 prices per day
        var sum = 0m;
        for (int i = 0; i < sortedData.Count; i++)
        {
            sum += sortedData[i].Price;
            if (i >= period)
                sum -= sortedData[i - period].Price;

            result[sortedData[i].DateTime] = i < period - 1 ? null : sum / period;
        }

        return result;
    }
}