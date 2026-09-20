<!-- Repository current release marker: v0.1.8 -->

# Codex SOS

![Codex SOS banner](docs/assets/codex-sos-banner.png)

[![Build and test](https://github.com/djshitiancai2023-commits/codex-sos/actions/workflows/ci.yml/badge.svg)](https://github.com/djshitiancai2023-commits/codex-sos/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/djshitiancai2023-commits/codex-sos)](https://github.com/djshitiancai2023-commits/codex-sos/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

> **Codex 一卡住，按一下救生圈。 / When Codex gets stuck, press the lifebuoy.**

Turn a Codex error screenshot or one-sentence description into **privacy-reviewed troubleshooting material and a cautious next step**—without finding logs, running commands, or learning GitHub. Codex SOS is an unofficial, local-first Windows helper built around the official `codex doctor --json` when available. It also helps avoid duplicate reports and sending another application's problems to Codex maintainers.

**No API key. No model call. No automatic repair or posting.**

[中文说明](README.zh-CN.md) · [Download the latest Windows release](https://github.com/djshitiancai2023-commits/codex-sos/releases/latest) · [Privacy](PRIVACY.md) · [Security](SECURITY.md) · [Code signing policy](CODE_SIGNING_POLICY.md)

> **Unofficial community project. Not affiliated with or endorsed by OpenAI.** Codex SOS does not use OpenAI branding or logos.

## Quick download (Windows x64)

[Download the portable ZIP](https://github.com/djshitiancai2023-commits/codex-sos/releases/download/v0.1.8/Codex-SOS-0.1.8-win-x64-portable.zip)

Directly download the ZIP. Then right-click it, choose **Extract All**, and double-click `CodexSOS.exe`. No GitHub sign-in or star is required.

The installer is optional when you want a Start-menu entry and an uninstaller.

## Why it exists

A useful support report needs more than a screenshot. Codex SOS helps users gather the relevant context, check what is safe to share, and choose the right place to ask for help. It is designed to reduce missing information, duplicate reports, and unrelated reports—not to replace official support or promise a repair.

| What happened | What Codex SOS adds |
|---|---|
| The user only knows “Codex got stuck” | Bounded diagnostics and a draft that can be copied into an existing Support conversation |
| `codex doctor` is green, but the task still failed | Keeps the visible symptom separate from the current diagnostic result; does not declare the problem solved |
| Feedback upload failed, or the issue belongs to another app | Does not claim delivery, and avoids routing clearly unrelated problems to Codex |

**See it before downloading:** [three source-backed examples and verification](docs/EXAMPLES_AND_VERIFICATION.md). These are clearly labelled synthetic test examples, not customer testimonials or screenshots of a new test run.

**Current release snapshot:** v0.1.8, three UI languages, and **31/31 automated test groups passed** in the local candidate verification. Visible Windows mouse-and-keyboard acceptance is still being completed; automated tests are not a claim of live Codex or every UI combination. [How the project is maintained](CONTRIBUTING.md#maintenance-ownership-and-evidence).

## What the user does

1. Open Codex SOS.
2. Paste/select a screenshot **or** write one sentence about what happened.
3. Click the large check button.

You can paste with the button or Ctrl+V, or drop one local image into the screenshot area. Remove the image without losing the description. **Check again** reuses the current input; **New problem (clear)** is the action that clears it. If a check takes too long, **Stop waiting** keeps the input and returns control. The selected language is remembered on the next launch.

The result page answers four questions:

1. What kind of problem might this be?
2. Have other users reported something similar?
3. What is the safest next step?
4. What can be saved if more help is needed?

## What it checks

- Local OCR for common English error text in a user-provided screenshot
- Windows and Codex version, running state, and obvious duplicate-install clues
- Official `codex doctor --json`, with graceful handling for unsupported, failed, timed-out, malformed, and changed output
- A narrow time window of Windows application fault events related to Codex
- OpenAI's public service-status page
- Public `openai/codex` issues using a small set of redacted, stable error terms
- Explainable fixed rules for conservative classification and safe next steps
- A second redaction pass before any report is saved
- After the privacy preview, the recommended action copies a support-ready draft without opening a browser or requiring GitHub sign-in; a separate action can open the public Codex App bug form
- The preview shows the exact text copied by the copy-only action; saving and opening the public form are secondary choices, and Codex SOS never submits for the user
- The copied draft also carries concise OpenAI follow-up notes: incident time and time zone, visible-error status, optional private Feedback ID guidance, and scope/log reminders—without another form or a forced reproduction
- A visible `feedback upload failed` message can be matched against similar public reports, while an unchanged Feedback ID is never treated as proof that anything was sent
- Descriptions that clearly concern a browser, web page, startup item, or another program stay local and are not routed to Codex issue search or reporting

## Privacy and networking

- The original screenshot is processed locally and is not included in the public report by default.
- Codex SOS does not open `auth.json`, full conversations, prompts, session files, or project source code.
- It does not call the OpenAI API or any model.
- To check public service health, it reads OpenAI's public status page.
- To look for known problems, it may send a few redacted error terms to GitHub's public issue search. The original screenshot, full description, and full report are not sent.
- Reports are not posted automatically. The copy-only action does not open GitHub. If the user explicitly opens the official bug form, the report is copied only to the local clipboard; no report text is put in the URL, uploaded, pasted, or submitted by Codex SOS.
- When the evidence clearly points to another program, Codex SOS does not search Codex issues or offer the official Codex bug form.
- The optional public GitHub form may require sign-in and a user-selected subscription plan. Copying a report for an existing Support email, diagnosis, and local saving do not.

Automatic redaction is not a guarantee. Review exported material before publishing it. See [PRIVACY.md](PRIVACY.md) for the exact boundary.

## Code signing

Public Windows releases use the signing policy in [CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md).
The v0.1.0 through v0.1.8 releases are unsigned. A later release will only
be described as signed after its installer and portable application pass
Windows Authenticode verification.

## Download

For ordinary Windows users, start with the [portable ZIP](https://github.com/djshitiancai2023-commits/codex-sos/releases/download/v0.1.8/Codex-SOS-0.1.8-win-x64-portable.zip). It runs without installation:

- `Codex-SOS-0.1.8-win-x64-portable.zip` — download, choose **Extract All**, then double-click `CodexSOS.exe`
- `Codex-SOS-Setup-0.1.8.exe` — optional when you want a Start-menu entry and an uninstaller
- `SHA256SUMS.txt` — integrity checksums

The current public builds are unsigned, so Windows may show an unknown-publisher warning. Verify the SHA-256 checksum from the release page and do not disable Windows security protections to bypass a warning.

## What it will not do

- Delete caches, sessions, databases, or Codex data
- Reinstall Codex, reset sign-in, or change network settings
- Read the complete `.codex` directory or user projects
- Automatically create or comment on a GitHub issue
- Claim that a probable category is a confirmed root cause
- Claim that Codex is healthy just because `codex doctor` is green

## Current scope

- Windows x64 first, with Windows 11 as the primary acceptance target
- Common English error text for local screenshot OCR; Chinese descriptions can be typed directly
- Simplified Chinese by default, with a prominent switch for Traditional Chinese and English
- Fixed, explainable diagnostic rules rather than model-generated diagnosis
- Unsigned Windows builds through v0.1.8

## Build and test

The repository contains the original application source, executable tests with synthetic fixtures, the Windows packaging script, and the Inno Setup installer definition.

```powershell
pwsh ./scripts/check-source-secrets.ps1
pwsh ./scripts/test.ps1
pwsh ./scripts/build-release.ps1 -Version 0.1.8
```

See [BUILDING.md](BUILDING.md), [CONTRIBUTING.md](CONTRIBUTING.md), and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Security

Never attach real tokens, `auth.json`, full sessions, customer data, proprietary project material, or unredacted screenshots to a public issue. Use a fully synthetic example and follow [SECURITY.md](SECURITY.md).

## License

Codex SOS is released under the [MIT License](LICENSE). Bundled third-party components retain their own licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
