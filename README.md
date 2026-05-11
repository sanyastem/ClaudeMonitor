# Claude Monitor

A lightweight WPF desktop widget for Windows that displays real-time Claude Code session metrics.

## Download

**[Latest Release](https://github.com/sanyastem/ClaudeMonitor/releases/latest)** — download `ClaudeMonitor-Setup-x.x.x.exe` and run the installer.

> **Note about SmartScreen.** The installer is not yet Authenticode-signed (free OSS signing via SignPath Foundation is in progress). On first install Windows may show *"Windows protected your PC — unrecognized app"*. Click **More info → Run anyway** to proceed. To verify the download before running:
>
> ```powershell
> (Get-FileHash .\ClaudeMonitor-Setup-x.x.x.exe -Algorithm SHA256).Hash
> ```
>
> The hash must match the `SHA256:` line in the corresponding [release notes](https://github.com/sanyastem/ClaudeMonitor/releases/latest). Subsequent auto-updates verify this hash automatically and refuse to install on mismatch.

## Features

- **Multi-session tabs** — each Claude Code session displayed as a tab, auto-switches to active session
- **Real-time updates** — FileSystemWatcher for instant data refresh, no polling
- **First-run setup** — position picker on first launch
- **Configurable visibility** — always visible or only when sessions are active
- **Always on top** — toggle between overlay mode and desktop-only
- **Idle rate limits** — show 5h/7d usage limits even without active sessions
- **Monitor-aware** — automatically repositions when displays change
- **System tray** — full settings via right-click context menu
- **Position memory** — remembers where you placed it across restarts
- **Low memory** — ~15-20MB RAM, ~1KB per session file, event-driven
- **Single instance** — Mutex prevents duplicate processes
- **Stale cleanup** — sessions inactive for 1 minute auto-remove

## Session Metrics

| Metric | Description |
|--------|-------------|
| Model | Active model name (Opus, Sonnet, etc.) |
| Context | Visual progress bar + percentage |
| Cost | Session cost in USD |
| Time | Session duration (seconds, minutes, hours, days) |
| Lines | Lines added / removed |
| 5h Limit | 5-hour rate limit usage + time until reset |
| 7d Limit | 7-day rate limit usage + time until reset |

## Tray Menu

| Option | Description |
|--------|-------------|
| Show/Hide | Toggle widget visibility |
| Always visible | Keep widget visible even without active sessions |
| Always on top | Overlay on all windows or desktop-only |
| Show limits when idle | Display rate limits when no sessions are active |
| Autostart | Launch with Windows (registry-based) |
| Exit | Close the application |

All settings persist in `~/.claude/widget-settings.json`.

## Installation

### Installer (recommended)

Download and run the setup from [Releases](https://github.com/sanyastem/ClaudeMonitor/releases/latest). The installer will:

1. Check for Node.js and .NET 10 Runtime, offer to install if missing
2. Install Claude Monitor to `C:\Program Files\Claude Monitor`
3. Configure Claude Code statusline automatically
4. Optionally add to Windows startup

### Build from source

```powershell
dotnet publish src/ClaudeMonitor/ClaudeMonitor.csproj -c Release -r win-x64 --self-contained false
```

Output: `src/ClaudeMonitor/bin/Release/net10.0-windows/win-x64/publish/ClaudeMonitor.exe`

To build the installer:

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\ClaudeMonitor.iss
```

### Manual statusline setup

If not using the installer, add to `~/.claude/settings.json`:

```json
{
  "statusLine": {
    "type": "command",
    "command": "node C:/Users/<YOUR_USERNAME>/.claude/statusline.js"
  }
}
```

The `statusline.js` script writes per-session data to `~/.claude/widget-sessions/<session_id>.json`.

## Requirements

- Windows 10/11 (x64)
- [Node.js](https://nodejs.org) (for statusline script)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (for widget)
- [Claude Code](https://claude.ai/code)

## Architecture

```
src/ClaudeMonitor/
├── Models/SessionData.cs          # JSON deserialization models
├── Services/
│   ├── SessionWatcher.cs          # FileSystemWatcher + ObservableCollection
│   └── PositionManager.cs         # Position, settings, rate limits persistence
├── Converters/ValueConverters.cs  # WPF value converters (colors, widths)
├── SetupWindow.xaml/cs            # First-run position picker
├── MainWindow.xaml/cs             # Overlay window with tabs
└── App.xaml/cs                    # Tray icon, single instance, autostart
```

### Data flow

```
Claude Code → statusline.js → ~/.claude/widget-sessions/<id>.json → ClaudeMonitor
```

## License

MIT — Copyright (c) 2026 Aliaksandr Rubis
