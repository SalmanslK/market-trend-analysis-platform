namespace MarketAnalytics.Api;

/// <summary>
/// Parts of the app that can be switched on or off in appsettings.json ("Features" section).
/// </summary>
public class FeatureSettings
{
    /// <summary>
    /// News and sentiment: the News page, the news and combined signals, and news in summaries, alerts and the track
    /// record. Set in appsettings.json (on); off when the setting is missing, for a technical-only app. The
    /// Worker keeps collecting news either way, so the history is there when it is switched on.
    /// </summary>
    public bool News { get; set; }
}
