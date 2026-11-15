using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// Scores the saved signal readings against what prices did next: the move over the following
/// <see cref="HorizonDays"/> trading days, starting at the next day's close (as in the research scripts), so a reading
/// counts only once its outcome is fully known.
/// </summary>
public static class SignalRecordSummary
{
    public const int HorizonDays = 20;

    // Group names, shown by the website (LiveRecord.tsx matches AnyDay and Combined by name)
    public const string AnyDay = "Any day";
    public const string Steady = "Technical signal (steady)";
    public const string Combined = "Combined signal";
    public const string VersusCash = "Stronger than cash";

    private static readonly string[] Sides = { "Buy", "Hold", "Sell" };

    public static SignalRecordDto Summarize(IReadOnlyCollection<SignalSnapshot> snapshots, IReadOnlyDictionary<int, List<PriceDataPoint>> pricesByInstrument)
    {
        var scored = new List<(SignalSnapshot Snapshot, decimal Move)>();
        foreach (var snapshot in snapshots)
        {
            if (!pricesByInstrument.TryGetValue(snapshot.InstrumentId, out var prices))
                continue;
            var index = IndexOf(prices, snapshot.Date);
            if (index < 0 || index + 1 + HorizonDays >= prices.Count || prices[index + 1].Price <= 0)
                continue;
            scored.Add((snapshot, prices[index + 1 + HorizonDays].Price / prices[index + 1].Price - 1));
        }

        var groups = new List<SignalRecordGroupDto> { Group(AnyDay, "All readings", scored) };
        // Only readings where the steady signal existed, so its forward test isn't mixed with the daily signal
        foreach (var side in Sides)
            groups.Add(Group(Steady, side, scored.Where(s => Side(s.Snapshot.SteadyAction) == side)));
        foreach (var side in Sides)
            groups.Add(Group(Combined, side, scored.Where(s => Side(s.Snapshot.CombinedAction) == side)));
        groups.Add(Group(VersusCash, "Ahead of cash", scored.Where(s => s.Snapshot.AheadOfCash == true)));
        groups.Add(Group(VersusCash, "Behind cash", scored.Where(s => s.Snapshot.AheadOfCash == false)));

        return new SignalRecordDto
        {
            Since = snapshots.Count == 0 ? null : snapshots.Min(s => s.Date),
            Readings = snapshots.Count,
            Scored = scored.Count,
            HorizonDays = HorizonDays,
            Groups = groups,
        };
    }

    /// <summary>Buy for StrongBuy and Buy, Hold for Hold, Sell for StrongSell and Sell; null for anything else.</summary>
    public static string? Side(string? action) => action switch
    {
        "StrongBuy" or "Buy" => "Buy",
        "Hold" => "Hold",
        "StrongSell" or "Sell" => "Sell",
        _ => null,
    };

    private static SignalRecordGroupDto Group(string signal, string reading, IEnumerable<(SignalSnapshot Snapshot, decimal Move)> items)
    {
        var moves = items.Select(i => i.Move).ToList();
        return new SignalRecordGroupDto
        {
            Signal = signal,
            Reading = reading,
            Count = moves.Count,
            AverageMove = moves.Count == 0 ? null : Math.Round(moves.Average() * 100, 2),
            ShareUp = moves.Count == 0 ? null : Math.Round(100m * moves.Count(m => m > 0) / moves.Count, 0),
        };
    }

    // The price on the snapshot's date; -1 if that date isn't in the series
    private static int IndexOf(List<PriceDataPoint> prices, DateTime date)
    {
        int low = 0, high = prices.Count - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            var day = prices[middle].DateTime.Date;
            if (day == date.Date) return middle;
            if (day < date.Date) low = middle + 1;
            else high = middle - 1;
        }
        return -1;
    }
}
