---
title: Installation
description: Build, setup, and configuration guide
parent: "[[index]]"
---

# Installation

## Installer (recommended)

1. Download `ClaudeMonitor-Setup-x.x.x.exe` from [GitHub Releases](https://github.com/sanyastem/ClaudeMonitor/releases/latest)
2. Run the installer — it will:
   - Check for **Node.js** and **.NET 10 Runtime**, offer to install via winget if missing
   - Install to `C:\Program Files\Claude Monitor`
   - Configure Claude Code statusline automatically
   - Optionally enable autostart with Windows
3. On first launch, pick your preferred widget corner

## Build from Source

### Prerequisites

- Windows 10/11 (x64)
- .NET 10 SDK
- Node.js

### Build

```bash
cd src/ClaudeMonitor
dotnet publish -c Release --self-contained false -p:PublishSingleFile=true
```

Output: `bin/Release/net10.0-windows/win-x64/publish/ClaudeMonitor.exe`

### Build installer

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php):

```bash
iscc installer/ClaudeMonitor.iss
```

Output: `installer/output/ClaudeMonitor-Setup-x.x.x.exe`

## Manual Statusline Setup

If not using the installer, configure manually:

### 1. Copy statusline script

Copy `installer/statusline.js` to `~/.claude/statusline.js`

### 2. Configure Claude Code

Edit `~/.claude/settings.json`:

```json
{
  "statusLine": {
    "type": "command",
    "command": "node C:/Users/<YOUR_USERNAME>/.claude/statusline.js"
  }
}
```

## Controls

| Action | Result |
|--------|--------|
| Drag window | Reposition (saved automatically) |
| Tray double-click | Show/Hide widget |
| Tray → Always visible | Show widget even without sessions |
| Tray → Always on top | Overlay or desktop-only mode |
| Tray → Show limits when idle | Rate limits visible without sessions |
| Tray → Autostart | Toggle Windows startup |
| Tray → Exit | Close application |

## Troubleshooting

### Widget not showing

- If "Always visible" is off, widget hides when no Claude Code sessions are active
- Check `~/.claude/widget-sessions/` for JSON files
- Verify Claude Code is running with statusline configured

### Widget stuck off-screen

- Widget auto-repositions every 30 seconds
- Delete `~/.claude/widget-pos.json` and restart to reset position

### Installer didn't configure statusline

- Run manually: `node "C:\Program Files\Claude Monitor\setup-statusline.js" "C:\Program Files\Claude Monitor"`
- Or configure manually (see [[#Manual Statusline Setup]])

### Session disappears too quickly

- Sessions auto-remove after 1 minute of inactivity
- This is expected — the session file is only updated when Claude Code responds
