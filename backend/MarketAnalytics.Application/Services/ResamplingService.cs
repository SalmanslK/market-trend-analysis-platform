using System.Globalization;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Service for resampling price data by interval.
/// Matches Python logic: pandas resample(rule).last() - takes last value per period.
/// </summary>
public class ResamplingService : IResamplingService
{
    /// <summary>
    /// Resamples price data points by the specified interval.
    /// For each interval period, takes the last price value (matches Python resample().last()).
    /// </summary>
    public IEnumerable<PriceDataPoint> ResamplePriceData(IEnumerable<PriceDataPoint> dataPoints, Interval interval)
    {
        var sortedData = dataPoints.OrderBy(d => d.DateTime).ToList();
        
        if (!sortedData.Any())
            return Enumerable.Empty<PriceDataPoint>();

        // Group by interval period and take the last value in each group
        return interval switch
        {
            Interval.Daily => sortedData, // No resampling needed
            Interval.Weekly => ResampleWeekly(sortedData),
            Interval.Monthly => ResampleMonthly(sortedData),
            Interval.Quarterly => ResampleQuarterly(sortedData),
            Interval.Yearly => ResampleYearly(sortedData),
            _ => sortedData
        };
    }

    private IEnumerable<PriceDataPoint> ResampleWeekly(IList<PriceDataPoint> dataPoints)
    {
        // Group by ISO week (Mon-Sun, matches pandas "W") and take last value per week.
        // Use the ISO week-numbering year, not the calendar year, so weeks spanning New Year stay together.
        return dataPoints
            .GroupBy(dp => new { Year = ISOWeek.GetYear(dp.DateTime), Week = ISOWeek.GetWeekOfYear(dp.DateTime) })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Week)
            .Select(g => g.Last());
    }

    private IEnumerable<PriceDataPoint> ResampleMonthly(IList<PriceDataPoint> dataPoints)
    {
        // Group by month and take last value per month
        return dataPoints
            .GroupBy(dp => new { dp.DateTime.Year, dp.DateTime.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Select(g => g.Last());
    }

    private IEnumerable<PriceDataPoint> ResampleQuarterly(IList<PriceDataPoint> dataPoints)
    {
        // Group by quarter and take last value per quarter
        return dataPoints
            .GroupBy(dp => new { dp.DateTime.Year, Quarter = (dp.DateTime.Month - 1) / 3 + 1 })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Quarter)
            .Select(g => g.Last());
    }

    private IEnumerable<PriceDataPoint> ResampleYearly(IList<PriceDataPoint> dataPoints)
    {
        // Group by year and take last value per year
        return dataPoints
            .GroupBy(dp => dp.DateTime.Year)
            .OrderBy(g => g.Key)
            .Select(g => g.Last());
    }
}