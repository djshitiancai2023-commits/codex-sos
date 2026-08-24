# Codex SOS 验收夹具

这里的内容全部是虚构数据，只用于自动测试和可见 UI 验收。任何名字、路径、编号、网址、故障时间和 issue 都不是用户真实资料。

## 使用约定

- `scenarios/*.json`：十条必跑的端到端场景。测试应按 `expected` 字段断言，而不是只检查“没有崩溃”。
- `images/stream-disconnected.svg`：高对比度虚构错误画面。可在本机浏览器打开后复制/截取，再粘贴到应用，验证真实剪贴板和本地 OCR。
- `privacy/redaction-cases.json`：逐条脱敏样例；原文 canary 在搜索请求、UI 公开预览和导出包中都必须为零命中。
- `boundaries/forbidden-access.json`：读文件和联网边界。测试路径中的 `{TEMP_ROOT}` 必须替换为测试临时目录，不能指向真实用户目录。
- `schema/scenario.schema.json`：场景格式约束。

## 运行规则

1. 每次运行先创建全新的临时目录，再写入 fixture 自带的虚构资料。
2. 官方体检由 mock `codex` 子进程模拟，不能对测试机的真实 Codex 运行体检。
3. GitHub 搜索只使用 fixture 内的离线返回值；UI 真实验收也不需要访问网络。
4. `expected.mustNotContain` 中任一文字出现即失败。
5. `expected.forbiddenActions` 中任一动作被执行即失败。
6. 所有场景都必须验证：没有读取禁止路径、没有上传截图/日志、没有调用 OpenAI API。

这些夹具描述的是产品应有行为；当实现与夹具冲突时，不能为了让测试变绿而降低隐私或防误诊要求。
