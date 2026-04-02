# Claude Monitor

A lightweight WPF desktop widget for Windows that displays real-time Claude Code session metrics.

## Features

- **Multi-session support** — each Claude Code session is displayed as a separate card
- **Real-time updates** — uses FileSystemWatcher for instant data refresh
- **Always on top** — stays visible over all windows
- **Monitor-aware** — automatically repositions when displays change
- **System tray** — minimize to tray, toggle visibility, autostart with Windows
- **Position memory** — remembers where you placed it
- **Low memory footprint** — ~15-20MB RAM, event-driven (no polling)
- **Single instance** — prevents duplicate processes via Mutex
- **Stale session cleanup** — sessions inactive for 10+ minutes auto-remove

## Session Metrics

Each session card shows:

| Metric | Description |
|--------|-------------|
| Model | Active model name (Opus, Sonnet, etc.) |
| Context | Visual progress bar + percentage |
| Cost | Session cost in USD |
| Time | Session duration |
| Lines | Lines added / removed |
| 5h Limit | 5-hour rate limit usage + reset timer |
| 7d Limit | 7-day rate limit usage + reset timer |

## Requirements

- Windows 10/11
- .NET 10 Runtime
- Claude Code with statusline configured

## Setup

### 1. Build

```bash
cd src/ClaudeMonitor
dotnet publish -c Release
```

The executable will be at `bin/Release/net10.0-windows/win-x64/publish/ClaudeMonitor.exe`

### 2. Configure Claude Code statusline

Add to `~/.claude/settings.json`:

```json
{
  "statusLine": {
    "type": "command",
    "command": "node C:/Users/<YOU>/.claude/statusline.js"
  }
}
```

The `statusline.js` script writes session data to `~/.claude/widget-sessions/<session_id>.json` which the widget monitors.

### 3. Run

Double-click `ClaudeMonitor.exe`. Right-click the tray icon to:
- **Show/Hide** the overlay
- **Toggle Autostart** (adds to Windows startup via registry)
- **Exit** the application

## Usage

- **Drag** the widget to reposition (position is saved)
- **Tray icon double-click** to show/hide
- Widget automatically follows primary monitor changes

## Architecture

```
src/ClaudeMonitor/
├── Models/SessionData.cs       # JSON data model
├── Services/
│   ├── SessionWatcher.cs       # FileSystemWatcher + ObservableCollection
│   └── PositionManager.cs      # Position persistence + screen bounds
├── Converters/ValueConverters.cs  # WPF value converters
├── MainWindow.xaml/cs          # Overlay window
└── App.xaml/cs                 # Tray icon, single instance, autostart
```

## License

MIT
