using ClaudeMonitor.Models;
using ClaudeMonitor.Services;

namespace ClaudeMonitor.Tests;

public class SessionViewModelTests
{
    private static SessionData MakeData(
        string model = "Opus",
        double ctx = 35.0,
        double cost = 1.5,
        long durationMs = 125000,
        int added = 10,
        int removed = 3,
        double rl5 = 20.0,
        long rl5Reset = 0,
        double rl7 = 13.0,
        long rl7Reset = 0)
    {
        return new SessionData
        {
            SessionId = "test-session",
            Model = new ModelInfo { DisplayName = model },
            ContextWindow = new ContextWindow { UsedPercentage = ctx },
            Cost = new CostInfo
            {
                TotalCostUsd = cost,
                TotalDurationMs = durationMs,
                TotalLinesAdded = added,
                TotalLinesRemoved = removed
            },
            RateLimits = new RateLimits
            {
                FiveHour = new RateLimitWindow { UsedPercentage = rl5, ResetsAt = rl5Reset },
                SevenDay = new RateLimitWindow { UsedPercentage = rl7, ResetsAt = rl7Reset }
            },
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    [Fact]
    public void ShortId_TruncatesTo8Chars()
    {
        var vm = new SessionViewModel("abcdefghijklmnop", MakeData());
        Assert.Equal("abcdefgh", vm.ShortId);
    }

    [Fact]
    public void ShortId_ShortIdUnchanged()
    {
        var vm = new SessionViewModel("abc", MakeData());
        Assert.Equal("abc", vm.ShortId);
    }

    [Fact]
    public void ContextText_RoundsPercentage()
    {
        var vm = new SessionViewModel("s1", MakeData(ctx: 35.7));
        Assert.Equal("36%", vm.ContextText);
    }

    [Fact]
    public void CostText_FormatsToTwoDecimals()
    {
        var vm = new SessionViewModel("s1", MakeData(cost: 12.156));
        Assert.Equal("$12.16", vm.CostText);
    }

    [Fact]
    public void DurationText_MinutesAndSeconds()
    {
        var vm = new SessionViewModel("s1", MakeData(durationMs: 125000));
        Assert.Equal("2m5s", vm.DurationText);
    }

    [Fact]
    public void DurationText_SecondsOnly()
    {
        var vm = new SessionViewModel("s1", MakeData(durationMs: 45000));
        Assert.Equal("45s", vm.DurationText);
    }

    [Fact]
    public void DurationText_HoursMinutes()
    {
        var vm = new SessionViewModel("s1", MakeData(durationMs: 4080000)); // 1h8m
        Assert.Equal("1h8m", vm.DurationText);
    }

    [Fact]
    public void DurationText_DaysHoursMinutes()
    {
        var vm = new SessionViewModel("s1", MakeData(durationMs: 93600000)); // 1d2h0m
        Assert.Equal("1d2h0m", vm.DurationText);
    }

    [Fact]
    public void LinesText_Format()
    {
        var vm = new SessionViewModel("s1", MakeData(added: 2227, removed: 425));
        Assert.Equal("+2227 / -425", vm.LinesText);
    }

    [Fact]
    public void RateLimits_NoData_ShowsNA()
    {
        var data = new SessionData
        {
            Model = new ModelInfo { DisplayName = "Opus" },
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        var vm = new SessionViewModel("s1", data);
        Assert.Equal("n/a", vm.Rl5Text);
        Assert.Equal("n/a", vm.Rl7Text);
    }

    [Fact]
    public void RateLimits_WithPercentage_ShowsValue()
    {
        var vm = new SessionViewModel("s1", MakeData(rl5: 20.4));
        Assert.StartsWith("20%", vm.Rl5Text);
    }

    [Fact]
    public void Update_ChangesAllProperties()
    {
        var vm = new SessionViewModel("s1", MakeData(model: "Opus", cost: 1.0));
        Assert.Equal("Opus", vm.Model);
        Assert.Equal("$1.00", vm.CostText);

        vm.Update(MakeData(model: "Sonnet", cost: 5.5));
        Assert.Equal("Sonnet", vm.Model);
        Assert.Equal("$5.50", vm.CostText);
    }

    [Fact]
    public void Update_FiresPropertyChanged()
    {
        var vm = new SessionViewModel("s1", MakeData());
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.Update(MakeData(cost: 99.0));

        Assert.Contains("Cost", changed);
        Assert.Contains("CostText", changed);
        Assert.Contains("Model", changed);
    }

    [Fact]
    public void IsActive_DefaultFalse()
    {
        var vm = new SessionViewModel("s1", MakeData());
        Assert.False(vm.IsActive);
    }

    [Fact]
    public void IsActive_ChangesTabProperties()
    {
        var vm = new SessionViewModel("s1", MakeData());
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.IsActive = true;

        Assert.True(vm.IsActive);
        Assert.Contains("TabBackground", changed);
        Assert.Contains("TabForeground", changed);
        Assert.Contains("ActiveIndicator", changed);
    }
}
