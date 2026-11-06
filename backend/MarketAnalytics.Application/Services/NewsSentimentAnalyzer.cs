using System.Text.RegularExpressions;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Sentiment of one article: Score from -100 (clearly negative) to +100 (clearly positive) and a 0-100
/// Confidence that grows with the number of matching words and how much they agree.
/// </summary>
public record SentimentResult(int Score, int Confidence, string Label, List<string> PositiveWords, List<string> NegativeWords);

/// <summary>
/// Lexicon-based sentiment for market headlines. Transparent by design: the matched words are returned so
/// readers can see why an article was scored the way it was. It reads tone, not meaning, so sarcasm, complex
/// sentences and context ("rate cut" is good for stocks but bad for savers) can be misjudged.
/// </summary>
public static class NewsSentimentAnalyzer
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    // Words and phrases that describe prices or the economy moving the right way
    private static readonly Regex Positive = new(
        @"\b(gains?|gained|gaining|rises?|rose|rising|surges?|surged|soars?|soared|jumps?|jumped|rall(?:y|ies|ied)|climbs?|climbed|" +
        @"advances?|advanced|record high|all-time high|fresh high|rebounds?|rebounded|recover(?:s|ed|y|ing)?|boosts?|boosted|" +
        @"improves?|improved|improvement|strengthens?|strengthened|stronger|bullish|bulls|optimism|upbeat|buoyant|" +
        @"surplus|inflows?|upgraded?|upgrades|beats? expectations|appreciates?|appreciated|appreciation|positive|" +
        @"approves?|approved|green zone|in the green|uptrend|upward|higher|edges? up|bull run|eases? pressure)\b",
        Options);

    // Words and phrases that describe prices or the economy moving the wrong way
    private static readonly Regex Negative = new(
        @"\b(falls?|fell|falling|drops?|dropped|declines?|declined|declining|slumps?|slumped|plunges?|plunged|tumbles?|tumbled|" +
        @"sinks?|sank|slides?|slid|loses?|losing|losses|lost|sheds?|crash(?:es|ed)?|weak(?:er|ens|ened|ness)?|bearish|bears|" +
        @"selling pressure|panic|sell-?off|fears?|concerns?|worr(?:y|ies|ied)|deficit|outflows?|downgraded?|downgrades|" +
        @"depreciates?|depreciated|depreciation|(?:one|two|three|\d+)-(?:year|month|week) low|lowest|red zone|jittery|" +
        @"uncertainty|volatile|volatility|slowdown|recession|default|downtrend|downward|lower|slips?|slipped|retreats?|retreated|" +
        @"in the red|profit-taking|selling|tensions|pressure|negative|shortage)\b",
        Options);

    private static readonly Regex RupeeSubject = new(@"\b(rupee|PKR)\b", Options);
    private static readonly Regex DollarSubject = new(@"\b(dollar|greenback|USD)\b", Options);

    // "snaps four-day rally", "ends winning streak": a good run has stopped
    private static readonly Regex EndsGoodRun = new(
        @"\b(snaps?|snapped|ends?|ended|halts?|halted|breaks?|cuts? short)\b[^.,;]{0,30}?\b(rally|winning streak|gaining streak|bull run|gains)\b", Options);
    // "ends losing streak", "halts slide": a bad run has stopped
    private static readonly Regex EndsBadRun = new(
        @"\b(snaps?|snapped|ends?|ended|halts?|halted|breaks?|arrests?)\b[^.,;]{0,30}?\b(losing streak|slide|decline|sell-?off|slump|fall)\b", Options);
    // "up 2.5%", "down 784 points"
    private static readonly Regex UpBy = new(@"\bup\s+(by\s+)?[\d.,]+\s*(%|percent|pc|points?)", Options);
    private static readonly Regex DownBy = new(@"\bdown\s+(by\s+)?[\d.,]+\s*(%|percent|pc|points?)", Options);
    private static readonly Regex InRed = new(@"\bin (the )?red\b", Options);
    private static readonly Regex InGreen = new(@"\bin (the )?green\b", Options);

    /// <summary>
    /// Finds phrases whose meaning differs from their individual words, returning the text with those phrases
    /// removed plus the phrases as positive or negative matches.
    /// </summary>
    private static (string Text, List<string> Positive, List<string> Negative) ExtractReversals(string text)
    {
        var positive = new List<string>();
        var negative = new List<string>();

        foreach (var (regex, list) in new[] { (EndsGoodRun, negative), (EndsBadRun, positive), (UpBy, positive), (DownBy, negative), (InGreen, positive), (InRed, negative) })
        {
            text = regex.Replace(text, m =>
            {
                list.Add(m.Value.ToLowerInvariant());
                return " ";
            });
        }

        return (text, positive, negative);
    }

    /// <summary>
    /// Scores an article's tone. Title words count double: headlines state the main point.
    /// </summary>
    public static SentimentResult Analyze(string title, string summary)
    {
        // Reversal phrases flip the word they act on ("snaps four-day rally" is negative), so score them first
        // and remove them before the single-word lists see "rally"
        var (titleText, titlePositive, titleNegative) = ExtractReversals(title);
        var (summaryText, summaryPositive, summaryNegative) = ExtractReversals(summary);

        var titlePositiveWords = titlePositive.Concat(Matches(Positive, titleText)).ToList();
        var titleNegativeWords = titleNegative.Concat(Matches(Negative, titleText)).ToList();
        var summaryPositiveWords = summaryPositive.Concat(Matches(Positive, summaryText)).ToList();
        var summaryNegativeWords = summaryNegative.Concat(Matches(Negative, summaryText)).ToList();

        var positive = titlePositiveWords.Concat(summaryPositiveWords).ToList();
        var negative = titleNegativeWords.Concat(summaryNegativeWords).ToList();

        double positiveWeight = titlePositiveWords.Count * 2 + summaryPositiveWords.Count;
        double negativeWeight = titleNegativeWords.Count * 2 + summaryNegativeWords.Count;
        var total = positiveWeight + negativeWeight;

        if (total == 0)
            return new SentimentResult(0, 0, "Neutral", new(), new());

        var score = (int)Math.Round((positiveWeight - negativeWeight) / total * 100);
        // Agreement between the words, scaled by how much evidence there is (1 title word ~ 63%, 2 ~ 86%)
        var agreement = Math.Abs(positiveWeight - negativeWeight) / total;
        var evidence = 1 - Math.Exp(-total / 2);
        var confidence = (int)Math.Round(agreement * evidence * 100);

        return new SentimentResult(score, confidence, LabelFor(score), Distinct(positive), Distinct(negative));
    }

    /// <summary>
    /// The article's sentiment for one instrument's price. Most instruments move with the tone of their news,
    /// but USD/PKR is quoted as rupees per dollar: "rupee gains" means USD/PKR falls, so the score is flipped
    /// when the rupee is the subject of the headline.
    /// </summary>
    public static int DirectionFor(string symbol, string title)
    {
        if (!symbol.Equals("USDPKR", StringComparison.OrdinalIgnoreCase))
            return 1;

        var rupee = RupeeSubject.Match(title);
        var dollar = DollarSubject.Match(title);
        if (!rupee.Success)
            return 1;
        // Whichever currency is mentioned first is treated as the subject ("Rupee falls against dollar")
        return !dollar.Success || rupee.Index < dollar.Index ? -1 : 1;
    }

    public static string LabelFor(int score) => score switch
    {
        >= 20 => "Positive",
        <= -20 => "Negative",
        _ => "Neutral",
    };

    private static List<string> Matches(Regex regex, string text) =>
        regex.Matches(text).Select(m => m.Value.ToLowerInvariant()).ToList();

    private static List<string> Distinct(List<string> words) => words.Distinct().ToList();
}
