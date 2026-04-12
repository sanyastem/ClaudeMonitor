# Contributing

Thanks for your interest in Claude Monitor. A few rules to keep contributions easy to review.

## Local setup

Requirements: .NET 10 SDK, Inno Setup 6, Node.js (for testing `statusline.js`).

```powershell
dotnet test tests/ClaudeMonitor.Tests/ClaudeMonitor.Tests.csproj -c Release
```

See [CLAUDE.md](CLAUDE.md) for the full architecture and per-area conventions.

## Security boundaries (do not regress)

The auto-update path has hardening that must not be weakened. If a PR touches any of the following, it needs both a clear justification in the description and a covering test:

- **`UpdateChecker.IsTrustedAssetUrl`** — only `https://api.github.com` is accepted. Adding any other host (including `github.com` or `objects.githubusercontent.com`) defeats the cross-host `Authorization` strip.
- **`UpdateChecker.ExtractSha256`** — the regex is the integrity oracle. Releases without a valid digest are refused. Loosening the regex is a security regression.
- **`AssetDownloader.TrustedHosts` + redirect handling** — the bearer token is only attached when the *current* hop is `api.github.com`. Don't carry it through redirects.
- **`App.DownloadAndInstall`** — random tmp filename + `FileMode.CreateNew` + `FileShare.None` prevent same-user TOCTOU. SHA-256 mismatch must short-circuit before `Process.Start`.

Tests live in [tests/ClaudeMonitor.Tests/UpdateCheckerSecurityTests.cs](tests/ClaudeMonitor.Tests/UpdateCheckerSecurityTests.cs) and [tests/ClaudeMonitor.Tests/AssetDownloaderTests.cs](tests/ClaudeMonitor.Tests/AssetDownloaderTests.cs). Add to them when extending the surface.

## Release flow

Versions in [src/ClaudeMonitor/ClaudeMonitor.csproj](src/ClaudeMonitor/ClaudeMonitor.csproj) and [installer/ClaudeMonitor.iss](installer/ClaudeMonitor.iss) must match. The [release workflow](.github/workflows/release.yml) enforces this. Tag `vX.Y.Z` triggers the release.

## Logging

Never log the GitHub token (or anything from `widget-gh-token.txt` / `gh auth token`). URLs and version strings are fine.

## Reporting security issues

If you find a vulnerability in the auto-update path or anywhere else that could lead to remote code execution or unauthorized access, please report it privately first via [GitHub Security Advisories](https://github.com/sanyastem/ClaudeMonitor/security/advisories/new) rather than a public issue.
