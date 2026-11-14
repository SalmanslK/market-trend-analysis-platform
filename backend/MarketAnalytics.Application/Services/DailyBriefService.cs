using System.Text.Json;
using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Gathers the day's facts from the app's own calculations (prices, trends, signals, volume, fund flows, news,
/// the stronger-than-cash check, and what history says followed similar trends), has them written up, and saves the
/// brief. The writer explains these facts; it is not asked to predict.
/// </summary>
public class DailyBriefService
{
    /// <summary>Instruments with a long enough history to say what followed similar trends.</summary>
    public static readonly string[] OutlookSymbols = ["KSE100", "MIF", "MGF", "USDPKR"];

    private const int HeadlineDays = 7;
    private const int MaxHeadlines = 40;

    /// <summary>What the app's own tests found, so the writer frames signals honestly.</summary>
    public static readonly string[] ResearchFindings =
    [
        "Technical buy/sell signals, the golden cross and news mood were tested on 2005-2026 prices with results written down in advance: none beat simply holding Meezan Islamic Fund; news describes moves after they happen rather than predicting them.",
        "Trading volume was tested the same way: it made the noisy technical signal less bad but no volume rule beat holding.",
        "The one rule that passed both its test periods is 'stronger than cash': hold stocks while their last 12 months beat the Meezan Cash Fund, otherwise wait in cash, checked monthly. It kept similar returns while cutting the worst fall from about -60% to about -25%. It was tested on stocks, not gold.",
        "Mixing stocks, gold and cash (e.g. 60/20/20) gave up a little return for much smaller falls than any buy/sell rule, 2020-2026.",
    ];

    private readonly IMarketAnalyticsService _analytics;
    private readonly IInstrumentRepository _instruments;
    private readonly IPricePointRepository _prices;
    private readonly IFundPayoutRepository _payouts;
    private readonly INewsRepository? _news;
    private readonly IIndicatorCalculationService _indicators;
    private readonly IBriefRepository _briefs;
    private readonly IBriefWriter _writer;

    public DailyBriefService(
        IMarketAnalyticsService analytics,
        IInstrumentRepository instruments,
        IPricePointRepository prices,
        IFundPayoutRepository payouts,
        IIndicatorCalculationService indicators,
        IBriefRepository briefs,
        IBriefWriter writer,
        INewsRepository? news = null)
    {
        _analytics = analytics;
        _instruments = instruments;
        _prices = prices;
        _payouts = payouts;
        _indicators = indicators;
        _briefs = briefs;
        _writer = writer;
        _news = news;
    }

    public bool CanWrite => _writer.IsConfigured;

    public async Task<DailyBriefDto?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var brief = await _briefs.GetLatestAsync(cancellationToken);
        return brief == null ? null : ToDto(brief);
    }

    public async Task<DailyBriefDto> WriteAsync(CancellationToken cancellationToken = default)
    {
        var factsJson = JsonSerializer.Serialize(await BuildFactsAsync(cancellationToken), JsonOptions);
        var written = await _writer.WriteAsync(factsJson, cancellationToken);
        var brief = new DailyBrief
        {
            Text = written.Text,
            SourcesJson = JsonSerializer.Serialize(written.Sources, JsonOptions),
            FactsJson = factsJson,
            Model = written.Model,
            CostUsd = written.CostUsd,
        };
        await _briefs.AddAsync(brief, cancellationToken);
        return ToDto(brief);
    }

    public async Task<object> BuildFactsAsync(CancellationToken cancellationToken = default)
    {
        var analytics = (await _analytics.GetAllAnalyticsAsync(cancellationToken)).ToList();

        var outlooks = new List<OutlookDto>();
        foreach (var symbol in OutlookSymbols)
        {
            var instrument = await _instruments.GetBySymbolAsync(symbol, cancellationToken);
            if (instrument == null)
                continue;
            var prices = await PriceHistory.LoadDailyAsync(_prices, _payouts, instrument.Id, cancellationToken: cancellationToken);
            if (HistoricalOutlook.Compute(symbol, prices, _indicators) is { } outlook)
                outlooks.Add(outlook);
        }

        var headlines = _news == null
            ? []
            : (await _news.GetPublishedBetweenAsync(DateTime.UtcNow.AddDays(-HeadlineDays), DateTime.UtcNow.AddDays(1), cancellationToken))
                .OrderByDescending(a => a.PublishedAt)
                .Take(MaxHeadlines)
                .Select(a => new { date = a.PublishedAt.ToString("yyyy-MM-dd"), a.Source, a.Title, about = a.Symbols })
                .ToList<object>();

        return new
        {
            writtenOn = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            notes = new[]
            {
                "Prices are daily closes. Fund prices include payouts reinvested; index prices leave out dividends.",
                "Percent figures are already percentages (5.2 means +5.2%).",
                "GOLDPK is rupees per tola, GOLDINT US dollars per ounce, USDPKR rupees per dollar (up = weaker rupee).",
                "MCF is the Meezan Cash Fund: what money earns while it waits.",
            },
            instruments = analytics.Select(a => new
            {
                a.Symbol, a.Name, kind = a.LowRiskKind ?? KindOf(a.Symbol), lastDate = a.LastDate.ToString("yyyy-MM-dd"), a.LastPrice,
                changePercent = new { day = a.Change1D, week = a.Change1W, month = a.Change1M, threeMonths = a.Change3M, yearToDate = a.ChangeYtd, year = a.Change1Y },
                a.High52W, a.Low52W, volatility1Y = a.Volatility1Y, worstFall1Y = a.MaxDrawdown1Y,
                trend = new { a.LongTermTrend.Direction, since = a.LongTermTrend.Since?.ToString("yyyy-MM-dd"), a.LongTermTrend.Phase },
                technicalSignal = a.IsCash ? null : new { a.Signal.Action, a.Signal.Score, a.Signal.MaxScore },
                newsMood = a.IsCash ? null : new { a.NewsSignal.Action, a.NewsSignal.Score, a.NewsSignal.Articles },
                strongerThanCash = a.StrongerThanCash,
                a.Volume, a.FundSize, a.PayoutYield1Y,
            }),
            whatFollowedSimilarTrends = outlooks,
            headlines,
            researchFindings = ResearchFindings,
        };
    }

    private static string KindOf(string symbol) => symbol switch
    {
        "KSE100" or "KMI30" => "Stock index",
        "GOLDPK" or "GOLDINT" => "Gold price",
        "MGF" => "Gold fund",
        "USDPKR" => "Exchange rate",
        _ => "Stock fund",
    };

    private static DailyBriefDto ToDto(DailyBrief brief) => new()
    {
        Id = brief.Id,
        CreatedAt = brief.CreatedAt,
        Text = brief.Text,
        Sources = JsonSerializer.Deserialize<List<BriefSourceDto>>(brief.SourcesJson, JsonOptions) ?? new(),
        Model = brief.Model,
        CostUsd = brief.CostUsd,
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
