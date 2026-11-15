using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Domain.ValueObjects;
using MarketAnalytics.Infrastructure.Data;
using MarketAnalytics.Infrastructure.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketAnalytics.Tests;

public class ForwardRecordTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // One price per day from Start, growing by `daily` (a fraction) each day
    private static List<PriceDataPoint> Growing(int days, decimal daily, decimal from = 100m)
    {
        var result = new List<PriceDataPoint>();
        var price = from;
        for (int i = 0; i < days; i++)
        {
            result.Add(new PriceDataPoint(Start.AddDays(i), price));
            price *= 1 + daily;
        }
        return result;
    }

    [Fact]
    public void StrongerThanCash_ChecksOnTheFirstDayOfEachMonthOnceThereAre12Months()
    {
        var checks = StrongerThanCash.MonthlyChecks(Growing(500, 0.001m), Growing(500, 0.0003m));

        Assert.All(checks, c => Assert.Equal(1, c.Date.Day));
        Assert.Equal(new DateTime(2025, 1, 1), checks[0].Date.Date);
        Assert.All(checks, c => Assert.True(c.AheadOfCash));
    }

    [Fact]
    public void StrongerThanCash_SaysBehindCashAndSinceWhenAfterAFall()
    {
        // Rises for a year and a half, then falls hard: the 12-month return drops below cash's
        var prices = Growing(550, 0.001m);
        var price = prices[^1].Price;
        for (int i = 550; i < 800; i++)
        {
            price *= 0.997m;
            prices.Add(new PriceDataPoint(Start.AddDays(i), price));
        }

        var result = StrongerThanCash.Evaluate(prices, Growing(800, 0.0003m));

        Assert.NotNull(result);
        Assert.False(result!.AheadOfCash);
        Assert.True(result.Return12M < result.CashReturn12M);
        Assert.NotNull(result.Since);
        Assert.Equal(1, result.Since!.Value.Day);
    }

    [Fact]
    public void StrongerThanCash_IsNullWithLessThanAYear()
    {
        Assert.Null(StrongerThanCash.Evaluate(Growing(300, 0.001m), Growing(300, 0.0003m)));
    }

    [Fact]
    public void Summary_ScoresOnlyReadingsWhoseNext20DaysAreKnown()
    {
        var prices = Growing(40, 0.01m);
        var snapshots = new[]
        {
            new SignalSnapshot { InstrumentId = 1, Date = Start.AddDays(5), DailyAction = "Buy", SteadyAction = "StrongBuy", CombinedAction = "Hold", AheadOfCash = true },
            new SignalSnapshot { InstrumentId = 1, Date = Start.AddDays(30), DailyAction = "Sell", CombinedAction = "Sell", AheadOfCash = false },
        };

        var summary = SignalRecordSummary.Summarize(snapshots, new Dictionary<int, List<PriceDataPoint>> { [1] = prices });

        Assert.Equal(2, summary.Readings);
        Assert.Equal(1, summary.Scored);
        Assert.Equal(Start.AddDays(5), summary.Since);
        var buy = summary.Groups.Single(g => g.Signal == "Technical signal (steady)" && g.Reading == "Buy");
        Assert.Equal(1, buy.Count);
        // From the next close (day 6) to 20 days later (day 26): 1.01^20 - 1
        Assert.Equal(Math.Round(((decimal)Math.Pow(1.01, 20) - 1) * 100, 2), buy.AverageMove);
        Assert.Equal(100, buy.ShareUp);
        Assert.Equal(0, summary.Groups.Single(g => g.Reading == "Behind cash").Count);
        Assert.Null(summary.Groups.Single(g => g.Reading == "Behind cash").AverageMove);
    }

    [Theory]
    [InlineData("StrongBuy", "Buy")]
    [InlineData("Buy", "Buy")]
    [InlineData("Hold", "Hold")]
    [InlineData("StrongSell", "Sell")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("Unknown", null)]
    public void Summary_GroupsStrongAndPlainSignalsTogetherAndSkipsUnknownOnes(string? action, string? side)
    {
        Assert.Equal(side, SignalRecordSummary.Side(action));
    }

    [Fact]
    public void Summary_ScoresAReadingOnlyOnceAll20DaysFromTheNextCloseAreKnown()
    {
        // 40 prices: a reading at index 18 has prices up to index 39 (next close 19, plus 20); one at 19 doesn't
        var prices = Growing(40, 0.01m);
        var snapshots = new[]
        {
            new SignalSnapshot { InstrumentId = 1, Date = Start.AddDays(18), SteadyAction = "Buy", CombinedAction = "Hold" },
            new SignalSnapshot { InstrumentId = 1, Date = Start.AddDays(19), SteadyAction = "Buy", CombinedAction = "Hold" },
        };

        var summary = SignalRecordSummary.Summarize(snapshots, new Dictionary<int, List<PriceDataPoint>> { [1] = prices });

        Assert.Equal(1, summary.Scored);
    }

    [Fact]
    public void Summary_CountsOnlyRealSteadyReadingsAndKnownCashAnswers()
    {
        var prices = Growing(40, 0.01m);
        var snapshots = new[]
        {
            // No steady signal (too little history) and no cash answer: counted in neither group
            new SignalSnapshot { InstrumentId = 1, Date = Start.AddDays(2), DailyAction = "Sell", SteadyAction = null, CombinedAction = "Hold", AheadOfCash = null },
            // A date with no price (e.g. a holiday) can't be scored
            new SignalSnapshot { InstrumentId = 1, Date = Start.AddDays(100), SteadyAction = "Buy", CombinedAction = "Hold" },
            // An instrument with no prices can't be scored
            new SignalSnapshot { InstrumentId = 2, Date = Start.AddDays(2), SteadyAction = "Buy", CombinedAction = "Hold" },
        };

        var summary = SignalRecordSummary.Summarize(snapshots, new Dictionary<int, List<PriceDataPoint>> { [1] = prices });

        Assert.Equal(3, summary.Readings);
        Assert.Equal(1, summary.Scored);
        Assert.All(summary.Groups.Where(g => g.Signal == SignalRecordSummary.Steady), g => Assert.Equal(0, g.Count));
        Assert.All(summary.Groups.Where(g => g.Signal == SignalRecordSummary.VersusCash), g => Assert.Equal(0, g.Count));
        Assert.Equal(1, summary.Groups.Single(g => g.Signal == SignalRecordSummary.Combined && g.Reading == "Hold").Count);
    }

    [Fact]
    public void StrongerThanCash_SkipsMonthsWhenTheCashPriceStoppedUpdating()
    {
        // Cash prices stop after 400 days; later checks would compare against an old cash price, so there are none
        var checks = StrongerThanCash.MonthlyChecks(Growing(700, 0.001m), Growing(400, 0.0003m));

        Assert.NotEmpty(checks);
        Assert.All(checks, c => Assert.True(c.Date <= Start.AddDays(400 + StrongerThanCash.MaxPriceAgeDays)));
    }

    [Fact]
    public void StrongerThanCash_ATieCountsAsBehindCashAndAnAnswerThatNeverChangedHasNoSince()
    {
        var same = Growing(500, 0.001m);

        var result = StrongerThanCash.Evaluate(same, same);

        Assert.NotNull(result);
        Assert.False(result!.AheadOfCash);
        Assert.Null(result.Since);
    }

    [Theory]
    [InlineData("KSE100", InstrumentType.StockIndex, null, true)]
    [InlineData("GOLDPK", InstrumentType.Commodity, null, true)]
    [InlineData("GOLDINT", InstrumentType.Commodity, null, false)]
    [InlineData("USDPKR", InstrumentType.Forex, null, false)]
    [InlineData("MCF", InstrumentType.MutualFund, "Cash fund", false)]
    public void StrongerThanCash_AppliesOnlyToRupeePricedInstrumentsWithSignals(string symbol, InstrumentType type, string? lowRiskKind, bool applies)
    {
        Assert.Equal(applies, StrongerThanCash.AppliesTo(new Instrument { Symbol = symbol, Type = type, LowRiskKind = lowRiskKind }));
    }

    private static ApplicationDbContext InMemoryDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static SignalRecorder Recorder(ApplicationDbContext db, params InstrumentAnalyticsDto[] analytics) =>
        new(db, new FakeAnalytics(analytics), null!, null!, NullLogger<SignalRecorder>.Instance);

    private static InstrumentAnalyticsDto Reading(string symbol, DateTime date, string action, bool isCash = false) => new()
    {
        Symbol = symbol,
        LastDate = date,
        LastPrice = 100,
        IsCash = isCash,
        Signal = new TechnicalSignalDto { Action = action },
        CombinedSignal = new ScaledSignalDto { Action = action },
    };

    [Fact]
    public async Task Recorder_UpdatesTheLatestReadingButNeverWritesAnOlderDate()
    {
        using var db = InMemoryDb();
        db.Instruments.AddRange(new Instrument { Id = 1, Symbol = "KSE100" }, new Instrument { Id = 2, Symbol = "MCF" });
        await db.SaveChangesAsync();
        var day = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);

        // Saves a reading for the latest date, but not for cash funds
        Assert.Equal(1, await Recorder(db, Reading("KSE100", day, "Buy"), Reading("MCF", day, "Hold", isCash: true)).RecordAsync());
        // The same date again updates that reading instead of adding one
        Assert.Equal(1, await Recorder(db, Reading("KSE100", day, "Sell")).RecordAsync());
        // A date older than the saved one (e.g. the latest price was removed) is neither added nor changed
        Assert.Equal(0, await Recorder(db, Reading("KSE100", day.AddDays(-1), "Hold")).RecordAsync());

        var saved = Assert.Single(db.SignalSnapshots);
        Assert.Equal(day, saved.Date);
        Assert.Equal("Sell", saved.DailyAction);
    }

    [Fact]
    public async Task Recorder_LeavesAnOlderReadingAloneWhenTheLatestPriceDateMovesBack()
    {
        using var db = InMemoryDb();
        db.Instruments.Add(new Instrument { Id = 1, Symbol = "KSE100" });
        await db.SaveChangesAsync();
        var day = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
        await Recorder(db, Reading("KSE100", day.AddDays(-1), "Buy")).RecordAsync();
        await Recorder(db, Reading("KSE100", day, "Buy")).RecordAsync();

        // Back to the earlier date, which already has a reading: it must stay as it was
        await Recorder(db, Reading("KSE100", day.AddDays(-1), "Sell")).RecordAsync();

        Assert.Equal("Buy", db.SignalSnapshots.Single(s => s.Date == day.AddDays(-1)).DailyAction);
    }

    private sealed class FakeAnalytics(IEnumerable<InstrumentAnalyticsDto> analytics) : IMarketAnalyticsService
    {
        public Task<IEnumerable<InstrumentAnalyticsDto>> GetAllAnalyticsAsync(CancellationToken cancellationToken = default) => Task.FromResult(analytics);
        public Task<InstrumentAnalyticsDto?> GetInstrumentAnalyticsAsync(string symbol, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public InstrumentAnalyticsDto ComputeAnalytics(IReadOnlyList<PriceDataPoint> dailyPrices, IReadOnlyList<PriceDataPoint>? benchmarkPrices = null) => throw new NotSupportedException();
    }
}
