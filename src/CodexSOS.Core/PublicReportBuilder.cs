using System.Text;

namespace CodexSOS.Core;

public sealed class PublicReportBuilder
{
    private readonly PrivacyRedactor _redactor;

    public PublicReportBuilder(PrivacyRedactor redactor) => _redactor = redactor;

    public (string PublicReport, string PrivacyReview, IReadOnlyList<PrivacyFinding> Findings) Build(
        Guid runId,
        DateTimeOffset createdAt,
        UserEvidence evidence,
        SystemFacts system,
        DoctorResult doctor,
        Diagnosis diagnosis,
        SimilarIssueSummary similarIssues,
        ServiceStatusResult serviceStatus,
        IReadOnlyList<FaultEvent> faultEvents)
    {
        var report = new StringBuilder();
        report.AppendLine("# Codex SOS 检查材料");
        report.AppendLine();
        report.AppendLine("> 由非官方社区工具 Codex SOS 在本机整理。请在公开前查看隐私复核说明。");
        report.AppendLine();
        report.AppendLine("## 发生了什么");
        report.AppendLine();
        report.AppendLine(string.IsNullOrWhiteSpace(evidence.Description)
            ? "用户没有补充文字描述。"
            : evidence.Description.Trim());
        if (!string.IsNullOrWhiteSpace(evidence.OcrText))
        {
            report.AppendLine();
            report.AppendLine("从截图中识别出的错误文字：");
            report.AppendLine();
            report.AppendLine($"> {evidence.OcrText.Trim().Replace("\n", "\n> ", StringComparison.Ordinal)}");
        }

        report.AppendLine();
        report.AppendLine("## 检查结论");
        report.AppendLine();
        report.AppendLine(diagnosis.PlainSummary);
        report.AppendLine();
        report.AppendLine($"建议：{diagnosis.SafeNextStep}");
        report.AppendLine();
        report.AppendLine("依据：");
        foreach (var item in diagnosis.Evidence)
        {
            report.AppendLine($"- {item}");
        }

        report.AppendLine();
        report.AppendLine("## 环境");
        report.AppendLine();
        report.AppendLine($"- Windows：{system.WindowsVersion}");
        report.AppendLine($"- 电脑类型：{system.Architecture}");
        report.AppendLine($"- 使用方式：{SurfaceName(system.Surface)}");
        report.AppendLine($"- Codex 版本：{doctor.CodexVersion ?? system.CodexVersion ?? "暂时无法确定"}");
        report.AppendLine($"- Codex 是否正在运行：{(system.CodexIsRunning ? "是" : "没有发现")}");
        report.AppendLine($"- 可能存在不同安装：{(system.PossibleDuplicateInstall ? "可能有关" : "暂未发现")}");

        report.AppendLine();
        report.AppendLine("## Codex 官方体检");
        report.AppendLine();
        report.AppendLine(doctor.PublicSummary);
        foreach (var check in doctor.Checks.Where(c => c.Status is "warning" or "fail").Take(12))
        {
            report.AppendLine($"- {check.Id}：{check.Summary}");
        }

        report.AppendLine();
        report.AppendLine("## 官方服务状态");
        report.AppendLine();
        report.AppendLine(serviceStatus.Succeeded
            ? $"{serviceStatus.Description}（这是整体状态，不能排除个人或单个任务的故障。）"
            : "暂时无法读取官方服务状态；这不会阻止本机检查。" );

        report.AppendLine();
        report.AppendLine("## 相似的公开问题");
        report.AppendLine();
        report.AppendLine(similarIssues.PlainSummary);
        foreach (var match in similarIssues.Matches.Take(5))
        {
            report.AppendLine($"- [#{match.Issue.Number} {match.Issue.Title}]({match.Issue.HtmlUrl}) — {TierName(match.Tier)}；依据：{string.Join("、", match.Reasons)}");
        }

        if (faultEvents.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("## 故障时间附近的 Windows 记录");
            report.AppendLine();
            foreach (var faultEvent in faultEvents.Take(8))
            {
                report.AppendLine($"- {faultEvent.Timestamp:O}：{faultEvent.Application} / {faultEvent.FaultModule ?? "未提供模块"} / {faultEvent.ExceptionCode ?? "未提供代码"}");
            }
        }

        report.AppendLine();
        report.AppendLine("## 边界");
        report.AppendLine();
        report.AppendLine("- 这些线索不能证明已经找到根因。没有足够证据时，Codex SOS 会明确说无法判断。");
        report.AppendLine("- 原截图没有放进这份公开材料。");
        report.AppendLine("- Codex SOS 没有读取完整对话、提示词、项目代码或 auth.json。");
        report.AppendLine("- Codex 官方体检由 Codex 自己运行；SOS 只处理其返回结果。");
        report.AppendLine("- 没找到相似问题，不代表从来没人遇到过。");
        report.AppendLine();
        report.AppendLine($"材料编号：{runId.ToString("N")[..8]} · 生成时间：{createdAt:O}");

        // Last line of defence: the complete assembled report is redacted again.
        var privacyResult = _redactor.Redact(report.ToString());
        var review = BuildPrivacyReview(privacyResult.Findings);
        return (privacyResult.SanitizedText, review, privacyResult.Findings);
    }

    public static string BuildPrivacyReview(IReadOnlyList<PrivacyFinding> findings)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# 隐私复核说明");
        builder.AppendLine();
        builder.AppendLine("## 我们准备公开这些信息");
        builder.AppendLine();
        builder.AppendLine("- 你主动写下的一句话（已检查隐私）");
        builder.AppendLine("- 截图里识别出的错误文字（已检查隐私，不包含原截图）");
        builder.AppendLine("- Windows 和 Codex 的版本、官方体检结论、少量故障记录");
        builder.AppendLine("- 相似公开问题的链接和匹配依据");
        builder.AppendLine();
        builder.AppendLine("## 不会包含");
        builder.AppendLine();
        builder.AppendLine("- 原截图、完整聊天、提示词、项目代码、账号文件、密钥或 cookie");
        builder.AppendLine();
        if (findings.Count == 0)
        {
            builder.AppendLine("自动检查暂未发现需要遮住的内容。");
        }
        else
        {
            builder.AppendLine($"自动检查一共遮住了 {findings.Sum(item => item.Count)} 处可能的私人信息：");
            foreach (var finding in findings)
            {
                builder.AppendLine($"- {FriendlyPrivacyName(finding.Kind)}：{finding.Count} 处");
            }
        }

        builder.AppendLine();
        builder.AppendLine("自动遮盖不能保证 100% 安全。保存或复制前，请快速看一眼上面的公开材料。Codex SOS 不会自动发布。 ");
        return builder.ToString();
    }

    private static string SurfaceName(CodexSurface surface) => surface switch
    {
        CodexSurface.Desktop => "Codex 桌面版",
        CodexSurface.Cli => "Codex 命令行版",
        _ => "只能确定为 Codex"
    };

    private static string TierName(IssueSimilarityTier tier) => tier switch
    {
        IssueSimilarityTier.High => "高度相似",
        IssueSimilarityTier.Possible => "可能相关",
        _ => "只是关键词相同"
    };

    private static string FriendlyPrivacyName(string kind) => kind switch
    {
        "email" => "邮箱",
        "local_path" => "本机路径",
        "private_git_remote" => "私人代码地址",
        "internal_url" => "内部网址",
        "private_ip" => "内部网络地址",
        "session_id" => "任务编号",
        "request_id" => "请求编号",
        "account_id" => "账号编号",
        "secret" => "疑似密钥或密码",
        "possible_secret" => "长随机字符串",
        _ => "可能的私人信息"
    };
}
