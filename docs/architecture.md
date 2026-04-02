---
title: Architecture
description: Project structure, components, and data flow
parent: "[[index]]"
---

# Architecture

## Data Flow

```
Claude Code Session
       │
       ▼
statusline.js (writes JSON per session)
       │
       ▼
~/.claude/widget-sessions/<session_id>.json
       │
       ▼
FileSystemWatcher (SessionWatcher.cs)
       │
       ▼
ObservableCollection<SessionViewModel>
       │
       ▼
WPF ItemsControl (MainWindow.xaml)
```

## Project Structure

```
src/ClaudeMonitor/
├── Models/
│   └── SessionData.cs         # JSON deserialization models
├── Services/
│   ├── SessionWatcher.cs      # File monitoring + data binding
│   └── PositionManager.cs     # Window position persistence
├── Converters/
│   └── ValueConverters.cs     # WPF value converters for colors/widths
├── Assets/
│   └── icon.ico               # Tray icon
├── App.xaml / App.xaml.cs      # Entry point, tray icon, single instance
├── MainWindow.xaml / .cs       # Overlay window
└── ClaudeMonitor.csproj        # Project config
```

## Components

### SessionWatcher

- Watches `~/.claude/widget-sessions/` for `.json` file changes
- Debounces events (300ms) to avoid excessive reloads
- Maintains `ObservableCollection<SessionViewModel>` bound to UI
- Auto-removes sessions not updated for 10+ minutes
- Cleans up stale JSON files from disk

### SessionViewModel

- Implements `INotifyPropertyChanged` for live UI updates
- Computed properties: `ContextBarWidth`, `DurationText`, `CostText`, `Rl5Text`, `Rl7Text`
- Color thresholds: green (<50%), yellow (<80%), red (80%+)

### PositionManager

- Saves/loads position to `~/.claude/widget-pos.json`
- `EnsureOnScreen()` — checks against virtual screen bounds
- If widget is off-screen (monitor removed), resets to primary monitor top-right

### App

- `Mutex` prevents duplicate instances
- `NotifyIcon` for system tray with context menu
- Registry-based autostart (`HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`)
- `ShutdownMode="OnExplicitShutdown"` — closing window hides to tray

## Session JSON Format

```json
{
  "session_id": "abc123",
  "model": { "id": "claude-opus-4-6", "display_name": "Opus" },
  "context_window": { "used_percentage": 35.2, "context_window_size": 200000 },
  "cost": {
    "total_cost_usd": 0.42,
    "total_duration_ms": 125000,
    "total_lines_added": 15,
    "total_lines_removed": 3
  },
  "rate_limits": {
    "five_hour": { "used_percentage": 22.5, "resets_at": 1743620000 },
    "seven_day": { "used_percentage": 8.1, "resets_at": 1744050000 }
  },
  "_ts": 1743600000000
}
```

## Memory Efficiency

- FileSystemWatcher is event-driven — no polling for file reads
- Debounce prevents rapid successive UI rebuilds
- `SessionViewModel` reuses objects via `Update()` instead of recreating
- Single-instance Mutex prevents accidental memory duplication
