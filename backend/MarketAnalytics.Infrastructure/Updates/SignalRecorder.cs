using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MarketAnalytics.Application.DTOs;
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
    private readonly ILogger<SignalRecorder> _logger;

    public SignalRecorder(ApplicationDbContext dbContext, IMarketAnalyticsService analytics, ILogger<SignalRecorder> logger)
    {
        _dbContext = dbContext;
        _analytics = analytics;
        _logger = logger;
    }

    /// <summary>
    /// Saves today's readings. A reading for a date already saved is updated (news can still change until the next
    /// price arrives); readings for earlier dates are never changed. Returns the number of instruments recorded.
    /// </summary>
    public async Task<int> RecordAsync(CancellationToken cancellationToken = default)
    {
        var ids = await _dbContext.Instruments.ToDictionaryAsync(i => i.Symbol, i => i.Id, cancellationToken);
        var recorded = 0;
        foreach (var a in await _analytics.GetAllAnalyticsAsync(cancellationToken))
        {
            if (a.IsCash || !ids.TryGetValue(a.Symbol, out var instrumentId))
                continue;

            var date = DateTime.SpecifyKind(a.LastDate.Date, DateTimeKind.Utc);
            var snapshot = await _dbContext.SignalSnapshots.FirstOrDefaultAsync(s => s.InstrumentId == instrumentId && s.Date == date, cancellationToken);
            if (snapshot == null)
            {
                // Never write a reading for a date older than one already saved: that would be hindsight
                var newest = await _dbContext.SignalSnapshots.Where(s => s.InstrumentId == instrumentId).MaxAsync(s => (DateTime?)s.Date, cancellationToken);
                if (newest > date)
                    continue;
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
            var points = await _dbContext.PricePoints.AsNoTracking()
                .Where(p => p.InstrumentId == group.Key && p.DateTime >= from)
                .OrderBy(p => p.DateTime)
                .ToListAsync(cancellationToken);
            var payouts = await _dbContext.FundPayouts.AsNoTracking().Where(p => p.InstrumentId == group.Key).ToListAsync(cancellationToken);
            prices[group.Key] = PayoutAdjuster.Adjust(PriceDataCleaner.RemoveSpikes(points.Select(p => new PriceDataPoint(p.DateTime, p.Price))), payouts);
        }
        return SignalRecordSummary.Summarize(snapshots, prices);
    }
}
