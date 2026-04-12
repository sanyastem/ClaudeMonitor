# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

WPF (.NET 10, `net10.0-windows`, x64) tray-resident desktop widget for Windows that displays live Claude Code session metrics. Companion `statusline.js` runs inside Claude Code itself and writes per-session JSON files that the widget watches.

## Common commands

```powershell
# Build (Release) — also runs after every Edit, must stay green
dotnet build src/ClaudeMonitor/ClaudeMonitor.csproj -c Release

# Publish single-file exe (input for the installer)
dotnet publish src/ClaudeMonitor/ClaudeMonitor.csproj -c Release -r win-x64 --self-contained false

# Tests (xUnit)
dotnet test tests/ClaudeMonitor.Tests/ClaudeMonitor.Tests.csproj
# Single test
dotnet test tests/ClaudeMonitor.Tests/ClaudeMonitor.Tests.csproj --filter "FullyQualifiedName~PositionManagerTests"

# Build installer — requires `dotnet publish` to have run first
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\ClaudeMonitor.iss
# Output: installer\output\ClaudeMonitor-Setup-<version>.exe

# Compute SHA-256 (must be embedded in GitHub Release body — see "Release flow")
(Get-FileHash installer\output\ClaudeMonitor-Setup-1.0.3.exe -Algorithm SHA256).Hash.ToLowerInvariant()
```

## Release flow (security-critical)

The auto-updater is **fail-closed on SHA-256**: it refuses to install any release whose body lacks a valid `SHA256: <hex>` line. Two paths:

### Automated (preferred) — [.github/workflows/release.yml](.github/workflows/release.yml)

1. Bump `<Version>` in [src/ClaudeMonitor/ClaudeMonitor.csproj](src/ClaudeMonitor/ClaudeMonitor.csproj) **and** `MyAppVersion` in [installer/ClaudeMonitor.iss](installer/ClaudeMonitor.iss).
2. Commit, then `git tag v<X.Y.Z>` and `git push --tags`.
3. The workflow asserts version match, runs tests, publishes the .NET app, compiles Inno Setup, computes SHA-256, and creates the GitHub Release with the digest embedded in the body.

### Manual (fallback)

```powershell
dotnet publish src/ClaudeMonitor/ClaudeMonitor.csproj -c Release -r win-x64 --self-contained false
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\ClaudeMonitor.iss
$sha = (Get-FileHash installer\output\ClaudeMonitor-Setup-1.0.4.exe -Algorithm SHA256).Hash.ToLowerInvariant()
# release body MUST contain a line: SHA256: <sha>
gh release create v1.0.4 --target master --notes-file release-body.md installer\output\ClaudeMonitor-Setup-1.0.4.exe
```

### Security invariants

- Release body must contain a line matching `^[\s>*\-_`]*sha-?256[\s:=]+`?[0-9a-f]{64}`?\s*$` (regex in [UpdateChecker.cs](src/ClaudeMonitor/Services/UpdateChecker.cs) `Sha256Pattern`).
- Asset name must end in `.exe`; the API URL (`asset.url`, not `browser_download_url`) must be on `api.github.com`.
- Releases without a `SHA256:` line will be silently ignored by clients ≥ 1.0.3.

## Architecture

### Two cooperating processes

```
Claude Code  ──exec──►  installer/statusline.js  ──writes──►  ~/.claude/widget-sessions/<sid>.json
                                                                          │
                                                              FileSystemWatcher
                                                                          ▼
                                                              ClaudeMonitor.exe (WPF widget)
```

- `statusline.js` is registered in `~/.claude/settings.json` under `statusLine.command`. It receives session JSON on stdin, sanitizes `session_id` (`[^a-zA-Z0-9_-]` stripped — guards path traversal), enriches with `_ts`, writes one file per session, and prints the statusline string for Claude Code to render.
- The widget owns the watch directory; `statusline.js` is the only writer.

### Widget internals

- **Entry point**: [App.xaml.cs](src/ClaudeMonitor/App.xaml.cs). Single-instance `Mutex` (`ClaudeMonitor_SingleInstance`), `NotifyIcon` tray, first-run `SetupWindow` if `widget-pos.json` is missing.
- **Session pipeline**: [SessionWatcher.cs](src/ClaudeMonitor/Services/SessionWatcher.cs) — `FileSystemWatcher` on `~/.claude/widget-sessions`, **300 ms debounce**, drives an `ObservableCollection<SessionViewModel>` bound to the WPF `TabControl`. Sessions stale > 1 min are evicted (and their JSON deleted from disk). `_lastChangedFile` is `volatile` — multiple watcher threads can write it.
- **Persistence**: [PositionManager.cs](src/ClaudeMonitor/Services/PositionManager.cs) reads/writes three JSON files in `~/.claude/`: `widget-pos.json`, `widget-settings.json`, `widget-last-limits.json`. `EnsureOnScreen()` clamps to current virtual screen bounds (handles monitor unplug between runs).
- **Models**: [SessionData.cs](src/ClaudeMonitor/Models/SessionData.cs) — DTOs for the JSON shape Claude Code emits. `SessionViewModel` (in the same file or alongside) is the `INotifyPropertyChanged` wrapper with derived display props (`ContextBarWidth`, `DurationText`, `Rl5Text`, color thresholds at <50/<80/≥80%).
- **Converters**: [Converters/ValueConverters.cs](src/ClaudeMonitor/Converters/ValueConverters.cs) — XAML-side color/width conversions. Tab styling lives on the VM, not in converters.

### Auto-update path (security boundary)

[UpdateChecker.cs](src/ClaudeMonitor/Services/UpdateChecker.cs) → [App.xaml.cs `DownloadAndInstall`](src/ClaudeMonitor/App.xaml.cs) → [SignatureVerifier.cs](src/ClaudeMonitor/Services/SignatureVerifier.cs).

Hardening that must not regress:

- **Host pinning**: `UpdateChecker.IsTrustedAssetUrl` accepts only `https://api.github.com/...`. `App.TrustedDownloadHosts` whitelists the three GitHub-controlled CDN hosts that GitHub redirects to.
- **Manual redirect** (`SocketsHttpHandler { AllowAutoRedirect = false }`): the `Authorization: Bearer` header is re-attached only when the next hop is `api.github.com`. Never call `GetByteArrayAsync` directly on the asset URL — use `DownloadFollowingRedirects`.
- **TOCTOU**: temp file is `ClaudeMonitor-Setup-<guid>.exe` opened with `FileMode.CreateNew, FileShare.None`. Don't reuse a fixed filename.
- **SHA-256**: fail-closed. If the release body has no digest or hashes mismatch, the installer is **not** launched and a balloon tip surfaces the failure.
- **Authenticode**: advisory only — `WinVerifyTrust` runs after SHA-256 passes and logs the signer's subject; an unsigned installer still launches because the project's installers are not (yet) Authenticode-signed.

The GitHub PAT used to read private releases is read on demand via `GetGhToken()` from `~/.claude/widget-gh-token.txt`, then `gh auth token`. Don't pass the token to non-`api.github.com` hosts.

### Installer

[installer/ClaudeMonitor.iss](installer/ClaudeMonitor.iss) — Inno Setup 6, `PrivilegesRequired=admin`, components `widget` and `statusline` (either or both). Silent installs (`WizardSilent`) auto-launch via `runasoriginaluser` so the post-update relaunch doesn't end up as SYSTEM. Pre-install hook kills any running `ClaudeMonitor.exe` and offers winget-based bootstrap of Node.js / .NET 10 Desktop Runtime.

## Conventions

- **Async**: every I/O path is `async`. Never block on `Task.Result` or `.Wait()` on the UI thread.
- **Logging**: [Services/Logger.cs](src/ClaudeMonitor/Services/Logger.cs) writes to `~/.claude/widget.log` (3 rotated backups, 512KB each). Use `Log.Info` / `Log.Error` / `Log.Debug`. `Debug` is gated by env var `CLAUDEMONITOR_DEBUG=1` or the marker file `~/.claude/widget-debug`. URLs are OK to log; **never** log the GitHub token, contents of `widget-gh-token.txt`, or anything from `gh auth token`.
- **Settings keys**: when adding a new toggle, update both `widget-settings.json` (via `PositionManager.GetSetting*`) and the corresponding XAML in [SettingsWindow.xaml](src/ClaudeMonitor/SettingsWindow.xaml) — there is no MVVM binding layer between them.
- **Adding a release artifact**: any change that touches the auto-update contract (`asset.url` host, SHA-256 regex, redirect host whitelist) is a security boundary. Update tests in `tests/ClaudeMonitor.Tests/` accordingly.
