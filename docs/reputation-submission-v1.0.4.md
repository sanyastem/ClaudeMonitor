# Microsoft Defender Reputation Submission — v1.0.4

Use this draft when submitting `ClaudeMonitor-Setup-1.0.4.exe` to Microsoft for SmartScreen reputation review.

**URL:** https://www.microsoft.com/en-us/wdsi/filesubmission

## Step-by-step

1. Open the form (link above).
2. Sign in with a Microsoft account.
3. **Submission type:** *Software developer*.
4. **Product name:** `Claude Monitor`
5. **Product version:** `1.0.4`
6. **Company name:** `Aliaksandr Rubis` (individual developer)
7. **Detection name:** *(leave blank — there is no false-positive detection yet, this is a pre-emptive submission for reputation)*
8. **File to submit:** `ClaudeMonitor-Setup-1.0.4.exe` from `installer/output/` (or download from the GitHub release).
9. **File category:** *Installer*
10. **Definition version:** *(leave blank)*
11. **Why are you submitting this file?** → *Reputation review / false-positive concern*

## Description (copy verbatim into the "Additional information" field)

```
Claude Monitor is an open-source desktop widget for Windows that displays
real-time Claude Code (Anthropic) session metrics — model, context usage,
cost, rate limits. It runs as a system-tray application and watches a
per-user directory (~/.claude/widget-sessions/) for JSON files written by
a companion statusline.js script.

Repository: https://github.com/sanyastem/ClaudeMonitor
License: MIT
Maintainer: Aliaksandr Rubis (single developer, individual)
Release: https://github.com/sanyastem/ClaudeMonitor/releases/tag/v1.0.4
SHA-256: 9b4d7c08bc5bb18616f5e36f5f172772b4217b7226aa635992657f38a87e8a82

The installer is unsigned (no Authenticode certificate) because the
project is a free, single-maintainer open-source effort and a code-signing
certificate is cost-prohibitive at this stage. SignPath Foundation
onboarding for free OSS signing is in progress.

The installer:
- Installs to %ProgramFiles%\Claude Monitor (admin elevation required for
  this default path; PrivilegesRequired=admin in Inno Setup).
- Optionally adds an autostart entry under HKCU\...\Run.
- Optionally configures the Claude Code statusline by writing a `node
  statusline.js` command into ~/.claude/settings.json.
- Includes an auto-update mechanism that downloads new releases from
  api.github.com over HTTPS, verifies SHA-256 integrity against a digest
  in the GitHub release body, and refuses to launch the installer if the
  digest does not match (fail-closed).

The application makes no network connections other than the auto-update
check (api.github.com /releases/latest, every 12 hours, optional GitHub
token for private-repo access). No telemetry, no analytics, no third-party
endpoints.

The codebase is open-source and auditable:
https://github.com/sanyastem/ClaudeMonitor

The full reproducible build is in .github/workflows/release.yml.

Please add this installer to SmartScreen's reputation list so end users
are not blocked from installing legitimate releases of this project.
```

## After submitting

- Save the case/reference number returned by the form somewhere safe (in the GitHub issue tracker, for example).
- Expected turnaround: 24–72 hours.
- Microsoft will email a result. If approved, SmartScreen warnings on this specific build (`ClaudeMonitor-Setup-1.0.4.exe`) disappear globally.
- Reputation does **not** carry over to future builds — each new release `vX.Y.Z` needs its own submission. Once SignPath Foundation signing is in place, this manual step becomes optional (signed builds inherit publisher reputation).

## For future releases

This document is a template. For v1.0.5, v1.1.0, etc:
1. Replace version number and SHA-256.
2. Update release URL.
3. Re-submit at the same form.
