using System.Text.Json.Serialization;

namespace ClaudeMonitor.Models;

public sealed class SessionData
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("model")]
    public ModelInfo? Model { get; set; }

    [JsonPropertyName("context_window")]
    public ContextWindow? ContextWindow { get; set; }

    [JsonPropertyName("cost")]
    public CostInfo? Cost { get; set; }

    [JsonPropertyName("rate_limits")]
    public RateLimits? RateLimits { get; set; }

    [JsonPropertyName("_ts")]
    public long Timestamp { get; set; }
}

public sealed class ModelInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}

public sealed class ContextWindow
{
    [JsonPropertyName("used_percentage")]
    public double UsedPercentage { get; set; }

    [JsonPropertyName("context_window_size")]
    public long ContextWindowSize { get; set; }
}

public sealed class CostInfo
{
    [JsonPropertyName("total_cost_usd")]
    public double TotalCostUsd { get; set; }

    [JsonPropertyName("total_duration_ms")]
    public long TotalDurationMs { get; set; }

    [JsonPropertyName("total_lines_added")]
    public int TotalLinesAdded { get; set; }

    [JsonPropertyName("total_lines_removed")]
    public int TotalLinesRemoved { get; set; }
}

public sealed class RateLimits
{
    [JsonPropertyName("five_hour")]
    public RateLimitWindow? FiveHour { get; set; }

    [JsonPropertyName("seven_day")]
    public RateLimitWindow? SevenDay { get; set; }
}

public sealed class RateLimitWindow
{
    [JsonPropertyName("used_percentage")]
    public double UsedPercentage { get; set; }

    [JsonPropertyName("resets_at")]
    public long ResetsAt { get; set; }
}
