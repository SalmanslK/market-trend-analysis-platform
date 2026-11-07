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

        var changes = AlertService.Changes(history, since: DateTime.MinValue);

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

        var combined = AlertService.Changes(history, DateTime.MinValue).Single(c => c.Signal == "Combined");

        Assert.False(combined.Important);
    }

    [Fact]
    public void Changes_LeavesOutDaysBeforeTheWindow()
    {
        var history = new[] { Day(0, "Hold", "Hold", "Hold", "Down"), Day(1, "Sell", "Hold", "Hold", "Down"), Day(2, "Hold", "Hold", "Hold", "Down") };

        var changes = AlertService.Changes(history, since: history[2].Date);

        Assert.Equal("Sell", Assert.Single(changes).From);
    }
}
