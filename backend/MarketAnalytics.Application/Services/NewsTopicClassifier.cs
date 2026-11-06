using System.Text.RegularExpressions;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// A market news topic and the instruments it affects.
/// </summary>
/// <param name="Symbols">Instruments the topic can affect; articles are shown for all of them.</param>
/// <param name="DirectSymbols">Instruments the topic is directly about. Only these count the article's tone towards
/// their news mood; for the others the article is background context (e.g. oil news for USD/PKR).</param>
public record NewsTopic(
    string Key,
    string Label,
    string WhyItMatters,
    string[] Symbols,
    string[] DirectSymbols,
    Regex Keywords,
    bool RequiresPakistanContext);

/// <summary>
/// Tags news articles with market topics using keyword rules, and links topics to instruments.
/// Rules are deliberately simple and transparent: the UI shows which topic linked an article to an instrument.
/// </summary>
public static class NewsTopicClassifier
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static readonly string[] PakistanStocks = { "KSE100", "KMI30" };
    private static readonly string[] MeezanFunds = { "KMIF", "MIF", "MGF", "MBF" };

    // Feeds also carry international stories ("European stocks fall"), so Pakistan-specific topics
    // only match when the article mentions Pakistan
    private static readonly Regex PakistanContext = new(
        @"\b(Pakistan|Pakistani|PSX|KSE|KMI|SBP|State Bank|Karachi|Islamabad|Lahore|rupee|PKR|Rs\.?\s?\d)", Options);

    // "Rupee" and "Rs" are also Indian and Sri Lankan, so a story about those markets needs a clear Pakistan mention
    private static readonly Regex ForeignMarket = new(
        @"\b(Sri Lankan?|Colombo|India|Indian|Nifty|Sensex|Bangladesh|Dhaka|Nepal)\b", Options);
    private static readonly Regex ClearlyPakistan = new(
        @"\b(Pakistan|Pakistani|PSX|KSE|KMI|SBP|Karachi|Islamabad|Lahore)\b", Options);

    public static IReadOnlyList<NewsTopic> Topics { get; } = new[]
    {
        new NewsTopic(
            "pakistan-stocks",
            "Pakistan stocks",
            "Share prices on the Pakistan Stock Exchange drive the KSE-100 and KMI-30 indices and the Meezan stock funds.",
            PakistanStocks.Concat(MeezanFunds).ToArray(),
            PakistanStocks.Concat(MeezanFunds).ToArray(),
            new Regex(@"\b(PSX|KSE[- ]?100|KSE|KMI[- ]?30|stock exchange|stock market|stocks|equities|equity market|bourse|share prices|benchmark index|bulls|bears)\b", Options),
            RequiresPakistanContext: true),

        new NewsTopic(
            "mutual-funds",
            "Mutual funds",
            "News about fund managers and fund flows can affect the Meezan funds directly.",
            MeezanFunds,
            MeezanFunds,
            new Regex(@"\b(mutual funds?|Meezan|MUFAP|asset management|net asset value)\b", Options),
            RequiresPakistanContext: true),

        new NewsTopic(
            "interest-rates",
            "Interest rates & inflation",
            "Higher interest rates make bank deposits more attractive than shares, which often weighs on stocks; lower rates tend to help them.",
            PakistanStocks.Concat(MeezanFunds).Append("USDPKR").ToArray(),
            Array.Empty<string>(),
            new Regex(@"\b(policy rate|interest rates?|monetary policy|inflation|CPI|T-?bills?|treasury bills?|rate cut|rate hike)\b", Options),
            RequiresPakistanContext: true),

        new NewsTopic(
            "rupee-dollar",
            "Rupee & dollar",
            "The rupee's value against the dollar sets USD/PKR and changes the rupee price of gold in Pakistan.",
            new[] { "USDPKR", "GOLDPK" },
            new[] { "USDPKR" },
            new Regex(@"\b(rupee|PKR|exchange rate|dollar|greenback|forex|foreign exchange|remittances?|foreign reserves|FX reserves|current account)\b", Options),
            RequiresPakistanContext: true),

        new NewsTopic(
            "economy-imf",
            "Economy & IMF",
            "IMF programmes, budgets and growth figures shape confidence in Pakistan's economy, its stocks and the rupee.",
            PakistanStocks.Append("USDPKR").ToArray(),
            Array.Empty<string>(),
            new Regex(@"\b(IMF|International Monetary Fund|budget|fiscal deficit|GDP|economic growth|trade deficit|credit rating|Moody's|Fitch|S&P)\b", Options),
            RequiresPakistanContext: true),

        new NewsTopic(
            "gold",
            "Gold",
            "Gold is priced globally in US dollars; the Pakistani gold price follows it, adjusted for the rupee.",
            new[] { "GOLDINT", "GOLDPK" },
            new[] { "GOLDINT", "GOLDPK" },
            new Regex(@"\b(gold|bullion|XAU|precious metals?)\b", Options),
            RequiresPakistanContext: false),

        new NewsTopic(
            "oil",
            "Oil prices",
            "Pakistan imports most of its oil, so oil prices affect the trade bill, the rupee and energy-heavy stock indices.",
            PakistanStocks.Append("USDPKR").ToArray(),
            Array.Empty<string>(),
            new Regex(@"\b(oil prices?|crude|Brent|OPEC\+?|petroleum prices?)\b", Options),
            RequiresPakistanContext: false),
    };

    private static readonly Dictionary<string, NewsTopic> TopicsByKey = Topics.ToDictionary(t => t.Key);

    /// <summary>
    /// Returns the keys of all topics the article covers (possibly none).
    /// </summary>
    public static List<string> Classify(string title, string summary)
    {
        var text = $"{title} {summary}";
        var hasPakistanContext = PakistanContext.IsMatch(text)
            && (!ForeignMarket.IsMatch(title) || ClearlyPakistan.IsMatch(title));

        return Topics
            .Where(t => (!t.RequiresPakistanContext || hasPakistanContext) && t.Keywords.IsMatch(text))
            .Select(t => t.Key)
            .ToList();
    }

    public static NewsTopic? GetTopic(string key) => TopicsByKey.GetValueOrDefault(key);

    public static IEnumerable<string> ParseTopics(string topics) =>
        topics.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(TopicsByKey.ContainsKey);

    /// <summary>
    /// Topics in the article that affect the given instrument.
    /// </summary>
    public static List<NewsTopic> TopicsForSymbol(string topics, string symbol) =>
        ParseTopics(topics)
            .Select(k => TopicsByKey[k])
            .Where(t => t.Symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase))
            .ToList();

    /// <summary>
    /// Whether any of the article's topics is directly about the instrument.
    /// </summary>
    public static bool IsDirectTopic(string topics, string symbol) =>
        ParseTopics(topics).Any(k => TopicsByKey[k].DirectSymbols.Contains(symbol, StringComparer.OrdinalIgnoreCase));

    public static string[] TopicKeysForSymbol(string symbol) =>
        Topics.Where(t => t.Symbols.Contains(symbol, StringComparer.OrdinalIgnoreCase)).Select(t => t.Key).ToArray();
}
