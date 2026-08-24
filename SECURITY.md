# Security Policy

## Supported versions

| Version | Security updates |
|---|---|
| 0.1.x | Supported |
| Earlier or unofficial builds | Not guaranteed |

## Report a vulnerability privately

Use GitHub Security Advisories and select **Report a vulnerability** for this repository. Do not post secrets or private diagnostic material in a public issue.

A report should use fully synthetic data and include:

- affected Codex SOS and Windows versions
- the smallest safe reproduction
- expected and actual behavior
- possible impact
- any safe temporary mitigation

Never send:

- `auth.json`, cookies, tokens, passwords, or API keys
- full Codex conversations, prompts, sessions, or project source
- unredacted screenshots, logs, or crash dumps
- real customer, employer, or internal product information

When demonstrating a redaction problem, replace the real value with a fixture such as `C:\Users\Avery.Fixture\Documents\Project-Nebula-Fixture\trace.log`.

## High-priority security issues

- a screenshot, raw doctor output, fault event, or description is uploaded unexpectedly
- the app directly reads account files, sessions, or project content
- exported material leaks common credentials or local identity after redaction
- an untrusted look-alike executable is treated as Codex
- a timed-out official doctor process is started repeatedly
- public-search code performs write, comment, or account operations
- the installer is tampered with or embeds local developer data
- crafted input causes arbitrary file access, file writes, or command execution

## Response targets

Maintainers aim to acknowledge a report within 7 days and provide an initial impact assessment within 14 days. Actual remediation depends on reproducibility and severity. Unverified findings will be marked as unverified rather than dismissed as safe.

## Security boundary

- Codex SOS does not directly read Codex account, session, or project files.
- The official `codex doctor --json` command runs as an external official diagnostic and may perform its own state and network checks.
- SOS waits up to 25 seconds for the UI result, does not start a second doctor process in the same run, and allows the official process to end naturally.
- Only allow-listed fields are displayed or exported, followed by another redaction pass.
- Automatic networking is limited to public read-only GET requests.
- Automatic repair, deletion, reset, reinstall, and posting are outside the v0.1 scope.

These controls reduce risk but do not guarantee perfect redaction or diagnosis. Download only from this repository's Releases page and verify the published SHA-256 checksum.
