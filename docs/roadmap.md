---
title: Roadmap
description: Planned features for upcoming releases
parent: "[[index]]"
---

# Roadmap

## v1.1.0 — Analytics & Alerts

### Session History (SQLite)
- Archive completed sessions to local SQLite database
- History window: daily/weekly/all-time cost summaries
- Scrollable session list with model, cost, duration, tokens, date
- Filter by time range (today / 7d / 30d / all)

### Burn Rate
- Calculate $/minute and tokens/minute from session data
- Show in widget card below cost/time
- Optional display in statusline

### Rate Limit Alerts
- Balloon notification when rate limit crosses 80%
- Notification when limit resets
- Configurable on/off in settings

### Tooltips on Metrics
- Hover any metric to see explanation
- What it means, how it's calculated

### Update Progress Dialog
- Progress bar during update download
- Cancel button
- Changelog preview before install

### Tray Icon Status Colors
- Green — active session
- Gray — idle (no sessions)
- Red — error state
