using System.Reflection;
using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Domain.ValueObjects;
using MarketAnalytics.Infrastructure.Data;
using MarketAnalytics.Infrastructure.Migrations;
using MarketAnalytics.Infrastructure.Repositories;
using MarketAnalytics.Infrastructure.Updates;
using MarketAnalytics.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketAnalytics.Tests;

/// <summary>
/// Checks that guard against quiet mistakes: hand-entered payout data, which instruments get the cash check, and a
/// recording failure stopping the Worker.
/// </summary>
public class SafetyNetTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(typeof(AddEarlyMifPayouts))]
    [InlineData(typeof(AddEarlyMcfPayouts))]
    public void PayoutMigrations_ListPositiveAmountsOnceEachInDateOrder(Type migration)
    {
        var field = migration.GetField("Payouts", BindingFlags.NonPublic | BindingFlags.Static)!;
        var payouts = ((string Date, decimal Amount)[])field.GetValue(null)!;
        var dates = payouts.Select(p => DateTime.Parse(p.Date)).ToList();

        Assert.NotEmpty(payouts);
        Assert.All(payouts, p => Assert.True(p.Amount > 0));
        Assert.Equal(dates.OrderBy(d => d), dates);
        Assert.Equal(dates.Count, dates.Distinct().Count());
    }

    [Fact]
    public void PayoutAdjuster_MatchesAPayoutListedOnAWeekendToTheNextTradingDaysDrop()
    {
        // Friday 100, then the NAV drops by the payout on Monday; the payout is listed on Saturday
        var friday = new DateTime(2025, 6, 27, 0, 0, 0, DateTimeKind.Utc);
        var prices = new List<PriceDataPoint>
        {
            new(friday.AddDays(-1), 99), new(friday, 100), new(friday.AddDays(3), 90), new(friday.AddDays(4), 91),
        };

        var matches = PayoutAdjuster.MatchExDates(prices, new[] { new FundPayout { PayoutDate = friday.AddDays(1), AmountPerUnit = 10 } });

        Assert.Equal(2, Assert.Single(matches).Index);
    }

    private static List<PricePoint> Daily(int instrumentId, int days, decimal daily) =>
        Enumerable.Range(0, days).Select(i => new PricePoint
        {
            InstrumentId = instrumentId,
            DateTime = Start.AddDays(i),
            Price = 100m * (decimal)Math.Pow(1 + (double)daily, i),
        }).ToList();

    private static async Task<List<InstrumentAnalyticsDto>> AnalyticsFor(bool withCashFund)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Instruments.AddRange(
            new Instrument { Id = 1, Symbol = "KSE100", Type = InstrumentType.StockIndex },
            new Instrument { Id = 2, Symbol = "GOLDINT", Type = InstrumentType.Commodity },
            new Instrument { Id = 3, Symbol = "USDPKR", Type = InstrumentType.Forex });
        db.PricePoints.AddRange(Daily(1, 500, 0.001m).Concat(Daily(2, 500, 0.001m)).Concat(Daily(3, 500, 0.0001m)));
        if (withCashFund)
        {
            db.Instruments.Add(new Instrument { Id = 4, Symbol = StrongerThanCash.CashSymbol, Type = InstrumentType.MutualFund, LowRiskKind = "Cash fund" });
            db.PricePoints.AddRange(Daily(4, 500, 0.0003m));
        }
        await db.SaveChangesAsync();

        var service = new MarketAnalyticsService(
            new InstrumentRepository(db), new PricePointRepository(db), new IndicatorCalculationService(),
            NullLogger<MarketAnalyticsService>.Instance, fundPayoutRepository: new FundPayoutRepository(db));
        return (await service.GetAllAnalyticsAsync()).ToList();
    }

    [Fact]
    public async Task Analytics_GiveTheCashCheckOnlyToRupeePricedInstrumentsWithSignals()
    {
        var analytics = await AnalyticsFor(withCashFund: true);

        Assert.NotNull(analytics.Single(a => a.Symbol == "KSE100").StrongerThanCash);
        Assert.Null(analytics.Single(a => a.Symbol == "KSE100").StrongerThanCashUnavailable);
        foreach (var symbol in new[] { "GOLDINT", "USDPKR", StrongerThanCash.CashSymbol })
        {
            Assert.Null(analytics.Single(a => a.Symbol == symbol).StrongerThanCash);
            Assert.Null(analytics.Single(a => a.Symbol == symbol).StrongerThanCashUnavailable);
        }
    }

    [Fact]
    public async Task Analytics_SayWhyTheCashCheckIsMissingWhenTheCashFundIs()
    {
        var kse = (await AnalyticsFor(withCashFund: false)).Single(a => a.Symbol == "KSE100");

        Assert.Null(kse.StrongerThanCash);
        Assert.Contains("Meezan Cash Fund", kse.StrongerThanCashUnavailable);
    }

    private sealed class ThrowingAnalytics(Exception error) : IMarketAnalyticsService
    {
        public Task<IEnumerable<InstrumentAnalyticsDto>> GetAllAnalyticsAsync(CancellationToken cancellationToken = default) => throw error;
        public Task<InstrumentAnalyticsDto?> GetInstrumentAnalyticsAsync(string symbol, CancellationToken cancellationToken = default) => throw error;
        public InstrumentAnalyticsDto ComputeAnalytics(IReadOnlyList<PriceDataPoint> dailyPrices, IReadOnlyList<PriceDataPoint>? benchmarkPrices = null) => throw error;
    }

    private static SignalRecorder FailingRecorder(Exception error) =>
        new(null!, new ThrowingAnalytics(error), null!, null!, NullLogger<SignalRecorder>.Instance);

    [Fact]
    public async Task Worker_KeepsGoingWhenRecordingSignalsFails()
    {
        // Logged and swallowed, so the next price update still runs
        await DataUpdateBackgroundService.RecordSignalsAsync(FailingRecorder(new InvalidOperationException("database down")), NullLogger.Instance, CancellationToken.None);
    }

    [Fact]
    public async Task Worker_StillStopsWhenShuttingDown()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            DataUpdateBackgroundService.RecordSignalsAsync(FailingRecorder(new OperationCanceledException()), NullLogger.Instance, CancellationToken.None));
    }
}
