using System.Collections.Concurrent;
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

    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan StaleCheckInterval = TimeSpan.FromSeconds(15);

    public ObservableCollection<SessionViewModel> Sessions { get; } = new();
    public event Action<SessionViewModel>? SessionUpdated;

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

    // Accumulate all files touched in the debounce window so that multiple concurrent watcher
    // threads don't lose each other's notifications.
    private readonly ConcurrentDictionary<string, byte> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (e.Name != null) _pendingChanges[e.Name] = 1;
        _dispatcher.BeginInvoke(() => ScheduleRefresh());
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (e.Name != null) _pendingChanges.TryRemove(e.Name, out _);
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

    internal void LoadAll()
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var activeIds = new HashSet<string>();

        // Snapshot and clear the pending set so a watcher event that fires during LoadAll lands
        // in the next refresh cycle, not this one.
        var changedFiles = _pendingChanges.Keys.ToArray();
        foreach (var k in changedFiles) _pendingChanges.TryRemove(k, out _);
        var changedSids = new HashSet<string>(
            changedFiles.Select(f => Path.GetFileNameWithoutExtension(f)!),
            StringComparer.OrdinalIgnoreCase);

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
                    UpdateOrAddSession(sid, data, changedSids.Contains(sid));
                }
                catch (Exception ex) { Log.Error($"Failed to parse session file: {file}", ex); }
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

    private void UpdateOrAddSession(string sid, SessionData data, bool wasChanged)
    {
        var existing = Sessions.FirstOrDefault(s => s.SessionId == sid);
        if (existing != null)
        {
            existing.Update(data);
            if (wasChanged)
                SessionUpdated?.Invoke(existing);
        }
        else
        {
            var vm = new SessionViewModel(sid, data);
            Sessions.Insert(0, vm);
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
        _debounceTimer = null;
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
    private long _totalInputTokens;
    private long _totalOutputTokens;

    public string Model { get => _model; private set { _model = value; OnPropertyChanged(nameof(Model)); } }
    public double ContextPercent { get => _contextPercent; private set { _contextPercent = value; OnPropertyChanged(nameof(ContextPercent)); OnPropertyChanged(nameof(ContextBarWidth)); OnPropertyChanged(nameof(ContextText)); } }
    public double Cost { get => _cost; private set { _cost = value; OnPropertyChanged(nameof(Cost)); OnPropertyChanged(nameof(CostText)); } }
    public long DurationMs { get => _durationMs; private set { _durationMs = value; OnPropertyChanged(nameof(DurationMs)); OnPropertyChanged(nameof(DurationText)); } }
    public int LinesAdded { get => _linesAdded; private set { _linesAdded = value; OnPropertyChanged(nameof(LinesAdded)); OnPropertyChanged(nameof(LinesText)); } }
    public int LinesRemoved { get => _linesRemoved; private set { _linesRemoved = value; OnPropertyChanged(nameof(LinesRemoved)); OnPropertyChanged(nameof(LinesText)); } }
    public long TotalInputTokens { get => _totalInputTokens; private set { _totalInputTokens = value; OnPropertyChanged(nameof(TotalInputTokens)); OnPropertyChanged(nameof(TokensText)); } }
    public long TotalOutputTokens { get => _totalOutputTokens; private set { _totalOutputTokens = value; OnPropertyChanged(nameof(TotalOutputTokens)); OnPropertyChanged(nameof(TokensText)); } }
    public double Rl5Percent { get => _rl5Percent; private set { _rl5Percent = value; OnPropertyChanged(nameof(Rl5Percent)); OnPropertyChanged(nameof(Rl5Text)); } }
    public double Rl7Percent { get => _rl7Percent; private set { _rl7Percent = value; OnPropertyChanged(nameof(Rl7Percent)); OnPropertyChanged(nameof(Rl7Text)); } }
    public long Rl5Reset { get => _rl5Reset; private set { _rl5Reset = value; OnPropertyChanged(nameof(Rl5Text)); } }
    public long Rl7Reset { get => _rl7Reset; private set { _rl7Reset = value; OnPropertyChanged(nameof(Rl7Text)); } }
    public long Timestamp { get => _timestamp; private set { _timestamp = value; OnPropertyChanged(nameof(Timestamp)); } }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(TabBackground));
            OnPropertyChanged(nameof(TabForeground));
            OnPropertyChanged(nameof(ActiveIndicator));
        }
    }

    private static readonly System.Windows.Media.SolidColorBrush TabBackgroundActive = FrozenBrush("#252545");
    private static readonly System.Windows.Media.SolidColorBrush TabBackgroundInactive = FrozenBrush("#16162a");
    private static readonly System.Windows.Media.SolidColorBrush TabForegroundActive = FrozenBrush("#c084fc");
    private static readonly System.Windows.Media.SolidColorBrush TabForegroundInactive = FrozenBrush("#555555");
    private static readonly System.Windows.Media.SolidColorBrush ActiveIndicatorOn = FrozenBrush("#4ade80");
    private static readonly System.Windows.Media.SolidColorBrush ActiveIndicatorOff = FrozenBrush("#333333");

    private static System.Windows.Media.SolidColorBrush FrozenBrush(string hex)
    {
        var b = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public System.Windows.Media.SolidColorBrush TabBackground => IsActive ? TabBackgroundActive : TabBackgroundInactive;
    public System.Windows.Media.SolidColorBrush TabForeground => IsActive ? TabForegroundActive : TabForegroundInactive;
    public System.Windows.Media.SolidColorBrush ActiveIndicator => IsActive ? ActiveIndicatorOn : ActiveIndicatorOff;

    public string ShortId => SessionId.Length > 8 ? SessionId[..8] : SessionId;
    public double ContextBarWidth => Math.Round(ContextPercent * 2.72, 1); // 272px max scaled
    public string ContextText => $"{Math.Round(ContextPercent)}%";
    public string CostText => $"${Cost:F2}";
    public string DurationText
    {
        get
        {
            var total = (int)(DurationMs / 1000);
            var d = total / 86400;
            var h = total % 86400 / 3600;
            var m = total % 3600 / 60;
            var s = total % 60;
            if (d > 0) return $"{d}d{h}h{m}m";
            if (h > 0) return $"{h}h{m}m";
            if (m > 0) return $"{m}m{s}s";
            return $"{s}s";
        }
    }
    public string LinesText => $"+{LinesAdded} / -{LinesRemoved}";
    public string TokensText => $"↓{FormatTokens(TotalInputTokens)}  ↑{FormatTokens(TotalOutputTokens)}";

    private static string FormatTokens(long n)
    {
        if (n >= 1_000_000) return $"{n / 1_000_000.0:F1}M";
        if (n >= 1_000) return $"{n / 1_000.0:F1}k";
        return n.ToString();
    }

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
            var d = (int)(diff / 86400);
            var h = (int)(diff % 86400 / 3600);
            var m = (int)(diff % 3600 / 60);
            if (d > 0) reset = $" ({d}d{h}h)";
            else if (h > 0) reset = $" ({h}h{m}m)";
            else reset = $" ({m}m)";
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
        TotalInputTokens = data.ContextWindow?.TotalInputTokens ?? 0;
        TotalOutputTokens = data.ContextWindow?.TotalOutputTokens ?? 0;
        Timestamp = data.Timestamp;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
