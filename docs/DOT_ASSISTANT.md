# Codex SOS · dot assistant preview

v0.2.0-preview.2 improves the task-first local page introduced in preview.1. It is a **local-connection preview**, not a replacement for official dot or a full-account cloud task manager.

## 使用起来有什么不同 / What changes

双击 `CodexSOS.exe` 后打开本机任务页。先记下一件事，点击“复制给 dot”，再到官方 dot 粘贴；记下、复制、打开入口都不等于已经发送。遇到问题可点任务旁的 SOS，或首页“截图 / 一句话求助”，使用原来的 Windows 截图、离线识字及诊断流程。关闭 SOS 窗口不关闭任务页；“关闭助手”才结束本产品组件，不会关闭 Codex。

The homepage groups tasks into Working, Needs me, Needs help and other records. Notes and imported snapshots stay on the PC. Classic SOS is retained (`CodexSOS.exe --classic`); its completed, redacted report returns to the board for preview/copy. Manual notes can be copied for dot, but this app does not execute them.

### 继续跟进一件事 / Follow up on a task

- 用“修改”补充自己的任务笔记或原任务链接，原有状态保留；无效链接不会悄悄被丢掉。官方实时状态仍只读。
- 在“找任务”中输入几个字；筛选没有结果时可以直接回到全部。“收起”只是整理这张卡片，可在“已收起”里放回，不是停止、取消或删除官方任务。上限仍是 100 张卡片，包含已收起的记录。
- 点“问进展”，可在同一个预览里选择问进展或“接着做这件事”。先看再复制到原来的 dot 对话。草稿会让 dot 先确认原任务及已有成果，避免无意中重建、重复执行；没有原任务链接时先请它确认对应任务。助手不会发送或执行草稿。
- 找不到云端任务时，展开页面提示：在 ChatGPT 的 dot 个人页面进入“活动”。[官方任务说明](https://learn.chatgpt.com/docs/dots/tasks-and-memory)。需要本机的工作应保持电脑在线且 ChatGPT 打开。

Task notes can be edited, searched, archived and restored locally. **Ask progress** opens a three-language draft preview with a **Continue this task** option. Copy the reviewed text into the existing dot conversation yourself. Archived cards remain available for follow-up; the actual task is never stopped or resumed by the assistant.

Disconnected or stale native states move to **Other records** and are labelled as previous observations. Manual note timestamps describe edits, not runtime observations. The last observed state is never presented as proof of current activity or delivery.

## What can connect

- A compatible **existing local Codex app-server**: the signed official CLI generates its current protocol schema; the observer disables all non-status notifications, verifies version agreement, and requests only loaded task identifiers. It never requests chat or turn contents. An optional loopback address connects to a service deliberately started by the user.
- The normal daemon connection is attempted automatically but is not available on every desktop installation. On the development PC it was unavailable; an isolated official runtime was separately verified. The UI explicitly says disconnected instead of showing invented tasks.
- Cloud dot tasks are **not** automatically discovered. Keep manual notes or import a selected local `dot-task-board` snapshot (maximum 100 tasks). Imports remain labelled records, not live observations.
- Source task IDs stay local. Native task titles are not fetched. The original-task action uses only validated Codex/ChatGPT links; it does not include a diagnostic report in a URL.

“一轮停止”不等于“已交付”；旧观察时间不会被页面刷新伪装成新状态。长时间没有事件不证明卡死。手工“完成”也明确标为未独立核实。

## SOS bridge and privacy

Only a reported local `systemError` can initiate the bounded generic local check (rate limited). Waiting, idle and stale states do not. Task titles, goals, links and identifiers are excluded from diagnostic/search input. Screenshot help still opens the retained native window; screenshots are not uploaded through the web page.

Browser requests use a per-launch secret plus strict loopback Host/Origin checks. Native calls have a fixed allowlist; no arbitrary path, command, upload or task-write endpoint exists. Copy binds to the exact presented, redacted report and preview identity. A stale preview or clipboard failure does not claim success. See [privacy](../PRIVACY.md) and [security](../SECURITY.md).

## Upstream and build

Uses the stable `dot-task-board` v0.2.0 snapshot/agent normalization modules, unchanged, under MIT. The homepage and native adapter are new SOS code; upstream preview execution/dispatch features are not bundled. [Source attribution](../dot-assistant/vendor/UPSTREAM.md) and [third-party notices](../THIRD_PARTY_NOTICES.md).

The package includes .NET and checksum-pinned Node 24.16.0. No Node, npm, SDK or API key setup for the user. No cloud server, model request or telemetry. Actual work in official Codex/dot still uses the user's ordinary account allowance.

## Verification

2026-10-19 preview.2 update:

- Native baseline: 36/36 passed, including real offline OCR of fictional imagery. Board tests: 20/20 passed, covering atomic edits, persistence, reversible archive, disconnected-state freshness, combined filters/search, three-language drafts and loopback/security boundaries. Final build totals are in the corresponding public CI run.
- Actual in-app browser actions exercised invalid-link input preservation, edit/save with state retention, archive/restore, empty-search recovery and progress/continuation drafts. All three UI languages copied text identical to the displayed draft.
- A 683×500 CSS viewport was used for the draft copy path and showed no horizontal overflow. It simulates a narrow usable area; physical Windows display scaling was not changed or tested.
- The browser acceptance harness used the production page/server with fictional notes and a controlled fake native bridge. New board interactions ran without task execution or external requests; native SOS regression and real OCR are separate harness results, not a fresh full WPF tour.

2026-10-13 local evidence, with fictional data:

- Native regression: **36/36**, including real offline OCR of a fictional image, existing doctor failure modes, privacy rules, exact-preview copy, stale-preview rejection and clipboard failure propagation. Build: 0 warnings, 0 errors.
- Node tests: **13/13**, including a version-mismatched server, forbidden notification rejection and malformed state flags. The exact build results are in each public CI log; later versions must not inherit this snapshot's pass.
- The production observer connected to an isolated official `codex-cli 0.162.0-alpha.17.2` server and received a real running-state event with no task title/prompt retained. Its fictional provider was restricted to an unreachable loopback address; no real account or remote model was used. This does not establish whole-account/cloud support.
- Visible in-app browser: task creation, copy and language switching; native bridge report preview and Windows clipboard matched byte-for-byte. Browser clipboard and Windows clipboard are separate in this harness, so the native clipboard was checked separately.
- Not claimed: a full repeated native WPF mouse/keyboard/OCR-paste tour, all physical Windows DPI combinations, or running doctor on a real account. These are not replaced by method-level tests.

CI builds from source and records package/privacy results. For preview installers, the release workflow verifies installed files against the manifest and uninstalls in an isolated runner directory before publishing. Read the actual [CI](https://github.com/djshitiancai2023-commits/codex-sos/actions/workflows/ci.yml) and [release workflow](https://github.com/djshitiancai2023-commits/codex-sos/actions/workflows/release.yml) outcomes; a green historical version does not prove a later build.

The preview remains unsigned. Stable releases and their assets are not replaced.
