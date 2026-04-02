using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;
using ClaudeMonitor.Services;

namespace ClaudeMonitor;

public partial class MainWindow : Window
{
    private readonly SessionWatcher _watcher;
    private readonly PositionManager _positionManager;
    private readonly DispatcherTimer _topmostTimer;

    public MainWindow()
    {
        InitializeComponent();

        _positionManager = new PositionManager();
        _watcher = new SessionWatcher(Dispatcher);

        DataContext = _watcher;
        sessionsList.ItemsSource = _watcher.Sessions;

        _watcher.Sessions.CollectionChanged += OnSessionsChanged;
        UpdateNoSessionsVisibility();

        _positionManager.ApplyDefaultPosition(this);

        // Re-assert topmost every 30s and check screen bounds
        _topmostTimer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) =>
        {
            Topmost = false;
            Topmost = true;
            _positionManager.EnsureOnScreen(this);
        }, Dispatcher);
        _topmostTimer.Start();
    }

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateNoSessionsVisibility();
    }

    private void UpdateNoSessionsVisibility()
    {
        tbNoSessions.Visibility = _watcher.Sessions.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Window_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
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
