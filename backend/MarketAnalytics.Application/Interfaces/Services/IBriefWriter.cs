using MarketAnalytics.Application.DTOs;

namespace MarketAnalytics.Application.Interfaces.Services;

/// <summary>Turns the day's facts into a brief in plain words. The only place the app uses AI.</summary>
public interface IBriefWriter
{
    /// <summary>False when the writer cannot run (Claude Code not found).</summary>
    bool IsConfigured { get; }

    Task<WrittenBrief> WriteAsync(string factsJson, CancellationToken cancellationToken = default);
}

/// <summary>Writing failed for a reason the user can act on (not signed in, plan limit reached...).</summary>
public class BriefUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
