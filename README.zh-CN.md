# Codex SOS 中文说明

![Codex SOS 横幅](docs/assets/codex-sos-banner.png)

[返回英文首页](README.md) · [下载最新版](https://github.com/djshitiancai2023-commits/codex-sos/releases/latest)

> **Codex 一卡住，按一下救生圈。**

截一下错误，或随便写一句发生了什么。Codex SOS 会在这台电脑上完成官方体检、寻找相似问题、检查隐私，然后告诉你下一步怎么做。

**不用找日志，不用懂命令，不用会 GitHub。**

> **非官方社区工具，与 OpenAI 无隶属关系。** 本项目不使用 OpenAI 官方 Logo，也不代表 OpenAI 提供支持或担保。

## 你只需要做两步

1. 双击打开 Codex SOS。
2. 粘贴一张报错截图，或写一句发生了什么，然后点“帮我看看”。

接下来会自动完成这些事情。

- 在本机识别截图里的英文错误文字
- 查看 Windows、Codex 版本、运行状态和明显的重复安装线索
- 运行 Codex 自带的官方体检
- 查看故障发生前后的少量 Windows 崩溃记录
- 查看 OpenAI 官方服务状态
- 在 `openai/codex` 的公开问题里寻找高度相似情况
- 用固定规则给出保守判断和安全下一步
- 再做一次隐私遮盖，并准备可公开的问题材料

结果页只先回答四件事。

1. 这大概是什么
2. 是不是只有我
3. 现在最安全怎么做
4. 还不行怎么办

![Codex SOS 使用流程](docs/assets/readme-flow.svg)

## 默认保护什么

- 截图只在本机识别，原图默认不保存、不上传，也不会放进公开材料。
- Codex SOS 自己不打开账号文件、完整聊天、提示词、会话记录或用户项目。
- 不要求 API Key，不调用 OpenAI API，正常运行不调用大模型，也不消耗用户的模型 token。
- 搜索公开问题前先遮盖私人信息，只发送少量稳定错误词。
- 材料不会自动发布。只有用户主动点“保存到电脑”才会写出报告。

Codex SOS 会自动启动 Codex 自带的官方体检。官方体检为了完成自己的检查，可能读取它需要的 Codex 状态、联网检查，并在必要时维护登录状态。SOS 不直接读取这些内容，只接收官方体检返回的信息，再做一次遮盖。

如果官方体检等待超过 25 秒，SOS 会先把其他结果给你，本次不会再次启动。已经开始的官方体检会自行结束，避免在它维护登录状态时突然中断。

自动遮盖不能保证 100% 认出人名、内部产品名等私人信息。因此，只有在用户选择保存完整材料时，界面才会请用户快速看一眼将要保存的内容。这不会打断前面的自动检查。

更多细节见 [隐私说明](PRIVACY.md)。

## 下载与运行

v0.1.0 已通过公开仓库发布。普通用户从 [GitHub Releases](https://github.com/djshitiancai2023-commits/codex-sos/releases/latest) 下载 Windows 安装包，双击安装，再从开始菜单打开。安装包会自带运行所需组件，用户不需要安装 .NET、Python、OCR 或开发工具。

发布页同时提供两种形式。

- `Codex-SOS-Setup-0.1.0.exe`，普通用户推荐，按向导安装
- `Codex-SOS-0.1.0-win-x64-portable.zip`，解压后双击 `CodexSOS.exe`

首个公开版本尚未签名，Windows 可能显示来源提醒。运行前请核对发布页上的 SHA-256。不要为了绕过提醒而关闭 Windows 安全保护。

完整图文步骤见 [中文使用说明](docs/zh-CN/USAGE.md)。

## 它不会做什么

- 不会自动删除缓存、会话或数据库
- 不会重装 Codex、重置登录或修改网络
- 不会读取完整 `.codex` 目录或项目代码
- 不会自动提交或评论 GitHub issue
- 不会根据几个关键词声称“已经确定根因”
- 不会因为官方体检全绿就说“Codex 没问题”

社区 workaround 只能作为带来源的参考，不会自动执行。

## 当前限制

- v0.1 只支持 Windows x64，重点验收 Windows 11。
- 本地截图识字首先支持常见英文错误文字。中文可直接写进描述框，中文截图识字尚未承诺。
- 截取正在打开的 Codex 依赖目标窗口可见；失败时仍可粘贴截图或选择图片。
- GitHub 公共搜索可能遇到断网或限流。此时会给出经过遮盖的浏览器搜索入口，不会谎称“没有相似问题”。
- 固定规则只能整理线索，不能保证找到真正根因。
- 未签名安装包可能触发 Windows 来源提醒。后续会根据公开项目维护情况申请开源代码签名。

## 为什么不直接截图发给聊天工具

单独发截图仍常常需要用户补版本、找日志、解释使用方式，还可能把截图和隐私一起上传。Codex SOS 会在本机提取错误文字，自动补齐最小环境信息，搜索官方公开问题，再把可公开材料与原始截图分开。

## 为什么不只运行官方体检

官方体检是重要证据，但它不认识用户这次截图里的错误，也不会替用户比较相似公开问题、整理大白话结论或准备经过二次遮盖的材料。体检全绿时，真实的断流、卡住或恢复失败仍可能存在。Codex SOS 会把官方体检与用户看到的现象分开保存，不让一份绿灯盖掉当前故障。

## 参与开发

项目采用 MIT 许可证。开发环境需要 Windows 和 .NET 10 SDK，普通用户不需要。

```powershell
pwsh ./scripts/test.ps1
pwsh ./scripts/build-release.ps1 -Version 0.1.0
```

测试与 UI 验收只能使用仓库里的虚构资料，构建脚本不会运行测试机上的真实 Codex 官方体检。贡献前请读 [CONTRIBUTING.md](CONTRIBUTING.md)、[项目架构](docs/ARCHITECTURE.md) 和 [安全政策](SECURITY.md)。

## English

Codex SOS is a Windows-first, local, community troubleshooting helper for ordinary Codex users. Paste an error screenshot or write one sentence; it runs bounded checks, searches public `openai/codex` issues, applies explainable rules, redacts the report, and suggests a conservative next step. Screenshots are not uploaded or included in public reports by default. No API key or model call is required.

This is an unofficial community project and is not affiliated with OpenAI.
