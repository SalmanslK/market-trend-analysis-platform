using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.ValueObjects;
using MarketAnalytics.Infrastructure.Data;

namespace MarketAnalytics.Infrastructure.Updates;

/// <summary>
/// Keeps the forward record: after each price update, saves every instrument's signals for its latest price date, and
/// scores the saved readings against what prices did next. Cash and income funds are left out (no signals).
/// </summary>
public class SignalRecorder
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMarketAnalyticsService _analytics;
    private readonly IPricePointRepository _prices;
    private readonly IFundPayoutRepository _payouts;
    private readonly ILogger<SignalRecorder> _logger;

    public SignalRecorder(
        ApplicationDbContext dbContext,
        IMarketAnalyticsService analytics,
        IPricePointRepository prices,
        IFundPayoutRepository payouts,
        ILogger<SignalRecorder> logger)
    {
        _dbContext = dbContext;
        _analytics = analytics;
        _prices = prices;
        _payouts = payouts;
        _logger = logger;
    }

    /// <summary>
    /// Saves today's readings. The reading for an instrument's newest saved date is updated (news can still change
    /// until the next price arrives); nothing is added or changed for a date older than the newest saved one.
    /// Returns the number of instruments recorded.
    /// </summary>
    public async Task<int> RecordAsync(CancellationToken cancellationToken = default)
    {
        var analytics = (await _analytics.GetAllAnalyticsAsync(cancellationToken)).ToList();
        try
        {
            return await SaveAsync(analytics, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // The Worker and the refresh button can record at the same moment; the other one's rows are now saved,
            // so a second pass updates them instead of adding duplicates
            _logger.LogWarning(ex, "Recording signals clashed with another writer; retrying once");
            _dbContext.ChangeTracker.Clear();
            return await SaveAsync(analytics, cancellationToken);
        }
    }

    private async Task<int> SaveAsync(List<InstrumentAnalyticsDto> analytics, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.Instruments.ToDictionaryAsync(i => i.Symbol, i => i.Id, cancellationToken);
        var recorded = 0;
        foreach (var a in analytics)
        {
            if (a.IsCash || !ids.TryGetValue(a.Symbol, out var instrumentId))
                continue;

            var date = DateTime.SpecifyKind(a.LastDate.Date, DateTimeKind.Utc);
            // Never add or change a reading for a date older than one already saved: that would be hindsight
            var newest = await _dbContext.SignalSnapshots.Where(s => s.InstrumentId == instrumentId).MaxAsync(s => (DateTime?)s.Date, cancellationToken);
            if (newest > date)
            {
                _logger.LogWarning("Not recording {Symbol}: its latest price date {Date:yyyy-MM-dd} is older than its saved reading for {Newest:yyyy-MM-dd}", a.Symbol, date, newest);
                continue;
            }

            var snapshot = newest == date
                ? await _dbContext.SignalSnapshots.SingleAsync(s => s.InstrumentId == instrumentId && s.Date == date, cancellationToken)
                : null;
            if (snapshot == null)
            {
                snapshot = new SignalSnapshot { InstrumentId = instrumentId, Date = date };
                _dbContext.SignalSnapshots.Add(snapshot);
            }

            Fill(snapshot, a);
            recorded++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Recorded signals for {Count} instruments", recorded);
        return recorded;
    }

    public static void Fill(SignalSnapshot snapshot, InstrumentAnalyticsDto a)
    {
        snapshot.Price = a.LastPrice;
        snapshot.DailyAction = a.Signal.Action;
        snapshot.DailyScore = a.Signal.Score;
        snapshot.SteadyAction = a.SteadySignal?.Action;
        snapshot.SteadyScore = a.SteadySignal?.Score;
        snapshot.NewsAction = a.NewsSignal.Action;
        snapshot.NewsScore = a.NewsSignal.Score;
        snapshot.NewsArticles = a.NewsSignal.Articles;
        snapshot.CombinedAction = (a.SteadyCombinedSignal ?? a.CombinedSignal).Action;
        snapshot.Trend = a.LongTermTrend.Direction;
        snapshot.AheadOfCash = a.StrongerThanCash?.AheadOfCash;
        snapshot.RecordedAt = DateTime.UtcNow;
    }

    /// <summary>The saved readings scored against later prices.</summary>
    public async Task<SignalRecordDto> SummarizeAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = await _dbContext.SignalSnapshots.AsNoTracking().ToListAsync(cancellationToken);
        var prices = new Dictionary<int, List<PriceDataPoint>>();
        foreach (var group in snapshots.GroupBy(s => s.InstrumentId))
        {
            var from = group.Min(s => s.Date).AddDays(-7);
            prices[group.Key] = await PriceHistory.LoadDailyAsync(_prices, _payouts, group.Key, from, cancellationToken);
        }
        return SignalRecordSummary.Summarize(snapshots, prices);
    }
}
