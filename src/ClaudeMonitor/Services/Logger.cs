using System.IO;

namespace ClaudeMonitor.Services;

public static class Log
{
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", "claude-monitor.log");

    private static readonly object Lock = new();
    private const long MaxSize = 512 * 1024; // 512KB

    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message) => Write("ERROR", message);
    public static void Error(string message, Exception ex) => Write("ERROR", $"{message}: {ex.Message}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Lock)
            {
                // Rotate if too large
                if (File.Exists(LogFile) && new FileInfo(LogFile).Length > MaxSize)
                {
                    try
                    {
                        var backup = LogFile + ".old";
                        File.Delete(backup);
                        File.Move(LogFile, backup);
                    }
                    catch { /* backup may be locked by another process */ }
                }

                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFile, line);
            }
        }
        catch { }
    }
}
