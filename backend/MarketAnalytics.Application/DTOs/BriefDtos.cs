namespace MarketAnalytics.Application.DTOs;

/// <summary>What followed past days in the same trend state as today (see HistoricalOutlook).</summary>
public class OutlookDto
{
    public string Symbol { get; set; } = string.Empty;
    /// <summary>Up, Up but weakening, Down, or Down but recovering.</summary>
    public string State { get; set; } = string.Empty;
    public DateTime HistoryFrom { get; set; }
    public DateTime HistoryTo { get; set; }
    /// <summary>Separate spells in today's state: how many independent episodes the numbers come from.</summary>
    public int Spells { get; set; }
    public List<OutlookHorizonDto> Horizons { get; set; } = new();
}

public class OutlookHorizonDto
{
    public string Label { get; set; } = string.Empty;
    public int TradingDays { get; set; }
    /// <summary>Past days in today's state.</summary>
    public OutlookStatsDto SameState { get; set; } = new();
    /// <summary>Every past day, for comparison.</summary>
    public OutlookStatsDto AllDays { get; set; } = new();
}

public class OutlookStatsDto
{
    public int Days { get; set; }
    /// <summary>Share of those days followed by a higher price, percent.</summary>
    public decimal ShareHigherPercent { get; set; }
    public decimal MedianPercent { get; set; }
    /// <summary>One in ten did worse than this.</summary>
    public decimal Low10Percent { get; set; }
    /// <summary>One in ten did better than this.</summary>
    public decimal High90Percent { get; set; }
}

/// <summary>A written brief, as saved.</summary>
public class DailyBriefDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>The brief in a small Markdown subset: ## headings, paragraphs, "- " bullets and **bold**.</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>Web pages the writer looked at for world and economic news.</summary>
    public List<BriefSourceDto> Sources { get; set; } = new();
    public string Model { get; set; } = string.Empty;
    /// <summary>Approximate cost of writing it, US dollars.</summary>
    public decimal CostUsd { get; set; }
}

public class BriefSourceDto
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary>The writer's answer, before it is saved.</summary>
public record WrittenBrief(string Text, List<BriefSourceDto> Sources, string Model, decimal CostUsd);
