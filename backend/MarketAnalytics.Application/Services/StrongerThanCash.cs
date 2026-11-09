using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Domain.Enums;
using MarketAnalytics.Domain.ValueObjects;

namespace MarketAnalytics.Application.Services;

/// <summary>
/// The "stronger than cash" rule: on the first trading day of each month, hold an instrument only if its return over
/// the past 12 months beat cash over the same 12 months, otherwise hold cash until the next month. The one rule in
/// this project that passed a test fixed in advance on both the development and held-back periods
/// (scripts/research/risk-rules-research.cs: Meezan Islamic Fund 2006-2026 kept similar returns with its worst fall
/// cut from about -60% to -25%). The app measures cash with the Meezan Cash Fund.
/// </summary>
public static class StrongerThanCash
{
    public const int LookbackMonths = 12;
    public const string CashSymbol = "MCF";

    // A price used by a check must be at most this old, so a feed that stopped can't quietly shorten the 12 months
    public const int MaxPriceAgeDays = 7;

    // Priced in dollars, so its return can't be compared with a rupee cash fund
    private static readonly HashSet<string> NotInRupees = new(StringComparer.OrdinalIgnoreCase) { "GOLDINT" };

    /// <summary>
    /// Whether the check applies: rupee-priced instruments with signals. Not cash and income funds (they are the
    /// cash), currencies, or instruments priced in dollars.
    /// </summary>
    public static bool AppliesTo(Instrument instrument) =>
        !instrument.IsLowRisk && instrument.Type != InstrumentType.Forex && !NotInRupees.Contains(instrument.Symbol);

    /// <summary>One monthly check: the instrument's and cash's returns over the previous 12 months, as fractions.</summary>
    public record Check(DateTime Date, decimal Return, decimal CashReturn)
    {
        public bool AheadOfCash => Return > CashReturn;
    }

    /// <summary>
    /// The check on the first trading day of every month that has 12 months of history for both series, oldest first.
    /// A month is skipped when a price it needs is more than <see cref="MaxPriceAgeDays"/> days older than the date it
    /// stands for (a gap in the data). Both lists must be sorted oldest first.
    /// </summary>
    public static List<Check> MonthlyChecks(IReadOnlyList<PriceDataPoint> prices, IReadOnlyList<PriceDataPoint> cash)
    {
        var result = new List<Check>();
        if (prices.Count == 0 || cash.Count == 0)
            return result;

        for (int i = 1; i < prices.Count; i++)
        {
            var date = prices[i].DateTime.Date;
            if (date.Month == prices[i - 1].DateTime.Date.Month)
                continue;

            var back = date.AddMonths(-LookbackMonths);
            var then = IndexOnOrBefore(prices, back);
            var cashNow = IndexOnOrBefore(cash, date);
            var cashThen = IndexOnOrBefore(cash, back);
            if (then < 0 || cashThen < 0 || cashNow < 0 || prices[then].Price <= 0 || cash[cashThen].Price <= 0)
                continue;
            if (TooOld(prices[then], back) || TooOld(cash[cashThen], back) || TooOld(cash[cashNow], date))
                continue;

            result.Add(new Check(
                prices[i].DateTime,
                prices[i].Price / prices[then].Price - 1,
                cash[cashNow].Price / cash[cashThen].Price - 1));
        }
        return result;
    }

    /// <summary>The latest monthly check and since when its answer has held; null without 12 months of history.</summary>
    public static CashTestDto? Evaluate(IReadOnlyList<PriceDataPoint> prices, IReadOnlyList<PriceDataPoint> cash)
    {
        var checks = MonthlyChecks(prices, cash);
        if (checks.Count == 0)
            return null;

        var latest = checks[^1];
        var start = checks.Count - 1;
        while (start > 0 && checks[start - 1].AheadOfCash == latest.AheadOfCash)
            start--;

        // The answer follows the rounded figures shown with it, so the two can't contradict each other
        var ret = Math.Round(latest.Return * 100, 2);
        var cashRet = Math.Round(latest.CashReturn * 100, 2);
        return new CashTestDto
        {
            AheadOfCash = ret > cashRet,
            Return12M = ret,
            CashReturn12M = cashRet,
            CheckedOn = latest.Date,
            Since = start == 0 ? null : checks[start].Date,
        };
    }

    private static bool TooOld(PriceDataPoint price, DateTime date) => (date - price.DateTime.Date).TotalDays > MaxPriceAgeDays;

    private static int IndexOnOrBefore(IReadOnlyList<PriceDataPoint> prices, DateTime date)
    {
        int low = 0, high = prices.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (prices[middle].DateTime.Date <= date) low = middle + 1;
            else high = middle;
        }
        return low - 1;
    }
}
