using System.Text.RegularExpressions;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Sentiment of one article: Score from -100 (clearly negative) to +100 (clearly positive) and a 0-100
/// Confidence that grows with the number of matching words and how much they agree.
/// </summary>
public record SentimentResult(int Score, int Confidence, string Label, List<string> PositiveWords, List<string> NegativeWords);

/// <summary>
/// Rule-based sentiment for market headlines; no AI. Transparent by design: the matched words are returned so
/// readers can see why an article was scored the way it was.
///
/// How it reads a headline:
/// 1. Phrases whose meaning differs from their words are taken first ("snaps rally", "easing fears", "pares gains").
/// 2. The text is split into clauses ("Gold surges | as dollar weakens") and each clause's subject is found: the
///    instrument itself, or something else such as the dollar, oil or silver. Words count for the instrument only
///    through how that subject affects it: a stronger dollar is bad for gold and means USD/PKR rises, cheaper oil is good
///    for Pakistani stocks and the rupee, and silver or US tech stocks say nothing about gold.
/// 3. A headline whose own subject is "unchanged", "steady" or "stable" is neutral.
/// 4. Strong words ("surges", "plunges") count more than mild ones ("edges up", "slight"); the title counts double,
///    its first clause double again, and background clauses ("amid volatility") half.
/// It still reads wording, not meaning, so sarcasm and complex sentences can be misjudged.
/// </summary>
public static class NewsSentimentAnalyzer
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    /// <summary>Whose point of view an article is read from.</summary>
    public enum View { General, Stocks, Gold, Rupee }

    private enum Subject { None, Stocks, Gold, Rupee, Dollar, Oil, OtherMetal, GlobalStocks, Rates, Reserves }

    // Words and phrases that describe prices or the economy moving the right way
    private static readonly Regex Positive = new(
        @"\b(gains?|gained|gaining|rises?|rose|rising|surges?|surged|soars?|soared|jumps?|jumped|rall(?:y|ies|ied)|climbs?|climbed|" +
        @"advances?|advanced|(?:record|all-time|fresh|historic|new|multi-year|\d+-(?:year|month|week)) highs?|rebounds?|rebounded|recover(?:s|ed|y|ing)?|boosts?|boosted|" +
        @"improves?|improved|improvement|strengthens?|strengthened|stronger|bullish|bulls|optimism|optimistic|upbeat|buoyant|" +
        @"surplus|inflows?|upgraded?|upgrades|beats? (?:expectations|estimates)|appreciates?|appreciated|appreciation|positive|" +
        @"approves?|approved|uptrend|upward|higher|edges? up|bull run|reclaims?|reclaimed|regains?|regained|comeback|" +
        @"increases?|increased|go(?:es)? up|went up|spikes?|spiked|bolsters?|emboldened|milestone|firms?|firmed|shines?|" +
        @"soaring|surging|jumping|bounces?|bounced|ascent|crosses|crossed|tops|topped|surpass(?:es|ed)?|winning streak|stellar)\b",
        Options);

    // Words and phrases that describe prices or the economy moving the wrong way
    private static readonly Regex Negative = new(
        @"\b(falls?|fell|falling|drops?|dropped|declines?|declined|declining|slumps?|slumped|plunges?|plunged|tumbles?|tumbled|" +
        @"sinks?|sank|slides?|slid|loses?|losing|losses|lost|sheds?|crash(?:es|ed)?|weak(?:er|ens|ened|ness)?|bearish|bears|" +
        @"selling pressure|panic|sell-?off|fears?|concerns?|worr(?:y|ies|ied)|deficit|outflows?|downgraded?|downgrades|" +
        @"depreciates?|depreciated|depreciation|(?:one|two|three|\d+|multi)-(?:year|month|week) lows?|" +
        @"(?:record|all-time|all time|lifetime|historic|new) lows?|lowest|red zone|jittery|suffers?|suffered|escalat\w+|losing streak|" +
        @"uncertainty|volatile|volatility|slowdown|recession|default|downtrend|downward|lower|slips?|slipped|retreats?|retreated|" +
        @"profit-taking|selling|tensions|pressure|negative|shortage|dives?|dived|dumped|rout|plummets?|plummeted|" +
        @"dips?|dipped|decreases?|decreased|threaten(?:s|ed)?|edges? down|eases?|eased|slashed|cools?|cooled|dims?|dimmed)\b",
        Options);

    // Words that make a move big or small
    private static readonly Regex Strong = new(
        @"^(surg|soar|plung|crash|tumbl|record high|all-time high|rout|dive|dived|dump|plummet|slump|jump|spik|sell-?off|panic)", Options);
    private static readonly Regex Mild = new(
        @"\b(slight(?:ly)?|marginal(?:ly)?|modest(?:ly)?|edges?|inch(?:es|ed)?|ticks?|a bit)\b", Options);

    // "unchanged", "holds steady": nothing moved
    private static readonly Regex Unchanged = new(
        @"\b(unchanged|no change|steady|stable|flat|flatlines?|range-bound|stays? at|remains? at)\b", Options);

    // Phrases read before single words, because their meaning differs from their words
    private static readonly (Regex Pattern, int Sign)[] Phrases =
    {
        // A good run has stopped: "snaps four-day rally", "ends winning streak", "pares gains"
        (new(@"\b(snaps?|snapped|ends?|ended|halts?|halted|breaks?|cuts? short|pauses?|paused|pares?|pared|trims?|trimmed|erases?|erased|reverses?|reversed|gives? up|gave up|wipes? out|wiped out)\b[^.,;]{0,30}?\b(rally|winning streak|gaining streak|bull run|gains)\b", Options), -1),
        // A bad run has stopped: "ends losing streak", "halts slide"
        (new(@"\b(snaps?|snapped|ends?|ended|halts?|halted|breaks?|arrests?)\b[^.,;]{0,30}?\b(losing streak|slide|decline|sell-?off|slump|fall)\b", Options), 1),
        // Worries going away: "easing fears", "fears ease", "allays concerns"
        (new(@"\b(eas(?:es|ed|ing)|allay\w*|cool(?:s|ed|ing)?)\s+(?:\w+\s+)?(fears?|concerns?|worries|tensions|pressure)\b|\b(fears?|concerns?|worries|tensions)\s+(eas(?:e|es|ed|ing)|recede\w*|fade\w*)\b", Options), 1),
        (new(@"\brecover(?:s|ed|ing)? (?:its )?lost ground\b", Options), 1),
        (new(@"\bbulls? head(?:s|ed)? for the exit\b|\bcasts? a shadow\b|\bweighs? on\b|\brattles?\b|\bspooks?\b", Options), -1),
        // "up 2.5%", "up by Rs1,500", "down 784 points"
        (new(@"\bup\s+(?:by\s+)?(?:Rs\.?\s?)?[\d.,]+\s*(%|percent|pc|points?|per\b)?", Options), 1),
        (new(@"\bdown\s+(?:by\s+)?(?:Rs\.?\s?)?[\d.,]+\s*(%|percent|pc|points?|per\b)?", Options), -1),
        (new(@"\bin (?:the )?green\b", Options), 1),
        (new(@"\bin (?:the )?red\b", Options), -1),
    };

    // Clause boundaries: punctuation and joining words that start a new thought
    private static readonly Regex ClauseBreak = new(
        @"\s*[,;:|–—]\s*|\s+(?=(?:as|amid|after|while|but|on|despite|following|ahead of|with|over|due to)\s)", Options);
    // Clauses that only give background ("amid volatility") count half
    private static readonly Regex Background = new(@"^(amid|despite|ahead of|with|over)\b", Options);

    // Subjects, by the first one named in a clause
    private static readonly (Subject Subject, Regex Pattern)[] Subjects =
    {
        (Subject.OtherMetal, new(@"\b(silver|platinum|copper|palladium|aluminium)\b", Options)),
        (Subject.Oil, new(@"\b(oil|crude|Brent|WTI|petrol|diesel|petroleum|fuel|OPEC\+?|LNG)\b", Options)),
        (Subject.Reserves, new(@"\b(reserves|remittances?|current account|inflows?)\b", Options)),
        (Subject.Rupee, new(@"\b(rupee|PKR)\b", Options)),
        (Subject.Dollar, new(@"\b(dollar|greenback|USD|DXY|interbank rate|exchange rate|dollar rate)\b", Options)),
        (Subject.Gold, new(@"\b(gold|bullion|yellow metal|XAU|tola)\b", Options)),
        (Subject.GlobalStocks, new(@"\b(Wall Street|Nasdaq|Dow|S&P|tech|US stocks|global stocks|global markets|Asian (?:stocks|markets)|Sensex|Nifty)\b", Options)),
        (Subject.Rates, new(@"\b(yields?|interest rates?|policy rate|rates|inflation|bonds?|T-bills?|Fed)\b", Options)),
        (Subject.Stocks, new(@"\b(PSX|KSE|KMI|stocks?|shares|bourse|index|equit(?:y|ies)|bulls|bears|market)\b", Options)),
    };

    /// <summary>
    /// The view an instrument reads news from: USD/PKR moves opposite to the rupee, gold with gold, the rest with stocks.
    /// </summary>
    public static View ViewFor(string symbol) => symbol.ToUpperInvariant() switch
    {
        "USDPKR" => View.Rupee,
        "GOLDPK" or "GOLDINT" or "MGF" => View.Gold,
        _ => View.Stocks,
    };

    /// <summary>
    /// How a move of the clause's subject affects the instrument: 1 the same way, -1 the opposite way, 0 not at all.
    /// For USD/PKR "up" means more rupees per dollar.
    /// </summary>
    private static int Effect(View view, Subject subject) => (view, subject) switch
    {
        (View.Stocks, Subject.Stocks) => 1,
        (View.Stocks, Subject.GlobalStocks) => 1,
        (View.Stocks, Subject.Oil) => -1,
        (View.Gold, Subject.Gold) => 1,
        (View.Gold, Subject.Dollar) => -1,
        (View.Gold, Subject.Rupee) => -1,
        (View.Rupee, Subject.Rupee) => -1,
        (View.Rupee, Subject.Reserves) => -1,
        (View.Rupee, Subject.Dollar) => 1,
        (View.Rupee, Subject.Oil) => 1,
        _ => 0,
    };

    /// <summary>
    /// Scores an article's tone in general: the subject of the headline's first clause is taken as its main subject,
    /// and clauses about something else are left out.
    /// </summary>
    public static SentimentResult Analyze(string title, string summary) => Score(title, summary, View.General);

    /// <summary>
    /// Scores an article for one instrument's price (see <see cref="ViewFor"/>).
    /// </summary>
    public static SentimentResult AnalyzeFor(string symbol, string title, string summary) => Score(title, summary, ViewFor(symbol));

    private static SentimentResult Score(string title, string summary, View view)
    {
        var titleClauses = Clauses(title);
        var main = titleClauses.Select(SubjectOf).FirstOrDefault(s => s != Subject.None);
        // A linked headline that names no subject is about the instrument itself ("Bulls return", "Rate hits Rs280")
        if (main == Subject.None)
            main = view switch { View.Stocks => Subject.Stocks, View.Gold => Subject.Gold, View.Rupee => Subject.Dollar, _ => Subject.None };
        int EffectOf(Subject subject) => view == View.General
            ? (subject == main || subject == Subject.None ? 1 : 0)
            : Effect(view, subject == Subject.None ? main : subject);

        // A headline saying its own subject didn't move is neutral, whatever the summary adds
        var mainClause = titleClauses.FirstOrDefault(c => SubjectOf(c) == main) ?? title;
        if (Unchanged.IsMatch(mainClause) && Hits(mainClause).Count == 0 && (view == View.General || EffectOf(main) != 0))
            return Neutral;

        var positive = new List<string>();
        var negative = new List<string>();
        double positiveWeight = 0, negativeWeight = 0;

        void Read(List<string> clauses, double baseWeight)
        {
            var subject = main;
            for (int i = 0; i < clauses.Count; i++)
            {
                var clause = clauses[i];
                var own = SubjectOf(clause);
                if (own != Subject.None) subject = own;
                var effect = EffectOf(subject);
                if (effect == 0) continue;

                var weight = baseWeight
                    * (baseWeight > 1 && i == 0 ? 2 : 1)
                    * (Background.IsMatch(clause) ? 0.5 : 1)
                    * (Mild.IsMatch(clause) ? 0.5 : 1);
                foreach (var (word, sign) in Hits(clause))
                {
                    var w = weight * (Strong.IsMatch(word) ? 1.5 : 1);
                    if (sign * effect > 0) { positiveWeight += w; positive.Add(word); }
                    else { negativeWeight += w; negative.Add(word); }
                }
            }
        }

        Read(titleClauses, 2);
        Read(Clauses(summary), 1);

        var total = positiveWeight + negativeWeight;
        if (total == 0)
            return Neutral;

        // One plain title word scores 80, a strong one 86, a mild one 67; mixed wording pulls towards 0
        var score = (int)Math.Round((positiveWeight - negativeWeight) / (total + 1) * 100);
        var agreement = Math.Abs(positiveWeight - negativeWeight) / total;
        var evidence = 1 - Math.Exp(-total / 2);
        var confidence = (int)Math.Round(agreement * evidence * 100);

        return new SentimentResult(score, confidence, LabelFor(score), positive.Distinct().ToList(), negative.Distinct().ToList());
    }

    private static SentimentResult Neutral => new(0, 0, "Neutral", new(), new());

    private static List<string> Clauses(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new();

        // A one- or two-word piece is part of a list ("Gold, silver prices hit record high"), so it joins the next clause
        var pieces = ClauseBreak.Split(text).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        var clauses = new List<string>();
        var carry = "";
        foreach (var piece in pieces)
        {
            var clause = carry.Length > 0 ? $"{carry}, {piece}" : piece;
            // "Gold firms, silver slides" is two clauses: a short piece only joins the next one when it says nothing itself
            if (clause.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 2 && Hits(clause).Count == 0 && piece != pieces[^1])
            {
                carry = clause;
                continue;
            }
            clauses.Add(clause);
            carry = "";
        }
        return clauses;
    }

    private static Subject SubjectOf(string clause)
    {
        var first = Subject.None;
        var at = int.MaxValue;
        foreach (var (subject, pattern) in Subjects)
        {
            var match = pattern.Match(clause);
            if (match.Success && match.Index < at)
            {
                first = subject;
                at = match.Index;
            }
        }
        return first;
    }

    /// <summary>Phrases first, then single words in what the phrases left.</summary>
    private static List<(string Word, int Sign)> Hits(string clause)
    {
        var hits = new List<(string, int)>();
        foreach (var (pattern, sign) in Phrases)
        {
            clause = pattern.Replace(clause, m =>
            {
                hits.Add((m.Value.Trim().ToLowerInvariant(), sign));
                return " ";
            });
        }
        hits.AddRange(Positive.Matches(clause).Select(m => (m.Value.ToLowerInvariant(), 1)));
        hits.AddRange(Negative.Matches(clause).Select(m => (m.Value.ToLowerInvariant(), -1)));
        return hits;
    }

    public static string LabelFor(int score) => score switch
    {
        >= 20 => "Positive",
        <= -20 => "Negative",
        _ => "Neutral",
    };
}
