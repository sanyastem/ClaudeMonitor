using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using ClaudeMonitor.Models;

namespace ClaudeMonitor.Services;

public sealed class SessionWatcher : IDisposable
{
    private readonly string _sessionsDir;
    private readonly FileSystemWatcher _watcher;
    private readonly DispatcherTimer _staleTimer;
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<string, SessionData> _sessions = new();
    private readonly object _lock = new();
    private DateTime _lastChange = DateTime.MinValue;
    private DispatcherTimer? _debounceTimer;

    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan StaleCheckInterval = TimeSpan.FromSeconds(30);

    public ObservableCollection<SessionViewModel> Sessions { get; } = new();

    public SessionWatcher(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _sessionsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "widget-sessions");

        Directory.CreateDirectory(_sessionsDir);

        _watcher = new FileSystemWatcher(_sessionsDir, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileDeleted;

        _staleTimer = new DispatcherTimer(StaleCheckInterval, DispatcherPriority.Background, (_, _) => CleanStale(), _dispatcher);
        _staleTimer.Start();

        LoadAll();
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _dispatcher.BeginInvoke(() => ScheduleRefresh());
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        _dispatcher.BeginInvoke(() => ScheduleRefresh());
    }

    private void ScheduleRefresh()
    {
        _debounceTimer?.Stop();
        _debounceTimer = new DispatcherTimer(DebounceInterval, DispatcherPriority.Normal, (_, _) =>
        {
            _debounceTimer?.Stop();
            LoadAll();
        }, _dispatcher);
        _debounceTimer.Start();
    }

    private void LoadAll()
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var activeIds = new HashSet<string>();

        try
        {
            foreach (var file in Directory.GetFiles(_sessionsDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var data = JsonSerializer.Deserialize<SessionData>(json);
                    if (data == null) continue;

                    var sid = Path.GetFileNameWithoutExtension(file);
                    var ageMin = (nowMs - data.Timestamp) / 60000.0;

                    if (ageMin > StaleThreshold.TotalMinutes)
                    {
                        TryDelete(file);
                        continue;
                    }

                    activeIds.Add(sid);
                    UpdateOrAddSession(sid, data);
                }
                catch { /* skip corrupt files */ }
            }
        }
        catch { /* dir may not exist yet */ }

        // Remove sessions no longer on disk
        for (int i = Sessions.Count - 1; i >= 0; i--)
        {
            if (!activeIds.Contains(Sessions[i].SessionId))
                Sessions.RemoveAt(i);
        }
    }

    private void UpdateOrAddSession(string sid, SessionData data)
    {
        var existing = Sessions.FirstOrDefault(s => s.SessionId == sid);
        if (existing != null)
        {
            existing.Update(data);
        }
        else
        {
            Sessions.Insert(0, new SessionViewModel(sid, data));
        }
    }

    private void CleanStale()
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (int i = Sessions.Count - 1; i >= 0; i--)
        {
            if ((nowMs - Sessions[i].Timestamp) / 60000.0 > StaleThreshold.TotalMinutes)
            {
                var file = Path.Combine(_sessionsDir, Sessions[i].SessionId + ".json");
                TryDelete(file);
                Sessions.RemoveAt(i);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _staleTimer.Stop();
        _debounceTimer?.Stop();
    }
}

public sealed class SessionViewModel : System.ComponentModel.INotifyPropertyChanged
{
    public string SessionId { get; }

    private string _model = "?";
    private double _contextPercent;
    private double _cost;
    private long _durationMs;
    private int _linesAdded;
    private int _linesRemoved;
    private double _rl5Percent = -1;
    private double _rl7Percent = -1;
    private long _rl5Reset;
    private long _rl7Reset;
    private long _timestamp;

    public string Model { get => _model; private set { _model = value; OnPropertyChanged(nameof(Model)); } }
    public double ContextPercent { get => _contextPercent; private set { _contextPercent = value; OnPropertyChanged(nameof(ContextPercent)); OnPropertyChanged(nameof(ContextBarWidth)); OnPropertyChanged(nameof(ContextText)); } }
    public double Cost { get => _cost; private set { _cost = value; OnPropertyChanged(nameof(Cost)); OnPropertyChanged(nameof(CostText)); } }
    public long DurationMs { get => _durationMs; private set { _durationMs = value; OnPropertyChanged(nameof(DurationMs)); OnPropertyChanged(nameof(DurationText)); } }
    public int LinesAdded { get => _linesAdded; private set { _linesAdded = value; OnPropertyChanged(nameof(LinesAdded)); OnPropertyChanged(nameof(LinesText)); } }
    public int LinesRemoved { get => _linesRemoved; private set { _linesRemoved = value; OnPropertyChanged(nameof(LinesRemoved)); OnPropertyChanged(nameof(LinesText)); } }
    public double Rl5Percent { get => _rl5Percent; private set { _rl5Percent = value; OnPropertyChanged(nameof(Rl5Percent)); OnPropertyChanged(nameof(Rl5Text)); } }
    public double Rl7Percent { get => _rl7Percent; private set { _rl7Percent = value; OnPropertyChanged(nameof(Rl7Percent)); OnPropertyChanged(nameof(Rl7Text)); } }
    public long Rl5Reset { get => _rl5Reset; private set { _rl5Reset = value; OnPropertyChanged(nameof(Rl5Text)); } }
    public long Rl7Reset { get => _rl7Reset; private set { _rl7Reset = value; OnPropertyChanged(nameof(Rl7Text)); } }
    public long Timestamp { get => _timestamp; private set { _timestamp = value; OnPropertyChanged(nameof(Timestamp)); } }

    public string ShortId => SessionId.Length > 8 ? SessionId[..8] : SessionId;
    public double ContextBarWidth => Math.Round(ContextPercent * 2.72, 1); // 272px max scaled
    public string ContextText => $"{Math.Round(ContextPercent)}%";
    public string CostText => $"${Cost:F2}";
    public string DurationText
    {
        get
        {
            var s = (int)(DurationMs / 1000);
            var m = s / 60; s %= 60;
            return m > 0 ? $"{m}m{s}s" : $"{s}s";
        }
    }
    public string LinesText => $"+{LinesAdded} / -{LinesRemoved}";

    public string Rl5Text => FormatRl(Rl5Percent, Rl5Reset);
    public string Rl7Text => FormatRl(Rl7Percent, Rl7Reset);

    private static string FormatRl(double pct, long resetEpoch)
    {
        if (pct < 0) return "n/a";
        var v = (int)Math.Round(pct);
        var reset = "";
        if (resetEpoch > 0)
        {
            var diff = Math.Max(0, resetEpoch - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var h = (int)(diff / 3600);
            var m = (int)(diff % 3600 / 60);
            reset = h > 0 ? $" ({h}h{m}m)" : $" ({m}m)";
        }
        return $"{v}%{reset}";
    }

    public SessionViewModel(string sessionId, SessionData data)
    {
        SessionId = sessionId;
        Update(data);
    }

    public void Update(SessionData data)
    {
        Model = data.Model?.DisplayName ?? "?";
        ContextPercent = data.ContextWindow?.UsedPercentage ?? 0;
        Cost = data.Cost?.TotalCostUsd ?? 0;
        DurationMs = data.Cost?.TotalDurationMs ?? 0;
        LinesAdded = data.Cost?.TotalLinesAdded ?? 0;
        LinesRemoved = data.Cost?.TotalLinesRemoved ?? 0;
        Rl5Percent = data.RateLimits?.FiveHour?.UsedPercentage ?? -1;
        Rl7Percent = data.RateLimits?.SevenDay?.UsedPercentage ?? -1;
        Rl5Reset = data.RateLimits?.FiveHour?.ResetsAt ?? 0;
        Rl7Reset = data.RateLimits?.SevenDay?.ResetsAt ?? 0;
        Timestamp = data.Timestamp;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
