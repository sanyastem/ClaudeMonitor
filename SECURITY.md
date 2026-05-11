# Security Policy

## Reporting a vulnerability

**Do not file a public GitHub issue.**

Report security issues privately via [GitHub Security Advisories](https://github.com/sanyastem/ClaudeMonitor/security/advisories/new). I aim to acknowledge within 7 days and resolve critical issues within 30 days when possible.

If GitHub Security Advisories are unavailable to you, email the project owner via the email listed on the GitHub profile (`sanyastem`).

## What's in scope

- The Claude Monitor application (`ClaudeMonitor.exe`).
- The Inno Setup installer (`ClaudeMonitor-Setup-x.y.z.exe`).
- The auto-update flow (`UpdateChecker`, `AssetDownloader`, `App.DownloadAndInstall`, `SignatureVerifier`).
- The `statusline.js` and `setup-statusline.js` scripts shipped with the installer.

## Sensitive surfaces (where to look first)

The threat model is a Windows desktop app that runs as an unprivileged user but has:

1. **An auto-update mechanism** that downloads and executes an installer. The integrity contract is fail-closed SHA-256 verification against a digest in the GitHub release body, host-pinning to `api.github.com`, and a whitelist for redirect targets. Any way to weaken these checks is a security issue — see [CONTRIBUTING.md](CONTRIBUTING.md) for the invariants.
2. **A token reader** (`GetGhToken` / `LoadToken`) that resolves a GitHub PAT from `~/.claude/widget-gh-token.txt`, `gh auth token`, or `hosts.yml`. Any code path that leaks the token (to logs, to a non-`api.github.com` host, to a subprocess argv) is a security issue.
3. **File-system access** to the user's `~/.claude/` profile, including session JSON files and settings. `statusline.js` sanitizes `session_id` to block path traversal; weakening that is a security issue.
4. **An admin-elevated installer**. The post-install `Run` script (`node setup-statusline.js`) executes with the original user, not admin — but anything that flips that boundary deserves scrutiny.

## Out of scope

- Crashes triggered by intentionally malformed local files (the user can already write anything to their own `~/.claude/`).
- SmartScreen "Unknown publisher" warning — the installer is not Authenticode-signed yet (SignPath Foundation onboarding pending).
- Issues that require local administrator privileges to begin with.

## Supported versions

Only the latest released version is supported with security updates. Clients on older releases will receive the latest update via the auto-update mechanism as long as their version has the SHA-256 verification logic (≥ v1.0.3).
