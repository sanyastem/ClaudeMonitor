using ClaudeMonitor.Services;

namespace ClaudeMonitor.Tests;

public class SessionWatcherCalcTests
{
    [Theory]
    [InlineData(0L, 0L)]
    [InlineData(-1L, 0L)]
    [InlineData(1_700_000_000L, 1_700_000_000L)]              // seconds — passthrough
    [InlineData(999_999_999_999L, 999_999_999_999L)]          // just under threshold — still seconds
    [InlineData(1_000_000_000_001L, 1_000_000_000L)]          // just over threshold — interpreted as ms
    [InlineData(1_700_000_000_000L, 1_700_000_000L)]          // ms typical value
    public void NormalizeEpochSeconds_HandlesBothUnits(long input, long expected)
    {
        Assert.Equal(expected, SessionViewModel.NormalizeEpochSeconds(input));
    }
}
