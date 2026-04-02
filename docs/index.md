---
title: Claude Monitor
description: Desktop widget for monitoring Claude Code sessions
tags: [tool, wpf, windows, claude-code]
---

# Claude Monitor

Desktop WPF widget for Windows that displays real-time Claude Code session metrics.

## Navigation

- [[architecture]] — Project structure, components, data flow
- [[installation]] — Build, setup, and configuration guide

## Features

- Multi-session monitoring — each session displayed as a separate card
- Real-time updates via FileSystemWatcher
- Always-on-top overlay with drag-to-reposition
- System tray with show/hide toggle and autostart
- Position persistence across restarts
- Auto-cleanup of stale sessions (10 min timeout)
- Monitor-change resilient — repositions when displays change

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Framework | .NET 10 |
| UI | WPF |
| Tray | Windows Forms NotifyIcon |
| Data | JSON via System.Text.Json |
| File Watch | FileSystemWatcher |
| Distribution | Single-file EXE |
