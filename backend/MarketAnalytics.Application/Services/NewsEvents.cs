using System.Text.RegularExpressions;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// One piece of news is often carried by many outlets under different headlines (Mari's copper-gold discovery: 20+
/// headlines in two days). Counted per headline, a widely copied event would outweigh everything else and inflate how
/// sure the mood looks. Headlines about the same instrument at most 2 days apart that share most of their key words
/// are treated as one event, which counts once with the average tone of its headlines.
/// </summary>
public static partial class NewsEvents
{
    public const double SameEvent = 0.4;
    public static readonly TimeSpan Window = TimeSpan.FromDays(2);

    // Words that say nothing about which event a headline is about
    private static readonly HashSet<string> Filler = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "into", "over", "after", "amid", "its", "his", "her", "their", "this", "that",
        "says", "said", "say", "new", "news", "report", "reports", "update", "today", "week", "year", "per", "cent",
        "percent", "than", "more", "less", "about", "near", "has", "have", "had", "will", "was", "were", "are", "not", "but",
        "rs", "pkr", "pakistan", "pakistans", "ltd", "limited", "company", "group", "share", "shares", "stock", "stocks",
    };

    [GeneratedRegex(@"[A-Za-z]+")]
    private static partial Regex Word();

    /// <summary>A headline's key words, cut to their first five letters so "discovery" and "discovers" match.</summary>
    public static HashSet<string> KeyWords(string title) =>
        Word().Matches(title)
            .Select(m => m.Value.ToLowerInvariant())
            .Where(w => w.Length >= 3 && !Filler.Contains(w))
            .Select(w => w.Length > 5 ? w[..5] : w)
            .ToHashSet();

    /// <summary>Share of key words two headlines have in common (0 to 1).</summary>
    public static double Similarity(HashSet<string> a, HashSet<string> b) =>
        a.Count == 0 || b.Count == 0 ? 0 : (double)a.Intersect(b).Count() / a.Union(b).Count();

    /// <summary>
    /// Groups items into events: in date order, an item joins the most recent event it resembles (any of its headlines,
    /// within the window), otherwise starts a new one.
    /// </summary>
    public static List<List<T>> Group<T>(IEnumerable<T> items, Func<T, string> title, Func<T, DateTime> date)
    {
        var events = new List<(List<T> Items, List<HashSet<string>> Words, DateTime Last)>();
        foreach (var item in items.OrderBy(date))
        {
            var words = KeyWords(title(item));
            var when = date(item);
            var match = events.LastOrDefault(e => when - e.Last <= Window && e.Words.Any(w => Similarity(w, words) >= SameEvent));
            if (match.Items == null)
            {
                events.Add((new List<T> { item }, new List<HashSet<string>> { words }, when));
                continue;
            }
            match.Items.Add(item);
            match.Words.Add(words);
            var index = events.FindLastIndex(e => ReferenceEquals(e.Items, match.Items));
            events[index] = (match.Items, match.Words, when > match.Last ? when : match.Last);
        }
        return events.Select(e => e.Items).ToList();
    }
}
