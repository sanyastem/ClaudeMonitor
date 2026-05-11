# Code Signing Policy

This document describes how Claude Monitor binaries are signed, who can request signing, and how the integrity of signed releases is established.

Last updated: 2026-05-11

## Signing infrastructure

Claude Monitor uses [SignPath.io](https://signpath.io/) (provided free of charge by the [SignPath Foundation](https://signpath.org/) to open-source projects) for Authenticode code signing of Windows installers. Private keys are stored in a SignPath-managed Hardware Security Module — they are never exported and never present on developer machines or build servers.

> **Status (2026-05-11):** SignPath Foundation onboarding is **in progress**. Until signing is active, releases are unsigned and Windows SmartScreen may show a "Windows protected your PC" warning on first install. Manual SHA-256 verification (see `README.md` and each release body) is the integrity contract in the interim.

## Trusted releases

The only signed binaries are those produced by the [release.yml](../.github/workflows/release.yml) GitHub Actions workflow, triggered by a `vX.Y.Z` tag push or manual `workflow_dispatch` by the maintainer.

A signed Claude Monitor installer satisfies all of the following:

- Signed by `O=SignPath Foundation` with project name `Claude Monitor` in the subject.
- The hash of the installer matches the `SHA256:` line in the corresponding GitHub release body at https://github.com/sanyastem/ClaudeMonitor/releases.
- Built from a commit reachable from `master` on the [public repository](https://github.com/sanyastem/ClaudeMonitor).
- Built via a fully scripted pipeline visible in [.github/workflows/release.yml](../.github/workflows/release.yml).

Any binary that does not satisfy all four conditions is not endorsed by the project and should be discarded.

## Roles and approval

Claude Monitor is a single-maintainer project.

| Role | Person | Responsibility |
|---|---|---|
| Committer | Aliaksandr Rubis ([@sanyastem](https://github.com/sanyastem)) | Writes and merges code changes. |
| Reviewer | Aliaksandr Rubis | Reviews PRs, including any from external contributors. |
| Release approver | Aliaksandr Rubis | Cuts and tags releases. Triggers the signing pipeline. |

External contributors are welcome to submit pull requests, but only the maintainer can merge to `master` and only the maintainer can trigger a signing job.

If the maintainer is unable to continue the project, this document will be updated to reflect a successor or to retire the signing certificate.

## Build & signing pipeline

1. Tag `vX.Y.Z` pushed to `master`, or release manually triggered via `workflow_dispatch`.
2. GitHub Actions workflow asserts that `<Version>` in `src/ClaudeMonitor/ClaudeMonitor.csproj` and `MyAppVersion` in `installer/ClaudeMonitor.iss` both match the tag.
3. Full test suite (`dotnet test`) must pass.
4. `dotnet publish` produces the single-file `ClaudeMonitor.exe`.
5. Inno Setup compiles `ClaudeMonitor-Setup-X.Y.Z.exe` bundling the published binary plus statusline scripts.
6. The unsigned installer is uploaded to SignPath via `signpath/github-action-submit-signing-request`.
7. SignPath signs and returns the artifact. The signed artifact is what is published.
8. SHA-256 is computed on the **signed** artifact and embedded into the GitHub release body.
9. `softprops/action-gh-release` publishes the release with the signed installer attached.

The auto-updater built into earlier clients (v1.0.3+) refuses to install any release whose body does not contain a valid `SHA256:` digest, and refuses if the digest does not match the bytes it actually downloaded (fail-closed).

## What changes require a new signing request

- Every released tag (each `vX.Y.Z`) is signed in its own workflow run.
- Test signing for development branches is not enabled and not requested.

## Reporting suspected misuse

If you find a Claude Monitor installer that appears signed but does not satisfy the four "trusted releases" conditions above, report it privately via [GitHub Security Advisories](https://github.com/sanyastem/ClaudeMonitor/security/advisories/new). See [SECURITY.md](../SECURITY.md) for the broader vulnerability disclosure policy.
