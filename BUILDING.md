<!-- Stable v0.1.11; dot preview v0.2.0-preview.1. -->

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

The test harness uses only synthetic fixtures and must finish with an all-green `RESULT:` line. The number of groups may grow with real regression coverage; do not edit a test or a claimed count just to make it green. The dot preview currently has 36 native groups:

```text
RESULT: 36/36 passed; 0 failed;
```

The build does not invoke the user's real Codex installation or run the real `codex doctor` command.

## Build the portable package

```powershell
pwsh ./scripts/build-release.ps1 -Version 0.2.0-preview.1 -SkipInstaller
```

Outputs are placed under `artifacts/`, including the self-contained folder, portable ZIP, file manifest, SHA-256 checksums, and release preflight report. `package-board.ps1` downloads the official Node 24.16.0 Windows ZIP, validates its pinned SHA-256, runs the Node tests and bundles only the required runtime/source/legal files. It does not use npm or require Node on the user's PC.

## Build the installer

Install Inno Setup 6, then run:

```powershell
pwsh ./scripts/build-release.ps1 -Version 0.2.0-preview.1
pwsh ./scripts/test-installer.ps1 -Version 0.2.0-preview.1
```

The installer is per-user and does not require administrator rights. Preview installs use `%LOCALAPPDATA%\Programs\Codex SOS dot Preview` and a separate application ID, leaving stable installations alone. `test-installer.ps1` permits only a prerelease/CI build and a freshly generated, isolated target; it verifies installed hashes and uninstall without opening real-account diagnostics.

## Verifying the dot preview

The native harness covers retained SOS behavior and the report bridge. `dot-assistant/tests/*.test.mjs` covers status freshness, snapshots, local storage, loopback access control and copy semantics; the packaging script runs it with the bundled runtime. See [the verification boundary](docs/DOT_ASSISTANT.md#verification). Browser tests are distinct from native WPF clicks, real OCR and a real-account doctor. Normal automated builds never run the user's official doctor or connect to their tasks.

## Release automation

After the `main` build-and-test workflow passes, pushing an already-reviewed tag such as `v0.2.0-preview.1` starts `.github/workflows/release.yml`. Only `vN.N.N` and `vN.N.N-preview.N` (positive preview number) are accepted. It derives the version, requires matching notes, builds/tests/scans/packages, and tests preview installation/uninstallation before publication. A preview is marked prerelease and never becomes `latest`; an existing Release is left unchanged. Missing notes or required assets fail the run.

Release artifacts must come from the public commit being tagged. Do not upload locally modified binaries that cannot be reproduced from the repository.
