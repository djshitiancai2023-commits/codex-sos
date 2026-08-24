# Gate 1｜会改变 v0.1 的公开事实

事实快照：2026-08-13。这里只保留会改变 Codex SOS v0.1 的结论，不构成竞品报告。

## 已核实事实

1. 当前官方 `codex doctor` 是只读为主、不会自动修复的诊断命令。它检查安装、配置、认证、网络、WebSocket、MCP 和本地状态等，`--json` 输出带 `schemaVersion`；检查失败可返回非零退出码。
2. “输出已脱敏”不等于“官方检查内部没有接触敏感位置”。当前 doctor 源码会读取认证存储状态，并递归遍历 `sessions`、`archived_sessions` 的 rollout 文件元数据；它还会执行有界联网探针。这是 doctor 自身的官方内部行为，不是 Codex SOS 直接读取这些材料。
3. 官方 CLI issue 模板只在版本支持时建议附上 `codex doctor --json`，并明确要求提交前人工复核；不支持时可写 `not available`。
4. doctor 全绿不能证明 Codex 没故障。公开复现中存在 doctor 全绿，但 Windows sandbox 运行时仍失败的情况。因此结果页只能说“官方体检暂未发现异常；这份检查无法解释当前故障”。
5. doctor 自身可能不支持、很慢、超时、返回非零或产生无法解析的输出。任何一种情况都不能让 Codex SOS 一起失败，也不能触发无限重试。
6. WebSocket 握手成功只说明当时能建立连接，不能排除长任务稍后断流。截图、用户描述和故障时间线必须与 doctor 结果并列，不能被它覆盖。
7. Windows Desktop 真实故障可能表现为高 CPU、界面冻结、进程崩溃或任务显示 `interrupted`；模块名和异常码只是现象证据，不是已确认根因。
8. `Failed to resume task` 与 `Invalid request: AbsolutePathBuf deserialized without a base path` 是可稳定识别的任务恢复异常信号。遇到此类信号，安全建议应优先保留会话和本地数据，禁止自动删除或重置。
9. Windows 上 App、standalone CLI 和旧 npm 安装可能并存；但同一 npm 安装产生的 `codex` 与 `codex.cmd` 不能仅凭两条 PATH 结果就判为重复安装。只有不同真实安装根或版本形成独立聚类时，才提示“可能存在重复安装”。
10. OpenAI 状态页给出 Codex 聚合状态，并明确个人可用性可能不同。状态页绿灯只能作为旁证，不能排除个人、地区、版本或单个任务故障。

公开 issue 是用户对症状和复现过程的一手记录，不等于 OpenAI 维护者已确认根因。分类规则只能输出“很可能有关”“可能有关”“暂未发现”或“这份检查无法解释当前故障”。

## v0.1 产品裁决

- Codex SOS 不直接打开或解析 `auth.json`、完整会话、提示词、项目代码、session rollout 或账号材料。
- Codex SOS 自动启动本机官方 `codex doctor --json`，并把它当作官方黑箱子进程；SOS 只接收其标准输出、标准错误和退出状态。
- 界面只正常显示检查进度，不为 doctor 弹出额外确认框或隐私警告。doctor 内部读取认证存储状态、遍历会话文件元数据和执行网络探针的行为属于官方检查实现，不转嫁给普通用户判断或操作。
- doctor 必须有墙钟超时和输出大小上限；不支持、失败、超时或损坏输出分别记录为独立状态，随后继续生成结果页。不得自动重跑。
- doctor 原始结果只进入本机临时私有材料；展示或导出前再做 Codex SOS 二次脱敏和用户可读预览。
- 即使 doctor 全绿，当前故障仍保持独立结论；不得显示“Codex 没问题”或“已经确定根因”。

## 10 个一手来源

1. [当前 doctor 固定源码快照](https://github.com/openai/codex/blob/b1373b74a27d1d9b65074a873202683355cae772/codex-rs/cli/src/doctor.rs)
2. [当前 CLI bug issue 模板固定快照](https://github.com/openai/codex/blob/b1373b74a27d1d9b65074a873202683355cae772/.github/ISSUE_TEMPLATE/3-cli.yml)
3. [OpenAI 官方状态页](https://status.openai.com/)
4. [doctor 全绿但 Windows sandbox 仍失败：openai/codex#24098](https://github.com/openai/codex/issues/24098)
5. [doctor 冷启动可耗时数分钟：openai/codex#28166](https://github.com/openai/codex/issues/28166)
6. [旧版不支持 `doctor --json`：openai/codex#23320](https://github.com/openai/codex/issues/23320)
7. [握手成功后仍发生 WebSocket idle timeout：openai/codex#28579](https://github.com/openai/codex/issues/28579)
8. [Windows Desktop 高 CPU、冻结和 KERNELBASE 崩溃：openai/codex#34907](https://github.com/openai/codex/issues/34907)
9. [Windows Desktop reconnect 后任务恢复空路径：openai/codex#33697](https://github.com/openai/codex/issues/33697)
10. [Windows 多安装与本地状态分散：openai/codex#27230](https://github.com/openai/codex/issues/27230)
