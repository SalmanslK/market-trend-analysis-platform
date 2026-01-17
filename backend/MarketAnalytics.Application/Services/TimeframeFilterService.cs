using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Service for filtering price data by timeframe.
/// Matches Python logic: filters data from last date backwards using date offsets.
/// </summary>
public class TimeframeFilterService : ITimeframeFilterService
{
    /// <summary>
    /// Filters price data points by the specified timeframe.
    /// </summary>
    public IEnumerable<PriceDataPoint> FilterByTimeframe(IEnumerable<PriceDataPoint> dataPoints, Timeframe timeframe)
    {
        var sortedData = dataPoints.OrderBy(d => d.DateTime).ToList();
        
        if (!sortedData.Any() || timeframe == Timeframe.All)
            return sortedData;

        var lastDate = sortedData.Last().DateTime;
        var startDate = CalculateStartDate(lastDate, timeframe);

        return sortedData.Where(dp => dp.DateTime >= startDate && dp.DateTime <= lastDate);
    }

    /// <summary>
    /// Calculates the start date based on the last date and timeframe.
    /// Matches Python logic: offsets["timeframe"] and YTD special handling.
    /// </summary>
    private DateTime CalculateStartDate(DateTime lastDate, Timeframe timeframe)
    {
        return timeframe switch
        {
            Timeframe.Day => lastDate.AddDays(-1),
            Timeframe.Week => lastDate.AddDays(-7),
            Timeframe.Month => lastDate.AddMonths(-1),
            Timeframe.Quarter => lastDate.AddMonths(-3),
            Timeframe.HalfYear => lastDate.AddMonths(-6),
            Timeframe.Year => lastDate.AddYears(-1),
            Timeframe.Ytd => new DateTime(lastDate.Year, 1, 1), // Year-to-date: January 1st of current year
            Timeframe.All => DateTime.MinValue,
            _ => lastDate
        };
    }
}