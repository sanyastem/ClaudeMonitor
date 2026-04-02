using System.Text.Json;
using ClaudeMonitor.Models;

namespace ClaudeMonitor.Tests;

public class SessionDataTests
{
    private const string FullJson = """
    {
        "session_id": "abc-123-def",
        "model": { "id": "claude-opus-4-6", "display_name": "Opus 4.6 (1M context)" },
        "context_window": { "used_percentage": 35.2, "context_window_size": 1000000 },
        "cost": {
            "total_cost_usd": 12.15,
            "total_duration_ms": 3881000,
            "total_lines_added": 2227,
            "total_lines_removed": 425
        },
        "rate_limits": {
            "five_hour": { "used_percentage": 20.0, "resets_at": 1743620000 },
            "seven_day": { "used_percentage": 13.0, "resets_at": 1744050000 }
        },
        "_ts": 1743600000000
    }
    """;

    [Fact]
    public void Deserialize_FullJson_AllFieldsParsed()
    {
        var data = JsonSerializer.Deserialize<SessionData>(FullJson);

        Assert.NotNull(data);
        Assert.Equal("abc-123-def", data.SessionId);
        Assert.Equal("Opus 4.6 (1M context)", data.Model?.DisplayName);
        Assert.Equal("claude-opus-4-6", data.Model?.Id);
        Assert.Equal(35.2, data.ContextWindow?.UsedPercentage);
        Assert.Equal(1_000_000, data.ContextWindow?.ContextWindowSize);
        Assert.Equal(12.15, data.Cost?.TotalCostUsd);
        Assert.Equal(3_881_000, data.Cost?.TotalDurationMs);
        Assert.Equal(2227, data.Cost?.TotalLinesAdded);
        Assert.Equal(425, data.Cost?.TotalLinesRemoved);
        Assert.Equal(20.0, data.RateLimits?.FiveHour?.UsedPercentage);
        Assert.Equal(1743620000, data.RateLimits?.FiveHour?.ResetsAt);
        Assert.Equal(13.0, data.RateLimits?.SevenDay?.UsedPercentage);
        Assert.Equal(1744050000, data.RateLimits?.SevenDay?.ResetsAt);
        Assert.Equal(1743600000000, data.Timestamp);
    }

    [Fact]
    public void Deserialize_MinimalJson_NullableFieldsAreNull()
    {
        var json = """{ "session_id": "x", "_ts": 100 }""";
        var data = JsonSerializer.Deserialize<SessionData>(json);

        Assert.NotNull(data);
        Assert.Equal("x", data.SessionId);
        Assert.Null(data.Model);
        Assert.Null(data.ContextWindow);
        Assert.Null(data.Cost);
        Assert.Null(data.RateLimits);
    }

    [Fact]
    public void Deserialize_NoRateLimits_RateLimitsNull()
    {
        var json = """
        {
            "session_id": "test",
            "model": { "display_name": "Sonnet" },
            "context_window": { "used_percentage": 10.0 },
            "cost": { "total_cost_usd": 0.5 },
            "_ts": 200
        }
        """;
        var data = JsonSerializer.Deserialize<SessionData>(json);

        Assert.NotNull(data);
        Assert.Equal("Sonnet", data.Model?.DisplayName);
        Assert.Null(data.RateLimits);
    }
}
