# Changelog

The project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Planned

- Maintenance updates based on real user feedback and future `codex doctor` schema changes.

## [0.1.6] - 2026-09-04

### Added

- A copy-only feedback action prepares a Support-ready draft without opening a browser or requiring GitHub sign-in.
- The stable phrase `feedback upload failed` and its common Chinese descriptions can be used for narrow, explainable public-issue matching.

### Changed

- Opening the public GitHub bug form is now a separate, explicit action instead of being bundled with copying the report.
- Feedback guidance says that an unchanged Feedback ID is not proof of delivery and discourages repeated retries or reproducing a failure just to report it.

### Security

- Copy-only feedback stays on the local clipboard and never opens or sends data to GitHub.
- Public-form URLs remain exact allow-listed links without report text, screenshots, paths, or identifiers in the URL.

### Known limitations

- Codex SOS prepares but never sends a Support email or public issue; the user still reviews and pastes the material.
- The v0.1.6 Windows packages are unsigned and may show an unknown-publisher warning.

## [0.1.5] - 2026-09-02

### Added

- The copied official-feedback draft now includes incident time and time zone, visible-error evidence status, optional private Feedback ID guidance, and concise scope/log reminders.
- The privacy-screen guidance explains that reviewed material can be used in either the public Codex bug form or an existing OpenAI Support email thread.

### Security

- Feedback IDs are not collected automatically and are kept out of public issues unless Support explicitly asks for one.
- Users are told not to reproduce a failure just to obtain an ID or repeat diagnostics already supplied.

### Known limitations

- Scope, reproducibility, affected accounts, and failure stage are not inferred; users add only facts they already know.
- The v0.1.5 Windows packages are unsigned and may show an unknown-publisher warning.

## [0.1.4] - 2026-08-30

### Fixed

- A clearly unrelated browser, web page, startup item, or other-program window is no longer presented as a Codex problem or routed to the Codex bug form.
- Chinese symptoms such as “没反应” and “闪退” remain available to local diagnosis when a preceding Windows path is redacted.

### Security

- Out-of-scope reports stay local: no Codex issue search is sent and the official-feedback action is hidden.
- Path redaction still removes the full path; only allow-listed generic symptom constants survive for diagnosis and narrow search.

### Known limitations

- The v0.1.4 Windows packages are unsigned and may show an unknown-publisher warning.

## [0.1.3] - 2026-08-29

### Added

- After the existing privacy preview, users can copy a draft aligned with the current official Codex App bug form and open that exact form.
- The draft includes safe-to-share version, platform, symptom, reproduction, expected-behavior, diagnostic, fault-event, and similar-issue sections; subscription, session, and usage details remain explicitly uncollected.
- When a highly similar issue already exists, the UI recommends confirming it and adding a 👍 instead of creating a duplicate report.

### Security

- The feedback page is an exact allow-listed URL with no report content in its query string.
- Codex SOS does not auto-login, paste, upload, react, or submit. The copied draft receives another privacy-redaction pass and remains marked `NOT SUBMITTED`.

### Known limitations

- Public reporting may require GitHub sign-in; users must paste the draft into the matching fields and select their subscription themselves.
- The v0.1.3 Windows packages are unsigned and may show an unknown-publisher warning.

## [0.1.2] - 2026-08-27

### Added

- Simplified Chinese remains the default, with a prominent in-app switch for Traditional Chinese and English.
- The start page, progress, results, clarification, privacy review, save messages, and exported report follow the selected language.

### Security

- Localization changes presentation only; diagnostic rules, collection boundaries, and read-only network behavior are unchanged.

### Known limitations

- The v0.1.2 Windows packages are unsigned and may show an unknown-publisher warning.

### Fixed

- Treat repeated Codex desktop exits as a cautious desktop-application clue,
  distinguish confirmed crash events from suspected exits, and exclude
  `RADAR_PRE_LEAK_64` resource warnings from crash evidence.
- Recognize the official Codex package when older Windows records identify the
  process as `ChatGPT.exe`, and use sanitized exception codes when searching
  similar public issues.

### Security

- Added the public [Code signing policy](CODE_SIGNING_POLICY.md) and kept
  signing credentials outside the repository and CI logs.

## [0.1.0] - 2026-08-23

### Added

- Windows-first WPF application with screenshot paste, file selection, and visible Codex-window capture.
- Local English OCR without a cloud OCR service.
- Bounded Windows and Codex environment collection, duplicate-install clues, and time-adjacent fault events.
- Official `codex doctor --json` integration with graceful handling for success, warnings, failure, unsupported versions, timeout, malformed output, and schema changes.
- Conservative fixed-rule diagnosis and a four-part plain-language result page.
- Explainable matching against public `openai/codex` issues, with a redacted browser fallback when GitHub search is unavailable.
- Second-pass privacy redaction, public-material preview, and a separate privacy-review file.
- Twenty executable regression tests and seventeen synthetic UI acceptance scenarios.
- Stable error phrases remain searchable after path redaction; ordinary menu screenshots and changing version numbers do not trigger public searches.
- Tied diagnoses stay visibly uncertain, unknown doctor checks remain evidence rather than becoming a false root cause, and privacy counts stay consistent across screen and export.
- Browser windows are excluded from Codex-window capture candidates, ambiguous desktop matches fail safely, and Windows collection runs outside the UI thread with a time limit.
- Self-contained Windows x64 portable package and per-user Inno Setup installer.
- Public source, CI, release automation, issue templates, privacy policy, security policy, and third-party notices.

### Security

- The app does not directly read `auth.json`, complete sessions, prompts, project source, or the entire `.codex` directory.
- The original screenshot is not saved or included in exported public material by default.
- Normal operation does not call the OpenAI API, require an API key, or invoke a model.
- Network access is limited to public read-only status and issue-search requests using redacted terms.

### Known limitations

- Windows x64 only; Windows 11 is the primary acceptance target.
- Local screenshot OCR is primarily for common English error text.
- Rules and redaction cannot guarantee a confirmed root cause or identify every private name.
- The v0.1.0 installer is unsigned and may show a Windows unknown-publisher warning.

[Unreleased]: https://github.com/djshitiancai2023-commits/codex-sos/compare/v0.1.6...HEAD
[0.1.6]: https://github.com/djshitiancai2023-commits/codex-sos/releases/tag/v0.1.6
[0.1.5]: https://github.com/djshitiancai2023-commits/codex-sos/releases/tag/v0.1.5
[0.1.4]: https://github.com/djshitiancai2023-commits/codex-sos/releases/tag/v0.1.4
[0.1.3]: https://github.com/djshitiancai2023-commits/codex-sos/releases/tag/v0.1.3
[0.1.2]: https://github.com/djshitiancai2023-commits/codex-sos/releases/tag/v0.1.2
[0.1.0]: https://github.com/djshitiancai2023-commits/codex-sos/releases/tag/v0.1.0
