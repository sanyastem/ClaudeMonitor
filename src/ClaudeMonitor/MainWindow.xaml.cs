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

        _topmostTimer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) =>
        {
            Topmost = false;
            Topmost = true;
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
        noSessionsPanel.Visibility = hasSessions ? Visibility.Collapsed : Visibility.Visible;
        sessionContent.Visibility = hasSessions ? Visibility.Visible : Visibility.Collapsed;
        tabHeaders.Visibility = _watcher.Sessions.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        if (hasSessions && _activeSession == null)
            SelectSession(_watcher.Sessions[0]);
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
