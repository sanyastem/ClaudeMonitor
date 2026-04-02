using System.IO;
using ClaudeMonitor.Services;

namespace ClaudeMonitor.Tests;

public class PositionManagerTests
{
    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var pm = new PositionManager();
        var testFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "widget-pos-test.json");

        try
        {
            // Use reflection to set the file path for testing
            var field = typeof(PositionManager).GetField("_posFile",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field!.SetValue(pm, testFile);

            pm.Save(100.5, 200.3);
            var pos = pm.Load();

            Assert.NotNull(pos);
            Assert.Equal(100.5, pos.Value.left);
            Assert.Equal(200.3, pos.Value.top);
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Fact]
    public void Load_NoFile_ReturnsNull()
    {
        var pm = new PositionManager();
        var field = typeof(PositionManager).GetField("_posFile",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field!.SetValue(pm, Path.Combine(Path.GetTempPath(), "nonexistent-widget-pos.json"));

        var pos = pm.Load();
        Assert.Null(pos);
    }
}
