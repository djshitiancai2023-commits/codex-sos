# See what Codex SOS does — without installing it

[English home](../README.md) · [中文首页](../README.zh-CN.md) · [current public v0.1.8](https://github.com/djshitiancai2023-commits/codex-sos/releases/tag/v0.1.8) · [historical v0.1.6 snapshot](https://github.com/djshitiancai2023-commits/codex-sos/releases/tag/v0.1.6)

**Evidence snapshot: v0.1.6, commit `2e90dc0563f14a030c1262939a383d7afcb9a307`.**

The current local `v0.1.9` candidate adds six small input, status, language, and feedback-routing regressions. Its executable test run is **34/34 passed; 0 failed**. That is a new automated result, not a visible Windows mouse-and-keyboard session or a real-account doctor run; the public download remains v0.1.8 until a separately authorized release.

The examples below are readable summaries of this version's public synthetic fixtures and regression checks. They are **not** real customer incidents, newly executed desktop recordings, or screenshots of the program. They do not establish user numbers, diagnosis accuracy, or time saved.

## 1. Official feedback upload failed

**Input from the fictional scenario:**

> Codex 里的反馈传不上去，画面显示 Feedback upload failed，反馈编号没有变化。

Mock image text: `Feedback upload failed. The upload failed, but your Feedback ID is unchanged.` The fixture supplies this text; it is not proof of a real OCR run.

**Expected behavior:** the diagnostic remains uncertain; the stable error phrase can be used to look for similar public reports. An unchanged Feedback ID is never treated as proof of delivery. After privacy review, the recommended action copies a support-ready draft without opening GitHub. The user decides whether and where to paste it. No retry, upload, email, or issue submission is performed automatically.

**Why it matters:** a failed reporting flow should not leave a non-technical user repeatedly retrying or thinking a report was delivered.

[Scenario input and expectations](../tests/fixtures/scenarios/20-feedback-upload-failed.json) · [Feedback draft implementation](../src/CodexSOS.Core/OfficialFeedbackBuilder.cs) · [v0.1.6 release notes](release-notes/v0.1.6.md)

## 2. The diagnostic report is green, but the user saw a failure

**Input from the fictional scenario:**

> 刚才 Codex 明显不正常，现在又恢复了。

The supplied diagnostic output reports `overallStatus: ok`.

**Expected behavior:** distinguish “the current check found no problem” from “the user's problem is solved.” The scenario requires `这份检查无法解释当前故障` and forbids `Codex 没问题`. There is no automatic deletion, reset, or repair.

**Why it matters:** a snapshot of the current environment cannot disprove a failure that happened earlier.

[Scenario input and expectations](../tests/fixtures/scenarios/03-doctor-green-unexplained.json) · [Diagnosis rules](../src/CodexSOS.Core/DiagnosisEngine.cs)

## 3. A startup window belongs to another program

**Input from the fictional scenario:**

> 最近几天每次开机都会弹出一个没有任何文字的黑窗口，卡在那里。我怀疑是 Orchid Workbench Fixture 的开机启动程序，这正常吗？

`Orchid Workbench Fixture` is a fictional name, not a private application or customer.

**Expected behavior:** explain that the available evidence does not identify a Codex problem, keep the report local, send no Codex issue search, and withhold the official Codex feedback action. The scenario requires `先确认这个窗口属于哪个程序` rather than telling the user to restart Codex.

**Why it matters:** helps avoid sending unrelated reports to Codex maintainers. This is an implemented boundary, not a measured claim about reduced support workload.

[Scenario input and expectations](../tests/fixtures/scenarios/19-external-startup-window.json) · [v0.1.4 release notes](release-notes/v0.1.4.md)

## What is publicly verified

| Evidence | What it demonstrates | What it does not demonstrate |
|---|---|---|
| [Windows release run, September 4](https://github.com/djshitiancai2023-commits/codex-sos/actions/runs/33837154773) | The pinned public commit was built, checked, packaged, and released successfully | A new real-user incident or a new manual desktop session |
| [Executable test list](../tests/CodexSOS.Tests/Program.cs) and that run's log | `26/26 passed; 0 failed`; the count is test groups | 26 independent users, live network tests, or 26 real-mouse sessions |
| [Synthetic scenario directory](../tests/fixtures/scenarios) | 20 fictional scenario files, with explicit inputs and expectations | 20 external testers or 20 newly executed OCR tests |
| [Release assets](https://github.com/djshitiancai2023-commits/codex-sos/releases/tag/v0.1.6) | Installer, portable ZIP, checksums, manifest, and release check are published | Code signing; v0.1.6 is unsigned |

The official doctor is deliberately replaced by a controlled fake process in automated checks. This avoids touching real credentials or conversations during a build. Real window interaction, clipboard integration, live OCR, and a real Codex diagnostic run require their own dated evidence; a green build alone is not proof of those paths.

The v0.1.9 regression checks additionally cover returning to edit without reusing an old report, the input-page-only `Ctrl+Enter` shortcut, the 1200-character paste boundary, an operation status line separate from diagnostic text, preserving the progress stage during language changes, and route-specific feedback fallback wording. These checks inspect the fixed local rules and source wiring; they do not claim that every WPF interaction was manually clicked in this run.

The September 4 release run's reported result is a historical public record. This documentation refresh did not rerun Windows tests or create new screenshots, and does not retroactively certify every acceptance-matrix row.

## 中文速读

这三个例子对应的价值很简单：**反馈没传上去时不冒充成功；体检全绿时不否认用户刚才的故障；其他程序的问题不误投给 Codex。**

上面的输入来自已公开、明确标注的虚构测试资料；正文讲的是测试要求和已有代码行为，不是本轮真实截图或客户评价。你不用安装程序，也能点进资料和测试检查这些设计有没有依据。

当前公开发布记录支持“26 组自动测试通过、20 份虚构场景、安装包与便携包已经发布”。它不等于真人实测数量，也不证明全部故障都能准确诊断。真实界面录像只能用真正运行生成的素材补充，不能用示意图或模型生成图片代替。
