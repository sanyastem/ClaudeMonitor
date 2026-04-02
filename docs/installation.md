---
title: Installation
description: Build, setup, and configuration guide
parent: "[[index]]"
---

# Installation

## Prerequisites

- Windows 10/11
- .NET 10 SDK (for building) or .NET 10 Runtime (for running)
- Claude Code with statusline configured

## Build

```bash
cd src/ClaudeMonitor
dotnet publish -c Release
```

Output: `bin/Release/net10.0-windows/win-x64/publish/ClaudeMonitor.exe`

## Configure Claude Code Statusline

The widget reads session data from `~/.claude/widget-sessions/`. Claude Code's statusline script must write to this directory.

### 1. Create statusline script

Save to `~/.claude/statusline.js`:

```javascript
const fs = require("fs");
const path = require("path");
const d = JSON.parse(fs.readFileSync(0, "utf8"));

// Save per-session data for widget
try {
  const dir = path.join(process.env.USERPROFILE || process.env.HOME, ".claude", "widget-sessions");
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  const sid = (d.session_id || "unknown").replace(/[^a-zA-Z0-9_-]/g, "");
  fs.writeFileSync(path.join(dir, `${sid}.json`), JSON.stringify({ ...d, _ts: Date.now() }));
} catch(e) {}

// ... rest of statusline output
```

### 2. Add to Claude Code settings

Edit `~/.claude/settings.json`:

```json
{
  "statusLine": {
    "type": "command",
    "command": "node C:/Users/<YOUR_USERNAME>/.claude/statusline.js"
  }
}
```

## Run

Double-click `ClaudeMonitor.exe`. The widget appears in the top-right corner.

## Controls

| Action | Result |
|--------|--------|
| Drag window | Reposition (saved automatically) |
| Tray icon double-click | Show/Hide widget |
| Tray right-click → Autostart | Toggle Windows startup |
| Tray right-click → Exit | Close application |

## Autostart

Toggle via tray icon context menu. Sets registry key:

```
HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run\ClaudeMonitor
```

## Troubleshooting

### Widget shows "No active sessions"

- Verify Claude Code is running with statusline configured
- Check `~/.claude/widget-sessions/` for JSON files
- Session files older than 10 minutes are auto-deleted

### Widget not visible after monitor change

- Widget auto-repositions every 30 seconds
- If stuck, delete `~/.claude/widget-pos.json` and restart
