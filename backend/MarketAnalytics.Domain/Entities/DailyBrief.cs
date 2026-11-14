namespace MarketAnalytics.Domain.Entities;

/// <summary>
/// A market brief written on request by Claude (through Claude Code, on the user's plan) from the app's own figures,
/// plus web news for the world economy. Saved so it can be read again without writing it twice.
/// </summary>
public class DailyBrief
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The brief in a small Markdown subset (## headings, paragraphs, "- " bullets, **bold**).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Web pages cited, as JSON [{ "title", "url" }].</summary>
    public string SourcesJson { get; set; } = "[]";

    /// <summary>The figures the brief was written from, as JSON, so any claim can be checked later.</summary>
    public string FactsJson { get; set; } = "{}";

    public string Model { get; set; } = string.Empty;

    /// <summary>Approximate cost of writing it, US dollars.</summary>
    public decimal CostUsd { get; set; }
}
