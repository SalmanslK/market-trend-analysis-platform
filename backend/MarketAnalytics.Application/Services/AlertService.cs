using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Lists the days a signal or trend changed. Rather than storing snapshots, each day's signals are recomputed with
/// the same rules and data as the live signals and the backtests, so the list always agrees with what the app shows.
/// </summary>
public class AlertService : IAlertService
{
    public const int MaxDays = 90;

    private readonly IInstrumentRepository _instrumentRepository;
    private readonly IPricePointRepository _pricePointRepository;
    private readonly IIndicatorCalculationService _indicators;
    private readonly INewsRepository? _newsRepository;
    private readonly IFundPayoutRepository? _fundPayoutRepository;

    public AlertService(
        IInstrumentRepository instrumentRepository,
        IPricePointRepository pricePointRepository,
        IIndicatorCalculationService indicators,
        INewsRepository? newsRepository = null,
        IFundPayoutRepository? fundPayoutRepository = null)
    {
        _instrumentRepository = instrumentRepository;
        _pricePointRepository = pricePointRepository;
        _indicators = indicators;
        _newsRepository = newsRepository;
        _fundPayoutRepository = fundPayoutRepository;
    }

    public async Task<List<SignalAlertDto>> GetRecentAsync(int days, bool steady = true, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, MaxDays);
        var since = DateTime.UtcNow.Date.AddDays(-days);

        // News for the window plus the 7 days that feed its first day's mood
        var articles = _newsRepository == null
            ? new()
            : (await _newsRepository.GetPublishedBetweenAsync(since.AddDays(-SignalRules.NewsWindowDays - 1), DateTime.UtcNow.AddDays(1), cancellationToken))
                .Select(a => (a, NewsSentimentAnalyzer.Analyze(a.Title, a.Summary)))
                .ToList();

        var alerts = new List<SignalAlertDto>();
        foreach (var instrument in await _instrumentRepository.GetActiveInstrumentsAsync(cancellationToken))
        {
            if (PortfolioService.IsCash(instrument.Symbol))
                continue;

            var prices = await PriceHistory.LoadDailyAsync(_pricePointRepository, _fundPayoutRepository, instrument.Id, cancellationToken: cancellationToken);
            var history = DailySignals(prices, NewsService.ScoreForSymbol(articles, instrument.Symbol));
            foreach (var change in Changes(history, since, steady))
            {
                change.Symbol = instrument.Symbol;
                change.Name = instrument.Name;
                alerts.Add(change);
            }
        }

        return alerts.OrderByDescending(a => a.Date).ThenBy(a => a.Symbol).ToList();
    }

    /// <summary>
    /// Each day's technical, news and combined action and trend direction, from the first day all can be computed.
    /// </summary>
    public List<DailySignals> DailySignals(IReadOnlyList<PriceDataPoint> prices, IReadOnlyList<ScoredNews> news)
    {
        var technical = SignalHistory.TechnicalScores(_indicators, prices);
        if (technical.Count == 0)
            return new();

        var sorted = prices.OrderBy(p => p.DateTime).ToList();
        var sma50 = _indicators.ComputeSma(sorted, SignalRules.MediumTrendPeriod);
        var sma200 = _indicators.ComputeSma(sorted, SignalRules.LongTrendPeriod);
        var moods = NewsService.DailyMood(technical.Select(t => t.Date).ToList(), news, SignalRules.NewsWindowDays);
        var steady = SignalRules.SteadyScores(technical.Select(t => t.Score).ToList(), BacktestService.MaxTechnicalScore);

        return technical.Select((t, i) =>
        {
            var mood = moods[i];
            var combined = SignalRules.CombinedScore(t.Score, BacktestService.MaxTechnicalScore, mood.Score, mood.Articles);
            return new DailySignals(
                t.Date,
                SignalRules.ActionFor(t.Score),
                SignalRules.ActionForScaledScore(SignalRules.NewsScore(mood.Score, mood.Articles)),
                SignalRules.ActionForScaledScore(combined),
                SignalRules.GoldenCross(sma50[t.Date]!.Value, sma200[t.Date]!.Value) > 0 ? "Up" : "Down",
                SignalRules.ActionForScaledScore(steady[i]),
                SignalRules.ActionForScaledScore(SignalRules.CombinedScaledScore(steady[i], mood.Score, mood.Articles)));
        }).ToList();
    }

    /// <summary>
    /// The changes from one day to the next, for days on or after <paramref name="since"/>. Only changes of side count
    /// (Buy to Hold, Hold to Sell...), not Buy to Strong Buy. Important: the combined signal turned to Buy or Sell, or
    /// the trend changed; a move to Hold means "stay as you are", so it isn't a call to act.
    /// </summary>
    public static List<SignalAlertDto> Changes(IReadOnlyList<DailySignals> history, DateTime since, bool steady = true)
    {
        var changes = new List<SignalAlertDto>();
        for (int i = 1; i < history.Count; i++)
        {
            var (before, after) = (history[i - 1], history[i]);
            if (after.Date < since)
                continue;

            void Add(string signal, string from, string to)
            {
                if (Side(from) == Side(to))
                    return;
                var important = signal == "Trend" || (signal == "Combined" && Side(to) != "Hold");
                changes.Add(new SignalAlertDto { Date = after.Date, Signal = signal, From = from, To = to, Important = important });
            }

            // The technical and combined signals follow the chosen version: steady (20-day average) or daily
            Add("Combined", steady ? before.SteadyCombined : before.Combined, steady ? after.SteadyCombined : after.Combined);
            Add("Technical", steady ? before.SteadyTechnical : before.Technical, steady ? after.SteadyTechnical : after.Technical);
            Add("News", before.News, after.News);
            Add("Trend", before.Trend, after.Trend);
        }
        return changes;
    }

    /// <summary>Buy, Sell or Hold, ignoring strength; trend directions are their own side.</summary>
    private static string Side(string action) => action switch
    {
        "StrongBuy" or "Buy" => "Buy",
        "StrongSell" or "Sell" => "Sell",
        _ => action,
    };
}

public record DailySignals(DateTime Date, string Technical, string News, string Combined, string Trend, string SteadyTechnical = "Hold", string SteadyCombined = "Hold");
