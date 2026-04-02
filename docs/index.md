---
title: Claude Monitor
description: Desktop widget for monitoring Claude Code sessions in real-time
tags: [tool, wpf, windows, claude-code]
version: 1.1.0
---

# Claude Monitor

Desktop WPF widget for Windows that displays real-time Claude Code session metrics.

## Navigation

- [[architecture]] — Project structure, components, data flow
- [[installation]] — Build, setup, and configuration guide

## Features

- **Multi-session tabs** — each session as a tab, auto-switches to active
- **Real-time updates** via FileSystemWatcher (no polling)
- **First-run position picker** — choose corner on first launch
- **Configurable visibility** — always visible or only with active sessions
- **Always on top toggle** — overlay or desktop-only mode
- **Idle rate limits** — show 5h/7d usage even without sessions
- **System tray** — full settings menu (visibility, on-top, limits, autostart)
- **Position persistence** across restarts
- **Monitor-aware** — repositions when displays change
- **Stale cleanup** — sessions older than 1 min auto-removed

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Framework | .NET 10 |
| UI | WPF |
| Tray | Windows Forms NotifyIcon |
| Data | JSON via System.Text.Json |
| File Watch | FileSystemWatcher |
| Installer | Inno Setup |
| Distribution | Single-file EXE / Setup installer |

## Settings

All persisted in `~/.claude/widget-settings.json`:

| Setting | Default | Description |
|---------|---------|-------------|
| AlwaysVisible | false | Show widget even without active sessions |
| AlwaysOnTop | true | Keep widget above all windows |
| ShowIdleLimits | true | Display rate limits in idle state |
