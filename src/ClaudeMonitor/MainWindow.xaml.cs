using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ClaudeMonitor.Services;

namespace ClaudeMonitor;

public partial class MainWindow : Window
{
    private readonly SessionWatcher _watcher;
    private readonly PositionManager _positionManager;
    private readonly DispatcherTimer _topmostTimer;
    private SessionViewModel? _activeSession;

    public bool AlwaysVisible
    {
        get => _positionManager.AlwaysVisible;
        set
        {
            _positionManager.AlwaysVisible = value;
            UpdateView();
        }
    }

    public bool ShowIdleLimits
    {
        get => _positionManager.ShowIdleLimits;
        set
        {
            _positionManager.ShowIdleLimits = value;
            UpdateView();
        }
    }

    public bool AlwaysOnTop
    {
        get => _positionManager.AlwaysOnTop;
        set
        {
            _positionManager.AlwaysOnTop = value;
            Topmost = value;
        }
    }

    public MainWindow()
    {
        InitializeComponent();

        _positionManager = new PositionManager();
        _watcher = new SessionWatcher(Dispatcher);

        tabHeaders.ItemsSource = _watcher.Sessions;

        _watcher.Sessions.CollectionChanged += OnSessionsChanged;
        _watcher.SessionUpdated += OnSessionUpdated;
        UpdateView();

        _positionManager.ApplyDefaultPosition(this);

        Topmost = _positionManager.AlwaysOnTop;

        _topmostTimer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) =>
        {
            if (_positionManager.AlwaysOnTop)
            {
                Topmost = false;
                Topmost = true;
            }
            _positionManager.EnsureOnScreen(this);
        }, Dispatcher);
        _topmostTimer.Start();
    }

    public void ApplySetupPosition(WidgetPosition pos)
    {
        var (left, top) = SetupWindow.CalculatePosition(pos, Width, 250);
        Left = left;
        Top = top;
        _positionManager.Save(Left, Top);
    }

    private void OnSessionUpdated(SessionViewModel session)
    {
        if (_activeSession != session)
            SelectSession(session);

        // Save last known rate limits
        if (session.Rl5Percent >= 0 || session.Rl7Percent >= 0)
            _positionManager.SaveLastLimits(session.Rl5Percent, session.Rl5Reset, session.Rl7Percent, session.Rl7Reset);
    }

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_activeSession != null && !_watcher.Sessions.Contains(_activeSession))
            _activeSession = null;

        if (_activeSession == null && _watcher.Sessions.Count > 0)
            SelectSession(_watcher.Sessions[0]);

        if (e?.Action == NotifyCollectionChangedAction.Add && e.NewItems?[0] is SessionViewModel newSession)
            SelectSession(newSession);

        UpdateView();
    }

    private void SelectSession(SessionViewModel session)
    {
        if (_activeSession != null)
            _activeSession.IsActive = false;

        _activeSession = session;
        _activeSession.IsActive = true;
        contentPanel.DataContext = _activeSession;
    }

    private void UpdateView()
    {
        var hasSessions = _watcher.Sessions.Count > 0;
        sessionContent.Visibility = hasSessions ? Visibility.Visible : Visibility.Collapsed;
        tabHeaders.Visibility = _watcher.Sessions.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        if (AlwaysVisible)
        {
            noSessionsPanel.Visibility = hasSessions ? Visibility.Collapsed : Visibility.Visible;
            if (!IsVisible)
            {
                Show();
                Topmost = true;
            }
        }
        else
        {
            noSessionsPanel.Visibility = Visibility.Collapsed;
            if (!hasSessions)
                Hide();
            else if (!IsVisible)
            {
                Show();
                Topmost = true;
            }
        }

        if (hasSessions && _activeSession == null)
            SelectSession(_watcher.Sessions[0]);

        UpdateIdleLimits(!hasSessions);
    }

    private void UpdateIdleLimits(bool show)
    {
        if (!show || !_positionManager.ShowIdleLimits)
        {
            idleLimitsGrid.Visibility = Visibility.Collapsed;
            return;
        }

        var limits = _positionManager.LoadLastLimits();
        if (limits == null)
        {
            idleLimitsGrid.Visibility = Visibility.Collapsed;
            return;
        }

        idleLimitsGrid.Visibility = Visibility.Visible;
        var (rl5, rl5Reset, rl7, rl7Reset) = limits.Value;

        tbIdleRl5.Text = FormatRateLimit(rl5, rl5Reset);
        tbIdleRl5.Foreground = GetLimitBrush(rl5);
        tbIdleRl7.Text = FormatRateLimit(rl7, rl7Reset);
        tbIdleRl7.Foreground = GetLimitBrush(rl7);
    }

    private static string FormatRateLimit(double pct, long resetEpoch)
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

    private static System.Windows.Media.SolidColorBrush GetLimitBrush(double pct)
    {
        var hex = pct < 50 ? "#4ade80" : pct < 80 ? "#facc15" : "#f87171";
        return new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
    }

    private void Tab_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SessionViewModel session)
        {
            SelectSession(session);
            e.Handled = true;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _positionManager.Save(Left, Top);
    }

    protected override void OnClosed(EventArgs e)
    {
        _positionManager.Save(Left, Top);
        _topmostTimer.Stop();
        _watcher.Dispose();
        base.OnClosed(e);
    }
}
