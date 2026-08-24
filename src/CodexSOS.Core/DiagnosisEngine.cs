namespace CodexSOS.Core;

public sealed class DiagnosisEngine
{
    public Diagnosis Diagnose(
        UserEvidence evidence,
        SystemFacts system,
        DoctorResult doctor,
        IReadOnlyList<FaultEvent> faultEvents,
        ServiceStatusResult? serviceStatus = null)
    {
        var descriptionText = evidence.Description.ToLowerInvariant();
        var ocrText = evidence.OcrText.ToLowerInvariant();
        var text = $"{descriptionText}\n{ocrText}";
        var scores = Enum.GetValues<IncidentCategory>()
            .ToDictionary(category => category, _ => 0);
        var reasons = new Dictionary<IncidentCategory, List<string>>();

        void Add(IncidentCategory category, int score, string reason)
        {
            scores[category] += score;
            if (!reasons.TryGetValue(category, out var list))
            {
                list = [];
                reasons[category] = list;
            }

            list.Add(reason);
        }

        if (ContainsAny(text, "stream disconnected", "idle timeout", "websocket", "reconnecting", "连接中断", "突然断开", "连不上"))
        {
            Add(IncidentCategory.Connection, 6, "截图或描述里出现了连接中断特征");
        }

        if (ContainsAny(text, "failed to resume task", "absolutepathbuf", "resume", "interrupted", "恢复不了", "无法恢复", "续不上", "任务恢复"))
        {
            Add(IncidentCategory.TaskRecovery, 6, "截图或描述里出现了任务恢复特征");
        }
        if (ContainsAny(text, "failed to resume task", "absolutepathbuf", "点继续", "恢复不了", "无法恢复", "续不上"))
        {
            Add(IncidentCategory.TaskRecovery, 3, "恢复操作本身也没有成功");
        }

        var loginFromDescription =
            ContainsAny(descriptionText, "authentication", "login", "sign in", "unauthorized", "登录", "账号");
        var loginFromOcr = ContainsAny(ocrText, "authentication", "unauthorized") ||
            (HasErrorClue(ocrText) && ContainsAny(ocrText, "login", "sign in", "登录", "账号"));
        if (loginFromDescription || loginFromOcr)
        {
            Add(IncidentCategory.Login, 6, "截图或描述里出现了登录特征");
        }

        if (ContainsAny(text, "freeze", "not responding", "kernelbase.dll", "闪退", "卡死", "没反应", "一直转圈"))
        {
            Add(IncidentCategory.DesktopApplication, 6, "截图或描述里出现了桌面应用卡住或退出特征");
        }

        var installFromDescription = ContainsAny(
            descriptionText, "version", "update", "install", "not recognized", "版本", "安装", "更新");
        var installFromOcr = ContainsAny(ocrText, "not recognized") ||
            (HasErrorClue(ocrText) && ContainsAny(ocrText, "version", "update", "install", "版本", "安装", "更新"));
        if (installFromDescription || installFromOcr)
        {
            Add(IncidentCategory.InstallationOrVersion, 4, "截图或描述里出现了安装或版本特征");
        }

        if (system.PossibleDuplicateInstall)
        {
            Add(IncidentCategory.DuplicateInstallation, 8, "发现了不同位置且版本不同的 Codex 安装线索");
        }

        foreach (var check in doctor.Checks.Where(c => c.Status is "warning" or "fail"))
        {
            var checkText = $"{check.Id} {check.Summary}".ToLowerInvariant();
            var category = CheckCategory(checkText);
            if (category is not null)
            {
                Add(category.Value, 6, $"官方体检提示：{check.Summary}");
            }
        }

        foreach (var faultEvent in faultEvents)
        {
            var eventText = $"{faultEvent.Application} {faultEvent.FaultModule}".ToLowerInvariant();
            if (ContainsAny(eventText, "codex", "openai.codex", "chatgpt"))
            {
                Add(IncidentCategory.DesktopApplication, 8, "故障发生时间附近有 Codex 应用异常记录");
            }
        }

        if (doctor.State is DoctorState.TimedOut or DoctorState.Malformed or DoctorState.Unsupported or DoctorState.UnknownSchema)
        {
            Add(IncidentCategory.DoctorFailure, 3, "官方体检这次没有完成");
        }

        if (serviceStatus is { Succeeded: true, IsCodexSpecific: true } &&
            serviceStatus.Indicator is not ("none" or "operational"))
        {
            Add(IncidentCategory.CodexService, 4, "官方状态页当前显示服务异常");
        }

        var ordered = scores
            .Where(pair => pair.Key is not IncidentCategory.Unknown and not IncidentCategory.DoctorCannotExplain)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .ToArray();
        var best = ordered.FirstOrDefault();

        if (best.Value == 0)
        {
            const string restartStep = "先完全关闭并重新打开 Codex；暂时不要删除任务或本地数据。";
            var hasOcrText = !string.IsNullOrWhiteSpace(evidence.OcrText);

            if (LooksLikeAllClearConfirmation(evidence.Description))
            {
                return Build(IncidentCategory.Unknown, ConfidenceLevel.CannotDetermine,
                    "目前没有看到明确的故障线索；这只代表这一次快照，不能保证 Codex 一直正常。",
                    "现在不需要做任何修复操作；之后如果出现具体报错，再截一张图给 SOS 就好。",
                    ["你提到画面正常，现有检查也没有发现故障特征"],
                    ["SOS 只能根据可见线索判断，不能保证内部状态一定正常"]);
            }

            if (evidence.ScreenshotProvided && !hasOcrText)
            {
                return Build(IncidentCategory.Unknown, ConfidenceLevel.CannotDetermine,
                    "没有从这张截图里读到足够的文字，暂时不能判断，也不能把它当成正常。",
                    restartStep,
                    ["截图里没有识别到可用的文字内容"],
                    ["读不到文字时，SOS 不假装看到了证据"]);
            }

            if (hasOcrText && HasErrorClue(ocrText))
            {
                return Build(IncidentCategory.Unknown, ConfidenceLevel.CannotDetermine,
                    "截图里像是有报错，但暂时还认不出是哪一类问题。",
                    restartStep,
                    ["截图文字里有疑似报错的字样，但没有命中已知的固定错误特征"],
                    ["认不出类别时不硬猜根因", "可以补一句大白话描述，帮助下一次判断"]);
            }

            if (hasOcrText)
            {
                return Build(IncidentCategory.Unknown, ConfidenceLevel.CannotDetermine,
                    "截图里暂未看到明确报错；仅凭这张图不能确认 Codex 一定正常。",
                    restartStep,
                    ["截图里只读到了普通文字，没有看到明确报错"],
                    ["画面看起来平静不等于后台一定正常"]);
            }

            if (doctor.State == DoctorState.Ok)
            {
                return Build(IncidentCategory.DoctorCannotExplain, ConfidenceLevel.CannotDetermine,
                    "这份检查无法解释当前故障。",
                    "先完全关闭并重新打开 Codex；暂时不要删除任务或本地数据。",
                    [doctor.PublicSummary],
                    ["官方体检全绿也无法覆盖所有卡住、断流和任务恢复问题"]);
            }

            return Build(IncidentCategory.Unknown, ConfidenceLevel.CannotDetermine,
                "目前还无法判断是哪一类问题。",
                "先完全关闭并重新打开 Codex；暂时不要删除任务或本地数据。",
                ["现有信息里没有足够稳定的故障特征"],
                ["没有足够证据时，SOS 不会强行猜根因"]);
        }

        var tied = ordered
            .Where(pair => pair.Value == best.Value && pair.Value >= 5)
            .Take(3)
            .ToArray();
        if (tied.Length >= 2)
        {
            var candidateCategories = tied.Select(pair => pair.Key).ToArray();
            var candidateNames = string.Join(" 或 ", candidateCategories.Select(PlainCategory));
            var tiedEvidence = candidateCategories
                .SelectMany(category => reasons.GetValueOrDefault(category) ?? [])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var tiedLimitations = new List<string>
            {
                "两类线索目前分数相同，所以没有硬选其中一类",
                "这些是可核对的线索，不是已经确定的根因"
            };
            if (doctor.State == DoctorState.Ok)
            {
                tiedLimitations.Add("官方体检暂未发现异常，但这份检查无法解释当前故障");
            }

            return Build(
                IncidentCategory.Unknown,
                ConfidenceLevel.PossiblyRelated,
                $"可能有关：{candidateNames}。",
                "先完全关闭并重新打开 Codex；暂时不要删除任务或本地数据。",
                tiedEvidence,
                tiedLimitations,
                candidateCategories);
        }

        if (best.Key == IncidentCategory.DoctorFailure && best.Value < 5)
        {
            return Build(IncidentCategory.DoctorFailure, ConfidenceLevel.CannotDetermine,
                "Codex 官方体检这次不可用；这不足以解释当前故障。",
                "先保留当前任务和本地数据；可以查看是否有 Codex 新版本，或稍后重新检查。",
                reasons.GetValueOrDefault(best.Key) ?? ["官方体检这次没有完成"],
                ["官方体检失败不等于 Codex 本身损坏", "其他本机检查和公开问题搜索仍已继续"]);
        }

        var confidence = best.Value >= 8 && reasons.GetValueOrDefault(best.Key)?.Count >= 2
            ? ConfidenceLevel.LikelyRelated
            : best.Value >= 5
                ? ConfidenceLevel.PossiblyRelated
                : ConfidenceLevel.CannotDetermine;

        var prefix = confidence switch
        {
            ConfidenceLevel.LikelyRelated => "很可能有关",
            ConfidenceLevel.PossiblyRelated => "可能有关",
            _ => "暂时无法判断"
        };
        var summary = $"{prefix}：{PlainCategory(best.Key)}。";
        var nextStep = SafeNextStep(best.Key);
        var evidenceReasons = reasons.GetValueOrDefault(best.Key) ?? [];
        var limitations = new List<string>
        {
            "这些是可核对的线索，不是已经确定的根因"
        };
        if (doctor.State == DoctorState.Ok)
        {
            limitations.Add("官方体检暂未发现异常，但这份检查无法解释当前故障");
        }

        return Build(best.Key, confidence, summary, nextStep, evidenceReasons, limitations);
    }

    public ClarifyingQuestion? BuildOnlyIfEssentialQuestion(
        UserEvidence evidence,
        SystemFacts system,
        DoctorResult doctor,
        IReadOnlyList<FaultEvent> events)
    {
        if (!string.IsNullOrWhiteSpace(evidence.OcrText) ||
            evidence.Description.Trim().Length >= 8 || doctor.Checks.Count > 0 || events.Count > 0 ||
            system.Surface != CodexSurface.Unknown)
        {
            return null;
        }

        return new ClarifyingQuestion(
            "刚才更像是哪一种情况？",
            "推荐先选第一项，因为它最常见，也最容易给出安全的下一步。",
            [
                new("一直转圈或突然断开", "我们会优先检查连接和任务恢复", true),
                new("整个窗口卡住或退出", "我们会优先检查桌面应用异常", false),
                new("提示我重新登录", "我们会优先检查登录状态", false)
            ]);
    }

    private static Diagnosis Build(
        IncidentCategory category,
        ConfidenceLevel confidence,
        string summary,
        string nextStep,
        IReadOnlyList<string> evidence,
        IReadOnlyList<string> limitations,
        IReadOnlyList<IncidentCategory>? candidates = null) =>
        new(category, confidence, summary, nextStep, evidence, limitations, candidates);

    private static IncidentCategory? CheckCategory(string checkText)
    {
        if (ContainsAny(checkText, "auth", "login")) return IncidentCategory.Login;
        if (ContainsAny(checkText, "network", "websocket", "dns", "connection")) return IncidentCategory.Connection;
        if (ContainsAny(checkText, "install", "version", "update", "upgrade", "not recognized", "command not found")) return IncidentCategory.InstallationOrVersion;
        if (ContainsAny(checkText, "state", "resume", "session")) return IncidentCategory.TaskRecovery;
        return null;
    }

    private static string PlainCategory(IncidentCategory category) => category switch
    {
        IncidentCategory.Login => "Codex 登录问题",
        IncidentCategory.Connection => "Codex 连接中断",
        IncidentCategory.InstallationOrVersion => "Codex 安装或版本问题",
        IncidentCategory.DuplicateInstallation => "电脑里可能同时存在不同版本的 Codex",
        IncidentCategory.DesktopApplication => "Codex 桌面应用异常",
        IncidentCategory.CodexService => "Codex 后台服务异常",
        IncidentCategory.TaskRecovery => "Codex 任务恢复异常",
        IncidentCategory.DoctorFailure => "Codex 官方体检自身异常",
        _ => "暂时无法判断的问题"
    };

    private static string SafeNextStep(IncidentCategory category) => category switch
    {
        IncidentCategory.TaskRecovery => "先完全关闭并重新打开 Codex；保留原任务和本地数据，不要删除会话。",
        IncidentCategory.Connection => "先重新打开 Codex，并查看官方服务状态；不要修改系统网络设置。",
        IncidentCategory.Login => "先重新打开 Codex，确认登录页面是否恢复；不要删除账号或重置本地数据。",
        IncidentCategory.InstallationOrVersion or IncidentCategory.DuplicateInstallation =>
            "先查看是否有 Codex 新版本；不要自动卸载或覆盖现有版本。",
        IncidentCategory.DesktopApplication => "先完全关闭并重新打开 Codex；不要删除缓存、任务或本地数据。",
        _ => "先完全关闭并重新打开 Codex；暂时不要删除任务或本地数据。"
    };

    private static bool HasErrorClue(string text) =>
        ContainsAny(text, "error", "failed", "failure", "exception", "traceback", "panic",
            "cannot", "unable", "invalid", "denied", "timeout", "错误", "失败", "异常", "无法", "报错");

    private static bool LooksLikeAllClearConfirmation(string description) =>
        ContainsAny(description.ToLowerInvariant(),
            "画面正常", "看起来正常", "一切正常", "运行正常", "没什么问题",
            "只是想确认", "想确认一下", "想确认下", "帮我确认");

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
