<!-- Repository current release marker: v0.1.9 candidate -->

# Building Codex SOS

## Requirements

- Windows x64
- PowerShell 7 (`pwsh`)
- .NET SDK 10.0.400, as pinned by `global.json`
- Inno Setup 6 only when building the installer

End users do not need these tools. The released application is self-contained.

## Clean verification

From the repository root:

```powershell
pwsh ./scripts/check-source-secrets.ps1
pwsh ./scripts/test.ps1
```

The test harness uses only synthetic fixtures and must finish with an all-green `RESULT:` line. The number of groups may grow with real regression coverage; do not edit a test or a claimed count just to make it green. The current candidate has 34 groups:

```text
RESULT: 34/34 passed; 0 failed;
```

The build does not invoke the user's real Codex installation or run the real `codex doctor` command.

## Build the portable package

```powershell
pwsh ./scripts/build-release.ps1 -Version 0.1.9 -SkipInstaller
```

Outputs are placed under `artifacts/`, including the self-contained folder, portable ZIP, file manifest, SHA-256 checksums, and release preflight report.

## Build the installer

Install Inno Setup 6, then run:

```powershell
pwsh ./scripts/build-release.ps1 -Version 0.1.9
```

The installer is per-user, does not require administrator rights, and installs under `%LOCALAPPDATA%\Programs\Codex SOS`.

## Release automation

After the `main` build-and-test workflow passes, pushing an already-reviewed tag such as `v0.1.9` starts `.github/workflows/release.yml`. The workflow accepts only tags in the form `vN.N.N`, derives the version from that tag, performs a clean Windows build, reruns the tests and privacy checks, creates the portable ZIP and installer, and publishes the Release only when it does not already exist. A missing matching release-notes file or required artifact fails the run.

Release artifacts must come from the public commit being tagged. Do not upload locally modified binaries that cannot be reproduced from the repository.
