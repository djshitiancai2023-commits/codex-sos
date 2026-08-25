# Code signing policy

Codex SOS is an unofficial community project and is not affiliated with OpenAI.

## Provider

The project intends to use the following open-source signing arrangement when
the project is accepted:

> Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

The certificate belongs to SignPath Foundation. Codex SOS does not hold or
export a private signing key. Only artifacts built by the public GitHub Actions
workflow from this repository may be submitted for signing.

## Roles

- Authors and maintainers: the `djshitiancai2023-commits` repository owner.
- Reviewers: the same maintainer until additional trusted maintainers are
  publicly listed in this file.
- Release approver: the same maintainer, with every signing approval recorded
  by the signing service.

Changes to source code, build scripts, workflow files, and this policy are part
of the review boundary. Signing credentials and service tokens are never stored
in the repository or printed in CI logs.

## What is signed

For each public Windows x64 release, the signed files are:

- the `CodexSOS.exe` application inside the portable package;
- the outer Inno Setup installer.

The release ZIP, installer, and checksum file are generated only after signing.
Third-party runtime libraries may retain their upstream signatures or remain
unsigned when they are merely bundled dependencies.

## Privacy and trust

Codex SOS is local-first. Its privacy boundary is described in
[PRIVACY.md](PRIVACY.md). The signing service receives only the build artifact
and the metadata required to verify that it came from this public repository;
it does not receive user screenshots, Codex sessions, prompts, or diagnostic
reports.

A valid signature identifies the signing certificate and protects integrity. It
does not prove that Codex SOS can diagnose every failure, and a new Windows
program may still receive a SmartScreen reputation warning.
