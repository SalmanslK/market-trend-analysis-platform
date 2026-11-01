using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Interfaces.Services;

/// <summary>
/// Service interface for calculating technical indicators.
/// Matches Python indicator computation logic exactly.
/// </summary>
public interface IIndicatorCalculationService
{
    /// <summary>
    /// Computes SMA-20 (Simple Moving Average over 20 periods).
    /// </summary>
    Dictionary<DateTime, decimal?> ComputeSma20(IEnumerable<PriceDataPoint> dataPoints);
    
    /// <summary>
    /// Computes Bollinger Bands (SMA-20 ± 2 standard deviations).
    /// </summary>
    (Dictionary<DateTime, decimal?> UpperBand, Dictionary<DateTime, decimal?> LowerBand) ComputeBollingerBands(
        IEnumerable<PriceDataPoint> dataPoints, 
        int period = 20, 
        double stdDev = 2.0);
    
    /// <summary>
    /// Computes interval-specific moving averages.
    /// Weekly: MA_4W (4-week MA)
    /// Monthly: MA_6M (6-month MA), MA_12M (12-month MA)
    /// </summary>
    Dictionary<string, Dictionary<DateTime, decimal?>> ComputeIntervalSpecificMovingAverages(
        IEnumerable<PriceDataPoint> dataPoints,
        Interval interval);

    /// <summary>
    /// Computes a simple moving average over the given number of periods.
    /// </summary>
    Dictionary<DateTime, decimal?> ComputeSma(IEnumerable<PriceDataPoint> dataPoints, int period);

    /// <summary>
    /// Computes an exponential moving average (pandas ewm(span, adjust=False)).
    /// </summary>
    Dictionary<DateTime, decimal?> ComputeEma(IEnumerable<PriceDataPoint> dataPoints, int period);

    /// <summary>
    /// Computes the Relative Strength Index with Wilder's smoothing.
    /// </summary>
    Dictionary<DateTime, decimal?> ComputeRsi(IEnumerable<PriceDataPoint> dataPoints, int period = 14);

    /// <summary>
    /// Computes MACD line, signal line and histogram.
    /// </summary>
    (Dictionary<DateTime, decimal?> Macd, Dictionary<DateTime, decimal?> Signal, Dictionary<DateTime, decimal?> Histogram)
        ComputeMacd(IEnumerable<PriceDataPoint> dataPoints, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9);
}