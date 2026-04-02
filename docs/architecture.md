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
WPF TabControl-style UI (MainWindow.xaml)
```

## Project Structure

```
src/ClaudeMonitor/
├── Models/
│   └── SessionData.cs         # JSON deserialization models
├── Services/
│   ├── SessionWatcher.cs      # File monitoring + data binding
│   └── PositionManager.cs     # Position, settings, rate limits persistence
├── Converters/
│   └── ValueConverters.cs     # WPF value converters for colors/widths
├── Assets/
│   └── icon.ico               # Tray icon (16/24/32/48px)
├── SetupWindow.xaml / .cs     # First-run position picker
├── App.xaml / App.xaml.cs     # Entry point, tray icon, single instance
├── MainWindow.xaml / .cs      # Overlay window with tabs
└── ClaudeMonitor.csproj       # Project config

installer/
├── ClaudeMonitor.iss          # Inno Setup script
├── statusline.js              # Bundled statusline for distribution
└── setup-statusline.js        # Post-install config script

tests/ClaudeMonitor.Tests/     # xUnit tests
docs/                          # Obsidian-compatible documentation
```

## Components

### SessionWatcher

- Watches `~/.claude/widget-sessions/` for `.json` file changes
- Debounces events (300ms) to avoid excessive reloads
- Maintains `ObservableCollection<SessionViewModel>` bound to UI
- Fires `SessionUpdated` event to trigger tab auto-switching
- Auto-removes sessions not updated for 1 minute
- Cleans up stale JSON files from disk

### SessionViewModel

- Implements `INotifyPropertyChanged` for live UI updates
- Tab state properties: `IsActive`, `TabBackground`, `TabForeground`, `ActiveIndicator`
- Computed properties: `ContextBarWidth`, `DurationText`, `CostText`, `Rl5Text`, `Rl7Text`
- Time formatting: seconds → minutes → hours → days
- Color thresholds: green (<50%), yellow (<80%), red (80%+)

### PositionManager

- **Position**: saves/loads to `~/.claude/widget-pos.json`
- **Settings**: saves/loads to `~/.claude/widget-settings.json`
  - `AlwaysVisible` — show widget without active sessions
  - `AlwaysOnTop` — overlay or desktop-only mode
  - `ShowIdleLimits` — display rate limits in idle state
- **Last limits**: saves/loads to `~/.claude/widget-last-limits.json`
- `EnsureOnScreen()` — checks against virtual screen bounds

### SetupWindow

- Shown on first launch (when no saved position exists)
- Visual monitor preview with 4 corner buttons
- Calculates position based on primary screen WorkingArea

### App

- `Mutex` prevents duplicate instances
- `NotifyIcon` system tray with context menu (5 items + exit)
- Registry-based autostart (`HKCU\...\Run`)
- `ShutdownMode="OnExplicitShutdown"` — closing hides to tray
- First-run detection via PositionManager

## Persisted Files

| File | Purpose | Size |
|------|---------|------|
| `widget-sessions/<id>.json` | Per-session metrics (auto-cleaned) | ~1KB |
| `widget-pos.json` | Window position | ~30B |
| `widget-settings.json` | User preferences | ~80B |
| `widget-last-limits.json` | Last known rate limits | ~60B |

## Session JSON Format

```json
{
  "session_id": "abc123",
  "model": { "id": "claude-opus-4-6", "display_name": "Opus 4.6 (1M context)" },
  "context_window": { "used_percentage": 35.2, "context_window_size": 1000000 },
  "cost": {
    "total_cost_usd": 12.15,
    "total_duration_ms": 3881000,
    "total_lines_added": 2227,
    "total_lines_removed": 425
  },
  "rate_limits": {
    "five_hour": { "used_percentage": 22.5, "resets_at": 1743620000 },
    "seven_day": { "used_percentage": 13.0, "resets_at": 1744050000 }
  },
  "_ts": 1743600000000
}
```

## Memory Efficiency

- FileSystemWatcher is event-driven — no polling
- Debounce (300ms) prevents rapid UI rebuilds
- `SessionViewModel.Update()` reuses objects instead of recreating
- Single-instance Mutex prevents duplication
- Session files ~1KB each, auto-deleted after 1 minute of inactivity
