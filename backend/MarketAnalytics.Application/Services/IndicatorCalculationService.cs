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
                // Calculate rolling standard deviation
                var window = sortedData.Skip(i - period + 1).Take(period).Select(d => d.Price);
                var mean = (double)sma.Value;
                var variance = window.Average(v => Math.Pow((double)v - mean, 2));
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
    /// Helper method to compute a simple moving average with specified period.
    /// Matches Python: df[price_col].rolling(window).mean()
    /// </summary>
    private Dictionary<DateTime, decimal?> ComputeMovingAverage(
        IList<PriceDataPoint> sortedData, 
        int period, 
        string label)
    {
        var result = new Dictionary<DateTime, decimal?>();

        for (int i = 0; i < sortedData.Count; i++)
        {
            if (i < period - 1)
            {
                result[sortedData[i].DateTime] = null;
            }
            else
            {
                var window = sortedData.Skip(i - period + 1).Take(period);
                var average = window.Average(d => d.Price);
                result[sortedData[i].DateTime] = average;
            }
        }

        return result;
    }
}