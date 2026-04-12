using System.IO;

namespace ClaudeMonitor.Services;

public static class Log
{
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", "widget.log");

    private static readonly string LegacyLogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", "claude-monitor.log");

    private static readonly object Lock = new();
    private const long MaxSize = 512 * 1024; // 512KB per file
    private const int Backups = 3;

    private static readonly bool DebugEnabled =
        Environment.GetEnvironmentVariable("CLAUDEMONITOR_DEBUG") == "1" ||
        File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "widget-debug"));

    static Log()
    {
        // One-shot migration so historical entries are preserved.
        try
        {
            if (!File.Exists(LogFile) && File.Exists(LegacyLogFile))
                File.Move(LegacyLogFile, LogFile);
        }
        catch { }
    }

    public static void Debug(string message) { if (DebugEnabled) Write("DEBUG", message); }
    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message) => Write("ERROR", message);
    public static void Error(string message, Exception? ex) => Write("ERROR", ex == null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Lock)
            {
                if (File.Exists(LogFile) && new FileInfo(LogFile).Length > MaxSize)
                    Rotate();

                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFile, line);
            }
        }
        catch { }
    }

    private static void Rotate()
    {
        try
        {
            // widget.log.3 → discarded; widget.log.N → widget.log.(N+1)
            for (var i = Backups; i >= 1; i--)
            {
                var src = i == 1 ? LogFile : $"{LogFile}.{i - 1}";
                var dst = $"{LogFile}.{i}";
                if (File.Exists(src))
                {
                    try { if (File.Exists(dst)) File.Delete(dst); } catch { }
                    try { File.Move(src, dst); } catch { }
                }
            }
        }
        catch { }
    }
}
