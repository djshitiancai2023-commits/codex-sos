# Changelog

The project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Planned

- Maintenance updates based on real user feedback and future `codex doctor` schema changes.

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

[Unreleased]: https://github.com/djshitiancai2023-commits/codex-sos/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/djshitiancai2023-commits/codex-sos/releases/tag/v0.1.0
