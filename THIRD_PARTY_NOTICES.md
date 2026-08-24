# 第三方软件说明

Codex SOS 自身使用 [MIT License](LICENSE)。下面列出 v0.1 源码和 Windows 发布包中直接使用或随包分发的主要第三方组件。Windows 发布包同时保留本项目许可、此说明和构建过程中收集到的直接依赖许可材料。

## TesserNet 0.8.0

- 用途　把截图像素交给本机 Tesseract 引擎，识别常见英文错误文字
- 项目　<https://github.com/CptWesley/TesserNet>
- 软件包　<https://www.nuget.org/packages/TesserNet/0.8.0>
- 许可证　Apache License 2.0
- 作者　Wesley Baartman

TesserNet 声明其 Windows 包带有运行所需的本机库和英文识别模型，因此最终用户不需要单独安装 OCR。这个依赖减少了安装步骤，但版本较旧。v0.1.0 已完成离线识别和 Windows 验收；该依赖版本较旧，后续维护会继续关注兼容性、上游状态和安全公告。若未来替换，也不会悄悄改用云端识字。

TesserNet 软件包还包含下面两个上游项目。

### Tesseract OCR

- 项目　<https://github.com/tesseract-ocr/tesseract>
- 许可证　Apache License 2.0

### Leptonica

- 项目　<http://www.leptonica.org/>
- 许可证　BSD 2-Clause License

构建脚本会从实际锁定的软件包提取可用许可文字；本页用于说明直接依赖和来源，不替代上游许可要求。

## Microsoft .NET SDK 10.0.400 / Runtime 10.0.11 与 WPF

- 用途　应用运行环境、Windows 桌面界面、系统与网络基础库
- 项目　<https://github.com/dotnet/runtime>、<https://github.com/dotnet/wpf>
- 许可证　MIT License，另含各项目仓库列出的第三方组件许可证

Codex SOS 使用自带运行环境的方式发布，因此用户不需要另装 .NET。v0.1.0 使用 `global.json` 固定 SDK 10.0.400，并以自带 .NET Runtime 10.0.11 的方式发布。Microsoft 组件适用 MIT 许可证及其仓库列出的第三方说明。

## Inno Setup 6

- 用途　在维护者电脑上把已经验收的便携目录制作成 Windows 安装程序
- 项目　<https://jrsoftware.org/isinfo.php>
- 许可证　Inno Setup License

Inno Setup 是可选构建工具，不由 Codex SOS 自动下载。构建者需要自行安装并遵守其许可证。生成的安装程序应保留 Inno Setup 要求的版权与许可说明。

## GitHub Actions

CI 配置引用以下公开 Action。它们只在 GitHub 的构建环境中运行，不进入用户安装包。

- `actions/checkout@v7`
- `actions/setup-dotnet@v6`
- `actions/upload-artifact@v7`

每个 Action 受其仓库许可证和 GitHub Actions 服务条款约束。工作流使用最小权限；发布工作流仅在创建 GitHub Release 时取得 `contents: write`。

## 维护规则

新增、升级或移除依赖时，必须同步更新本文件，并在 Pull Request 中写清下面这些信息。

- 用户得到的直接收益
- 是否增加联网、安装步骤或发布体积
- 许可证和需要随包保留的文字
- 当前维护状态和已知安全风险
- 离线测试与替换方案

项目不使用 OpenAI SDK、云端 OCR 或大模型依赖。
