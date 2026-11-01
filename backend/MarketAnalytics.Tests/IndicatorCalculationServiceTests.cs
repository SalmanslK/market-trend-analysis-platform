using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Tests;

public class IndicatorCalculationServiceTests
{
    private readonly IndicatorCalculationService _service = new();

    private static List<PriceDataPoint> Series(params decimal[] prices) =>
        prices.Select((p, i) => new PriceDataPoint(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i), p)).ToList();

    [Fact]
    public void BollingerBands_UseSampleStandardDeviation_LikePandas()
    {
        // Prices 1..20: SMA 10.5, sample std (ddof=1) sqrt(35)
        var points = Series(Enumerable.Range(1, 20).Select(i => (decimal)i).ToArray());

        var (upper, lower) = _service.ComputeBollingerBands(points);

        var last = points[^1].DateTime;
        Assert.Equal(10.5 + 2 * Math.Sqrt(35), (double)upper[last]!.Value, 6);
        Assert.Equal(10.5 - 2 * Math.Sqrt(35), (double)lower[last]!.Value, 6);
        Assert.Null(upper[points[18].DateTime]);
    }

    [Fact]
    public void Ema_IsSeededWithFirstValue_LikePandasAdjustFalse()
    {
        // Period 3 -> alpha 0.5: 1, 0.5*2 + 0.5*1 = 1.5, 0.5*3 + 0.5*1.5 = 2.25
        var points = Series(1, 2, 3);

        var ema = _service.ComputeEma(points, 3);

        Assert.Equal(new decimal?[] { 1m, 1.5m, 2.25m }, points.Select(p => ema[p.DateTime]).ToArray());
    }

    [Fact]
    public void Rsi_UsesWildersSmoothing()
    {
        // Period 2, changes +1, -1, +1:
        // first averages gain 0.5 / loss 0.5 -> RSI 50
        // then gain (0.5*1 + 1)/2 = 0.75, loss (0.5*1 + 0)/2 = 0.25 -> RS 3 -> RSI 75
        var points = Series(1, 2, 1, 2);

        var rsi = _service.ComputeRsi(points, 2);

        Assert.Null(rsi[points[1].DateTime]);
        Assert.Equal(50m, rsi[points[2].DateTime]);
        Assert.Equal(75m, rsi[points[3].DateTime]);
    }

    [Fact]
    public void Rsi_IsHundred_WhenPricesOnlyRise()
    {
        var points = Series(Enumerable.Range(1, 30).Select(i => (decimal)i).ToArray());

        Assert.Equal(100m, _service.ComputeRsi(points)[points[^1].DateTime]);
    }

    [Fact]
    public void Macd_IsZero_ForFlatPrices_AndEmptyUntilWindowsFill()
    {
        var points = Series(Enumerable.Repeat(100m, 40).ToArray());

        var (macd, signal, histogram) = _service.ComputeMacd(points);

        // MACD needs the 26-period slow EMA; the signal line needs 9 more periods of MACD
        Assert.Null(macd[points[24].DateTime]);
        Assert.Equal(0m, macd[points[25].DateTime]);
        Assert.Null(signal[points[32].DateTime]);
        Assert.Equal(0m, signal[points[33].DateTime]);
        Assert.Equal(0m, histogram[points[^1].DateTime]);
    }

    [Fact]
    public void Sma_AveragesTheLastPeriodValues()
    {
        var points = Series(1, 2, 3, 4, 5);

        var sma = _service.ComputeSma(points, 3);

        Assert.Null(sma[points[1].DateTime]);
        Assert.Equal(4m, sma[points[^1].DateTime]);
    }

    [Fact]
    public void WeeklyResampling_KeepsWeeksSpanningNewYearTogether()
    {
        // Mon 2024-12-30 .. Fri 2025-01-03 is one ISO week; Jan 2-3 2024 is a different week
        var dates = new[] { new DateTime(2024, 1, 2), new DateTime(2024, 1, 3), new DateTime(2024, 12, 30), new DateTime(2024, 12, 31), new DateTime(2025, 1, 3) };
        var points = dates.Select((d, i) => new PriceDataPoint(DateTime.SpecifyKind(d, DateTimeKind.Utc), i + 1)).ToList();

        var weekly = new ResamplingService().ResamplePriceData(points, Interval.Weekly).ToList();

        Assert.Equal(new decimal[] { 2, 5 }, weekly.Select(p => p.Price).ToArray());
    }
}
