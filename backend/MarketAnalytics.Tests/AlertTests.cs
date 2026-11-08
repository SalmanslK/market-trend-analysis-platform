using MarketAnalytics.Application.Services;

namespace MarketAnalytics.Tests;

public class AlertTests
{
    private static DailySignals Day(int day, string technical, string news, string combined, string trend) =>
        new(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(day), technical, news, combined, trend);

    [Fact]
    public void Changes_ListsEachSignalThatChangedFromOneDayToTheNext()
    {
        var history = new[]
        {
            Day(0, "Hold", "Buy", "Hold", "Down"),
            Day(1, "Buy", "Buy", "Buy", "Down"),
            Day(2, "StrongBuy", "Buy", "Buy", "Up"),
        };

        var changes = AlertService.Changes(history, since: DateTime.MinValue, steady: false);

        Assert.Equal(new[] { "Combined:Hold>Buy", "Technical:Hold>Buy", "Trend:Down>Up" },
            changes.Select(c => $"{c.Signal}:{c.From}>{c.To}"));
        Assert.Equal(history[2].Date, changes[^1].Date);
        // Buy to Strong Buy is the same side, so it isn't listed
        Assert.DoesNotContain(changes, c => c.To == "StrongBuy");
        Assert.Equal(new[] { true, false, true }, changes.Select(c => c.Important));
    }

    [Fact]
    public void Changes_MarksAMoveToHoldAsNotImportant()
    {
        var history = new[] { Day(0, "Buy", "Buy", "Buy", "Up"), Day(1, "Buy", "Hold", "Hold", "Up") };

        var combined = AlertService.Changes(history, DateTime.MinValue, steady: false).Single(c => c.Signal == "Combined");

        Assert.False(combined.Important);
    }

    [Fact]
    public void Changes_LeavesOutDaysBeforeTheWindow()
    {
        var history = new[] { Day(0, "Hold", "Hold", "Hold", "Down"), Day(1, "Sell", "Hold", "Hold", "Down"), Day(2, "Hold", "Hold", "Hold", "Down") };

        var changes = AlertService.Changes(history, since: history[2].Date, steady: false);

        Assert.Equal("Sell", Assert.Single(changes).From);
    }
}

public class SteadySignalTests
{
    [Fact]
    public void SteadyScores_AverageTheLast20DailyScores()
    {
        // 20 days at +5 (all votes up), then 10 days at -5
        var daily = Enumerable.Repeat(5, 20).Concat(Enumerable.Repeat(-5, 10)).ToList();

        var steady = SignalRules.SteadyScores(daily, 5);

        Assert.Equal(100, steady[19], 6);
        // Day 30: 10 days of +5 and 10 of -5 in the window
        Assert.Equal(0, steady[29], 6);
        // Early days average what there is so far
        Assert.Equal(100, steady[0], 6);
    }

    [Fact]
    public void SteadyScores_IgnoreOneDayFlips()
    {
        // Mostly +2 with a single -1 day: the daily signal says Sell that day, the steady one stays Buy
        var daily = Enumerable.Repeat(2, 19).Append(-1).ToList();

        var steady = SignalRules.SteadyScores(daily, 5);

        Assert.Equal("Sell", SignalRules.ActionFor(daily[^1]));
        Assert.Equal("Buy", SignalRules.ActionForScaledScore(steady[^1]));
    }

    [Fact]
    public void CombinedScaledScore_NeedsBothToAgree()
    {
        Assert.Equal(45, SignalRules.CombinedScaledScore(30, 60, 5));
        Assert.Equal(0, SignalRules.CombinedScaledScore(30, -60, 5));
        Assert.Equal(0, SignalRules.CombinedScaledScore(10, 60, 5));
        Assert.Equal(0, SignalRules.CombinedScaledScore(30, 60, 2));
    }

    [Fact]
    public void Changes_FollowTheChosenTechnicalVersion()
    {
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var history = new[]
        {
            new DailySignals(start, "Hold", "Buy", "Hold", "Up", "Buy", "Buy"),
            new DailySignals(start.AddDays(1), "Sell", "Buy", "Hold", "Up", "Buy", "Buy"),
        };

        Assert.Contains(AlertService.Changes(history, DateTime.MinValue, steady: false), c => c.Signal == "Technical");
        Assert.Empty(AlertService.Changes(history, DateTime.MinValue, steady: true));
    }
}
