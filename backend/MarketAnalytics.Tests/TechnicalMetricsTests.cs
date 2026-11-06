using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Tests;

public class TechnicalMetricsTests
{
    private static readonly IndicatorCalculationService Indicators = new();
    private static readonly DateTime Start = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<PriceDataPoint> Daily(IEnumerable<double> prices) =>
        prices.Select((p, i) => new PriceDataPoint(Start.AddDays(i), (decimal)p)).ToList();

    private static List<PriceDataPoint> Rising(int count) => Daily(Enumerable.Range(1, count).Select(i => (double)i));

    // A repeatable wobble with real ups and downs
    private static List<PriceDataPoint> Wavy(int count) =>
        Daily(Enumerable.Range(0, count).Select(i => 100 + 20 * Math.Sin(i / 9.0) + 5 * Math.Sin(i / 2.0)));

    private static MetricDto Metric(List<MetricDto> metrics, string name) => metrics.Single(m => m.Name == name);
    private static double Value(List<MetricDto> metrics, string name) => (double)Metric(metrics, name).Value;

    [Fact]
    public void Stochastic_And_WilliamsR_AreAtTheirExtremes_AtANewHigh()
    {
        var metrics = TechnicalMetrics.Compute(Rising(30), Indicators);

        Assert.Equal(100, Value(metrics, "Stochastic %K (14, closes)"), 8);
        Assert.Equal(0, Value(metrics, "Williams %R (14, closes)"), 8);
        Assert.Equal("Negative", Metric(metrics, "Stochastic %K (14, closes)").Reading);
    }

    [Fact]
    public void Stochastic_MatchesAHandCalculation()
    {
        var closes = Enumerable.Range(1, 14).Select(i => (double)i).Append(8).ToList();
        var metrics = TechnicalMetrics.Compute(Daily(closes), Indicators);

        // The 14-period window is 2..14 plus 8, so the low is 2 and the high 14
        Assert.Equal((8.0 - 2) / (14 - 2) * 100, Value(metrics, "Stochastic %K (14, closes)"), 8);
    }

    [Fact]
    public void RateOfChange_And_Momentum_MatchAHandCalculation()
    {
        var metrics = TechnicalMetrics.Compute(Rising(30), Indicators);

        Assert.Equal((30.0 / 18 - 1) * 100, Value(metrics, "Rate of change (12)"), 8);
        Assert.Equal(10, Value(metrics, "Momentum (10)"), 8);
    }

    [Fact]
    public void Cci_MatchesAHandCalculation()
    {
        // Closes 1..20: mean 10.5, mean deviation 5, (20 - 10.5) / (0.015 * 5)
        var metrics = TechnicalMetrics.Compute(Rising(20), Indicators);

        Assert.Equal(9.5 / (0.015 * 5), Value(metrics, "CCI (20, closes)"), 6);
    }

    [Fact]
    public void Donchian_And_Bandwidth_MatchAHandCalculation()
    {
        // Closes 1..20: SMA 10.5, sample std sqrt(35), bandwidth 4 * std / SMA
        var metrics = TechnicalMetrics.Compute(Rising(20), Indicators);

        Assert.Equal(20, Value(metrics, "Donchian high (20, closes)"), 8);
        Assert.Equal(1, Value(metrics, "Donchian low (20, closes)"), 8);
        Assert.Equal(4 * Math.Sqrt(35) / 10.5 * 100, Value(metrics, "Bollinger bandwidth"), 6);
    }

    [Fact]
    public void MovingAverages_ReadAgainstThePrice()
    {
        var metrics = TechnicalMetrics.Compute(Rising(250), Indicators);

        var sma200 = Metric(metrics, "SMA 200");
        Assert.Equal(150.5, (double)sma200.Value, 8);
        Assert.Equal("Positive", sma200.Reading);
        Assert.Equal(10, metrics.Count(m => m.Group == TechnicalMetrics.MovingAverages));
    }

    [Fact]
    public void Metrics_WithoutEnoughHistory_AreLeftOut()
    {
        var metrics = TechnicalMetrics.Compute(Rising(15), Indicators);

        Assert.Contains(metrics, m => m.Name == "SMA 10");
        Assert.DoesNotContain(metrics, m => m.Name == "SMA 20");
        Assert.DoesNotContain(metrics, m => m.Name == "MACD (12, 26)");
        Assert.DoesNotContain(metrics, m => m.Name == "Sharpe ratio (1 year)");
        Assert.DoesNotContain(metrics, m => m.Name == "CAGR (1 year)");
    }

    [Fact]
    public void Cagr_MatchesAHandCalculation()
    {
        // 100 doubles to 200 over exactly 3 years
        var days = (int)(Start.AddYears(3) - Start).TotalDays;
        var prices = Daily(Enumerable.Range(0, days + 1).Select(i => 100 + 100.0 * i / days));

        var metrics = TechnicalMetrics.Compute(prices, Indicators);

        Assert.Equal((Math.Pow(2, 1.0 / 3) - 1) * 100, Value(metrics, "CAGR (3 years)"), 6);
        Assert.DoesNotContain(metrics, m => m.Name == "CAGR (5 years)");
    }

    [Fact]
    public void Drawdown_MatchesAHandCalculation()
    {
        var metrics = TechnicalMetrics.Compute(Daily(new double[] { 100, 120, 90, 110 }), Indicators);

        Assert.Equal(-25, Value(metrics, "Max drawdown (all history)"), 8);
    }

    [Fact]
    public void Sharpe_And_Sortino_UseTheAssumedCashReturn()
    {
        var prices = Wavy(500);
        var metrics = TechnicalMetrics.Compute(prices, Indicators);

        var yearStart = prices.Last(p => p.DateTime <= prices[^1].DateTime.AddYears(-1));
        var growth = ((double)prices[^1].Price / (double)yearStart.Price - 1) * 100;
        var expected = (growth - TechnicalMetrics.AssumedRiskFreePercent) / Value(metrics, "Volatility (1 year)");

        Assert.Equal(expected, Value(metrics, "Sharpe ratio (1 year)"), 6);
        Assert.Contains(metrics, m => m.Name == "Sortino ratio (1 year)");
    }

    [Fact]
    public void ValueAtRisk_And_ExtremePeriods_AreOrdered()
    {
        var metrics = TechnicalMetrics.Compute(Wavy(500), Indicators);

        var worst = Value(metrics, "Worst period (1 year)");
        var valueAtRisk = Value(metrics, "Value at Risk (95%, 1 period)");
        var best = Value(metrics, "Best period (1 year)");
        Assert.True(worst <= valueAtRisk && valueAtRisk < 0 && best > 0);
        Assert.InRange(Value(metrics, "Periods up (1 year)"), 0, 100);
    }

    [Fact]
    public void Beta_IsOne_AgainstItself_AndTwo_AgainstHalfTheMoves()
    {
        var prices = Wavy(500);
        var same = TechnicalMetrics.Compute(prices, Indicators, prices);

        Assert.Equal(1, Value(same, "Beta vs KSE-100 (1 year)"), 8);
        Assert.Equal(1, Value(same, "Correlation vs KSE-100 (1 year)"), 8);

        // Benchmark returns are exactly half the instrument's on every day
        var doubled = new List<PriceDataPoint> { prices[0] };
        var halved = new List<PriceDataPoint> { prices[0] };
        for (int i = 1; i < prices.Count; i++)
        {
            var move = 0.01 * Math.Sin(i / 3.0);
            doubled.Add(new PriceDataPoint(prices[i].DateTime, (decimal)((double)doubled[^1].Price * (1 + 2 * move))));
            halved.Add(new PriceDataPoint(prices[i].DateTime, (decimal)((double)halved[^1].Price * (1 + move))));
        }

        var metrics = TechnicalMetrics.Compute(doubled, Indicators, halved);

        Assert.Equal(2, Value(metrics, "Beta vs KSE-100 (1 year)"), 4);
        Assert.Equal(1, Value(metrics, "Correlation vs KSE-100 (1 year)"), 4);
    }

    [Fact]
    public void BenchmarkMetrics_AreLeftOut_WithoutABenchmark()
    {
        var metrics = TechnicalMetrics.Compute(Wavy(500), Indicators);

        Assert.DoesNotContain(metrics, m => m.Name.StartsWith("Beta"));
    }

    [Fact]
    public void Every_Metric_HasAPlainDescription()
    {
        var metrics = TechnicalMetrics.Compute(Wavy(1500), Indicators, Wavy(1500));

        Assert.True(metrics.Count > 40, $"{metrics.Count} metrics");
        Assert.All(metrics, m => Assert.False(string.IsNullOrWhiteSpace(m.Description), m.Name));
        Assert.All(metrics, m => Assert.False(string.IsNullOrWhiteSpace(m.Note), m.Name));
        Assert.Equal(metrics.Count, metrics.Select(m => m.Name).Distinct().Count());
    }
}
