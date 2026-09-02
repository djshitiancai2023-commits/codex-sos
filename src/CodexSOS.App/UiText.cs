using System.Text;
using CodexSOS.Core;

namespace CodexSOS.App;

public enum UiLanguage
{
    SimplifiedChinese,
    TraditionalChinese,
    English
}

public static class UiText
{
    private static readonly IReadOnlyDictionary<string, string[]> Text = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["Tagline"] = ["一卡住，按一下救生圈", "一卡住，按一下救生圈", "When Codex gets stuck, reach for the lifebuoy"],
        ["UnofficialNotice"] = ["非官方社区工具，与 OpenAI 无隶属关系", "非官方社群工具，與 OpenAI 無隸屬關係", "Unofficial community tool, not affiliated with OpenAI"],
        ["StartTitle"] = ["Codex 出问题了", "Codex 出問題了", "Codex has a problem"],
        ["StartSubtitle"] = ["截一下错误，或随便说一句发生了什么。其他信息交给我们。", "截一下錯誤，或簡單說一句發生了什麼。其他資訊交給我們。", "Paste an error screenshot or briefly say what happened. We will handle the rest."],
        ["WithImageTitle"] = ["A  有画面", "A  有畫面", "A  I have a screenshot"],
        ["WithImageHint"] = ["选最顺手的一种就行", "選最方便的一種就好", "Choose whichever is easiest"],
        ["CaptureButton"] = ["截取正在打开的 Codex", "截取目前開啟的 Codex", "Capture the open Codex window"],
        ["PasteButton"] = ["粘贴截图", "貼上截圖", "Paste screenshot"],
        ["ChooseButton"] = ["选择已有截图", "選擇已有截圖", "Choose a screenshot"],
        ["ScreenshotEmpty"] = ["还没有截图", "還沒有截圖", "No screenshot yet"],
        ["NoImageTitle"] = ["B  没画面", "B  沒有畫面", "B  No screenshot"],
        ["NoImageHint"] = ["一句话就够，不用写报错单", "一句話就夠，不用填寫錯誤表單", "One sentence is enough—no error form"],
        ["DescriptionHint"] = ["例如：Codex 做了很久突然断开，再也续不上。", "例如：Codex 執行很久後突然斷線，再也無法繼續。", "Example: Codex disconnected after running for a while and cannot resume."],
        ["NoSkillsNeeded"] = ["不用找日志，不用懂版本，也不用会 GitHub。", "不用找記錄、不用懂版本，也不用會 GitHub。", "No logs, version knowledge, or GitHub skills needed."],
        ["StartButton"] = ["帮我看看", "幫我看看", "Check it for me"],
        ["AutoCheckHint"] = ["接下来会自动完成官方体检、查找相似问题和隐私检查。", "接下來會自動完成官方檢查、尋找相似問題和隱私檢查。", "Next, Codex SOS will run the official check, find similar issues, and review privacy."],
        ["ProgressTitle"] = ["正在帮你检查", "正在幫你檢查", "Checking for you"],
        ["ProgressPrivacy"] = ["不用找日志，也不会上传你的截图或聊天。", "不用找記錄，也不會上傳你的截圖或聊天內容。", "No logs needed. Your screenshot and chats are not uploaded."],
        ["ResultSubtitle"] = ["先看最重要的四件事", "先看最重要的四件事", "The four things that matter most"],
        ["ResultWhat"] = ["1  这大概是什么", "1  這大概是什麼", "1  What this may be"],
        ["ResultOthers"] = ["2  是不是只有我", "2  是不是只有我", "2  Is anyone else affected?"],
        ["BrowserFallback"] = ["在浏览器里搜索相似问题", "在瀏覽器中搜尋相似問題", "Search similar issues in a browser"],
        ["ResultNext"] = ["3  现在最安全怎么做", "3  現在最安全怎麼做", "3  Safest next step"],
        ["ResultMore"] = ["4  还不行怎么办", "4  還是不行怎麼辦", "4  If that does not work"],
        ["ResultMoreAnswer"] = ["已经准备好完整材料。先看一眼，再保存，或交给 Codex 官方。", "已準備好完整資料。先看一眼，再儲存，或交給 Codex 官方。", "A complete report is ready. Review it, save it, or share it with the Codex maintainers."],
        ["ReviewButton"] = ["查看并保存完整材料", "查看並儲存完整資料", "Review and save full report"],
        ["CopyResultButton"] = ["复制这四条结果", "複製這四項結果", "Copy these four results"],
        ["ResetButton"] = ["再检查一次", "再檢查一次", "Check again"],
        ["ReviewTitle"] = ["保存前，快速看一眼", "儲存前，快速看一眼", "Quick review before saving"],
        ["ReviewSubtitle"] = ["我们准备公开这些信息：", "我們準備公開這些資訊：", "This information is ready to share:"],
        ["ReviewIncludes"] = ["会保存：你的一句话、识别出的错误文字、版本和体检结论、相似公开问题链接。", "會儲存：你的一句描述、辨識出的錯誤文字、版本和檢查結論、相似公開問題連結。", "Included: your description, recognized error text, versions, check results, and links to similar public issues."],
        ["ReviewExcludes"] = ["不会保存：原截图、完整聊天、提示词、项目代码、账号文件、密钥或 cookie。", "不會儲存：原始截圖、完整聊天、提示詞、專案程式碼、帳號檔案、金鑰或 Cookie。", "Not included: the original screenshot, full chats, prompts, project code, account files, keys, or cookies."],
        ["ReviewExpander"] = ["查看完整公开材料", "查看完整公開資料", "View the complete public report"],
        ["ReviewWarning"] = ["自动遮盖不能保证 100% 安全。Codex SOS 不会自动发布这些材料。", "自動遮蔽無法保證 100% 安全。Codex SOS 不會自動發佈這些資料。", "Automatic redaction cannot guarantee 100% safety. Codex SOS never publishes this report automatically."],
        ["OfficialFeedbackHint"] = ["先看上面的相似问题；确实相同只需点赞。否则可复制材料，打开公开反馈页，或粘贴到原来的 OpenAI 客服邮件。客服如需反馈编号，原任务还能打开时可发送 /feedback 获取；打不开就跳过，不要故意重现故障。SOS 不会代你发送。", "先查看上面的相似問題；確實相同只需按讚。否則可複製資料、開啟公開回報頁，或貼到原本的 OpenAI 客服郵件。客服如需回報編號，原任務仍能開啟時可傳送 /feedback 取得；無法開啟就跳過，不要刻意重現故障。SOS 不會代你傳送。", "Check similar issues above first; if one is the same, only add a thumbs-up. Otherwise, copy the report to the public bug form or your existing OpenAI Support email. If Support asks for a Feedback ID and the relevant thread still opens, run /feedback there; otherwise skip it and do not reproduce the failure. SOS never sends anything for you."],
        ["SaveButton"] = ["保存到电脑", "儲存到電腦", "Save to this computer"],
        ["OfficialFeedbackButton"] = ["复制材料并打开官方反馈页", "複製資料並開啟官方回報頁", "Copy report and open official bug form"],
        ["BackButton"] = ["返回结果", "返回結果", "Back to results"],
        ["Footer"] = ["默认只在本机处理 · 不用 API Key · 正常运行不调用大模型 · 不自动发布", "預設只在本機處理 · 不用 API Key · 正常執行不呼叫大型模型 · 不自動發佈", "Local-first · No API key · No model calls in normal use · Never auto-publishes"]
    };

    public static string Get(UiLanguage language, string key) =>
        Text.TryGetValue(key, out var values) ? values[(int)language] : key;

    public static IReadOnlyCollection<string> Keys => Text.Keys.ToArray();

    public static string Get(UiLanguage language, string simplified, string traditional, string english) =>
        language switch
        {
            UiLanguage.TraditionalChinese => traditional,
            UiLanguage.English => english,
            _ => simplified
        };

    public static string Progress(UiLanguage language, string source) => source switch
    {
        "正在保护你提供的信息…" => Get(language, source, "正在保護你提供的資訊…", "Protecting the information you provided…"),
        "正在运行 Codex 官方体检，并查看这台电脑的基本情况…" => Get(language, source, "正在執行 Codex 官方檢查，並查看這台電腦的基本狀況…", "Running the official Codex check and reviewing this computer…"),
        "正在寻找相似的 Codex 公开问题…" => Get(language, source, "正在尋找相似的 Codex 公開問題…", "Looking for similar public Codex issues…"),
        "正在做最后一次隐私检查…" => Get(language, source, "正在做最後一次隱私檢查…", "Running the final privacy check…"),
        "检查完成" => Get(language, source, "檢查完成", "Check complete"),
        _ => source
    };

    public static string CaptureMessage(UiLanguage language, string source)
    {
        if (language == UiLanguage.SimplifiedChinese) return source;
        if (source.StartsWith("已截取", StringComparison.Ordinal))
            return Get(language, source, "已截取 Codex 視窗。圖片只在本機處理，不會放進公開資料。", "Codex window captured. The image stays on this computer and is not included in the public report.");
        if (source.StartsWith("没有找到", StringComparison.Ordinal))
            return Get(language, source, "沒有找到正在顯示的 Codex 視窗。你也可以直接貼上已有截圖。", "No visible Codex window was found. You can paste an existing screenshot instead.");
        return Get(language, source, "暫時無法讀取 Codex 視窗畫面。你也可以直接貼上已有截圖。", "The Codex window could not be captured. You can paste an existing screenshot instead.");
    }

    public static string DiagnosisSummary(UiLanguage language, Diagnosis diagnosis)
    {
        if (language == UiLanguage.SimplifiedChinese) return diagnosis.PlainSummary;
        if (diagnosis.Category == IncidentCategory.DoctorCannotExplain)
            return Get(language, diagnosis.PlainSummary, "這份檢查無法解釋目前的故障。", "This check cannot explain the current problem.");
        if (diagnosis.Category == IncidentCategory.Unknown)
        {
            if (diagnosis.PlainSummary.Contains("看不出这是 Codex 自己的问题", StringComparison.Ordinal))
                return Get(language, diagnosis.PlainSummary,
                    "目前看不出這是 Codex 自己的問題，更像是另一個程式或開機項目的視窗。",
                    "This does not currently look like a Codex problem. It looks more like another program or startup item.");
            if (diagnosis.PlainSummary.Contains("没有从这张截图里读到", StringComparison.Ordinal))
                return Get(language, diagnosis.PlainSummary, "沒有從這張截圖讀到足夠文字，暫時不能判斷，也不能當作正常。", "Not enough text was read from this screenshot to judge the problem, and it should not be treated as normal.");
            if (diagnosis.PlainSummary.Contains("像是有报错", StringComparison.Ordinal))
                return Get(language, diagnosis.PlainSummary, "截圖中像是有錯誤，但暫時還無法辨識是哪一類問題。", "The screenshot appears to contain an error, but its problem type is not recognized yet.");
            if (diagnosis.PlainSummary.Contains("暂未看到明确报错", StringComparison.Ordinal))
                return Get(language, diagnosis.PlainSummary, "截圖中暫未看到明確錯誤；僅憑這張圖不能確認 Codex 一定正常。", "No clear error is visible in the screenshot, but that alone cannot confirm that Codex is working normally.");
            if (diagnosis.PlainSummary.Contains("没有看到明确的故障线索", StringComparison.Ordinal))
                return Get(language, diagnosis.PlainSummary, "目前沒有看到明確的故障線索；這只代表這次快照，不能保證 Codex 一直正常。", "No clear failure clue is visible right now. This is only a snapshot and cannot guarantee Codex will remain healthy.");
            return Get(language, diagnosis.PlainSummary, "目前還無法判斷是哪一類問題。", "There is not enough evidence to identify the type of problem yet.");
        }

        var category = Category(language, diagnosis.Category);
        return diagnosis.Confidence switch
        {
            ConfidenceLevel.LikelyRelated => Get(language, diagnosis.PlainSummary, $"很可能與{category}有關。", $"Very likely related to {category}."),
            ConfidenceLevel.PossiblyRelated => Get(language, diagnosis.PlainSummary, $"可能與{category}有關。", $"Possibly related to {category}."),
            _ => Get(language, diagnosis.PlainSummary, $"暫未發現明確的{category}線索。", $"No clear evidence of {category} was found.")
        };
    }

    public static string SafeNextStep(UiLanguage language, Diagnosis diagnosis)
    {
        if (language == UiLanguage.SimplifiedChinese) return diagnosis.SafeNextStep;
        if (!diagnosis.OfficialFeedbackAppropriate)
            return Get(language, diagnosis.SafeNextStep,
                "先不要刪除 Codex、該程式或本機資料；先確認這個視窗屬於哪個程式。如果 Codex 自己也退出、卡住或報錯，再把當時的截圖交給 SOS。",
                "Do not delete Codex, the other program, or local data. First identify which program owns the window. If Codex itself also exits, freezes, or shows an error, give SOS a screenshot from that moment.");
        if (diagnosis.SafeNextStep.StartsWith("现在不需要做任何修复操作", StringComparison.Ordinal))
            return Get(language, diagnosis.SafeNextStep,
                "現在不需要做任何修復操作；之後如果出現具體錯誤，再截一張圖給 SOS 即可。",
                "No repair action is needed now. If a specific error appears later, give SOS a screenshot.");
        return SafeNextStepForCategory(language, diagnosis.Category);
    }

    private static string SafeNextStepForCategory(UiLanguage language, IncidentCategory category) => category switch
    {
        IncidentCategory.TaskRecovery => Get(language,
            "先完全关闭并重新打开 Codex；保留原任务和本地数据，不要删除会话。",
            "先完全關閉並重新開啟 Codex；保留原任務和本機資料，不要刪除工作階段。",
            "Fully close and reopen Codex. Keep the original task and local data; do not delete the session."),
        IncidentCategory.Connection => Get(language,
            "先重新打开 Codex，并查看官方服务状态；不要修改系统网络设置。",
            "先重新開啟 Codex，並查看官方服務狀態；不要修改系統網路設定。",
            "Reopen Codex and check the official service status. Do not change system network settings."),
        IncidentCategory.Login => Get(language,
            "先重新打开 Codex，确认登录页面是否恢复；不要删除账号或重置本地数据。",
            "先重新開啟 Codex，確認登入頁面是否恢復；不要刪除帳號或重設本機資料。",
            "Reopen Codex and see whether sign-in works again. Do not remove the account or reset local data."),
        IncidentCategory.InstallationOrVersion or IncidentCategory.DuplicateInstallation => Get(language,
            "先查看是否有 Codex 新版本；不要自动卸载或覆盖现有版本。",
            "先查看是否有 Codex 新版本；不要自動解除安裝或覆蓋現有版本。",
            "Check whether a newer Codex version is available. Do not automatically uninstall or overwrite the current version."),
        IncidentCategory.DesktopApplication => Get(language,
            "先完全关闭并重新打开 Codex；不要删除缓存、任务或本地数据。",
            "先完全關閉並重新開啟 Codex；不要刪除快取、任務或本機資料。",
            "Fully close and reopen Codex. Do not delete cache, tasks, or local data."),
        IncidentCategory.DoctorFailure => Get(language,
            "先保留当前任务和本地数据；可以查看是否有 Codex 新版本，或稍后重新检查。",
            "先保留目前任務和本機資料；可以查看是否有 Codex 新版本，或稍後重新檢查。",
            "Keep the current task and local data. Check for a newer Codex version or run the check again later."),
        _ => Get(language,
            "先完全关闭并重新打开 Codex；暂时不要删除任务或本地数据。",
            "先完全關閉並重新開啟 Codex；暫時不要刪除任務或本機資料。",
            "Fully close and reopen Codex. For now, do not delete tasks or local data.")
    };

    public static string SimilarSummary(UiLanguage language, SimilarIssueSummary similar)
    {
        if (language == UiLanguage.SimplifiedChinese) return similar.PlainSummary;
        if (similar.PlainSummary.StartsWith("这更像其他程序的问题", StringComparison.Ordinal))
            return Get(language, similar.PlainSummary,
                "這更像其他程式的問題，因此沒有搜尋 Codex 官方問題。",
                "This looks more like another program's problem, so no Codex issues were searched.");
        if (similar.SearchState == IssueSearchState.NoUsableTerms)
            return Get(language, similar.PlainSummary, "暫時沒有足夠穩定的錯誤文字，所以不會亂猜。", "There is not enough stable error text to make a reliable match.");
        if (similar.SearchState == IssueSearchState.Unavailable)
            return Get(language, similar.PlainSummary, "目前無法連上公開問題清單。檢查仍已完成，你可以稍後再試或使用搜尋入口。", "The public issue list is unavailable right now. The check still completed; try again later or use the browser search link.");
        var high = similar.Matches.Count(match => match.Tier == IssueSimilarityTier.High);
        var openHigh = similar.Matches.Count(match => match.Tier == IssueSimilarityTier.High &&
            string.Equals(match.Issue.State, "open", StringComparison.OrdinalIgnoreCase));
        if (high > 0)
            return Get(language, similar.PlainSummary, $"找到 {high} 個高度相似的公開問題，其中 {openHigh} 個仍未關閉。", $"Found {high} highly similar public issue(s); {openHigh} remain open.");
        if (similar.Matches.Count > 0)
            return Get(language, similar.PlainSummary, $"找到 {similar.Matches.Count} 個可能相關的公開問題，但證據還不足以說是同一個問題。", $"Found {similar.Matches.Count} possibly related public issue(s), but there is not enough evidence to call them the same problem.");
        return Get(language, similar.PlainSummary, "暫未找到足夠相似的問題。", "No sufficiently similar public issue was found.");
    }

    public static string DoctorSummary(UiLanguage language, DoctorResult doctor)
    {
        if (language == UiLanguage.SimplifiedChinese) return doctor.PublicSummary;
        return doctor.State switch
        {
            DoctorState.Ok => Get(language, doctor.PublicSummary, "Codex 官方檢查暫未發現異常；這份檢查無法單獨解釋所有執行中的故障。", "The official Codex check found no current warning, but it cannot explain every live problem."),
            DoctorState.Warning => Get(language, doctor.PublicSummary, "Codex 官方檢查發現可能有關的提醒。", "The official Codex check found a warning that may be related."),
            DoctorState.Failed => Get(language, doctor.PublicSummary, "Codex 官方檢查發現異常；這只是線索，不代表已確定根因。", "The official Codex check found a failure. This is evidence, not a confirmed root cause."),
            DoctorState.Unsupported => Get(language, doctor.PublicSummary, "此版本暫不支援官方檢查，但其他檢查已完成。", "This Codex version does not support the official check; the other checks still completed."),
            DoctorState.TimedOut => Get(language, doctor.PublicSummary, "官方檢查等候過久，SOS 已繼續完成其他檢查。", "The official check took too long, so SOS continued with the other checks."),
            DoctorState.Malformed or DoctorState.UnknownSchema => Get(language, doctor.PublicSummary, "官方檢查內容無法可靠辨識；SOS 沒有猜測，其他檢查已完成。", "The official check output could not be read reliably. SOS did not guess; the other checks still completed."),
            _ => Get(language, doctor.PublicSummary, "官方檢查這次無法執行，但其他檢查已完成。", "The official check could not run this time; the other checks still completed.")
        };
    }

    public static string Tier(UiLanguage language, IssueSimilarityTier tier) => tier switch
    {
        IssueSimilarityTier.High => Get(language, "高度相似", "高度相似", "Highly similar"),
        IssueSimilarityTier.Possible => Get(language, "可能相关", "可能相關", "Possibly related"),
        _ => Get(language, "仅供参考", "僅供參考", "For reference only")
    };

    public static string MatchReason(UiLanguage language, string reason)
    {
        if (language == UiLanguage.SimplifiedChinese) return reason;
        const string exactPrefix = "相同的固定错误短语：";
        const string overlapPrefix = "多个症状词一致：";
        if (reason.StartsWith(exactPrefix, StringComparison.Ordinal))
            return Get(language, reason, "相同的固定錯誤短語：", "Same distinctive error phrase: ") + reason[exactPrefix.Length..];
        if (reason.StartsWith(overlapPrefix, StringComparison.Ordinal))
            return Get(language, reason, "多個症狀詞一致：", "Several symptom terms match: ") + reason[overlapPrefix.Length..];
        return reason switch
        {
            "故障类型一致" => Get(language, reason, "故障類型一致", "Same problem type"),
            "使用方式一致" => Get(language, reason, "使用方式一致", "Same Codex interface"),
            "同为 Windows" => Get(language, reason, "同為 Windows", "Both on Windows"),
            _ => reason
        };
    }

    public static string Category(UiLanguage language, IncidentCategory category) => category switch
    {
        IncidentCategory.Login => Get(language, "Codex 登录问题", "Codex 登入問題", "a Codex sign-in problem"),
        IncidentCategory.Connection => Get(language, "Codex 连接中断", "Codex 連線中斷", "a Codex connection interruption"),
        IncidentCategory.InstallationOrVersion => Get(language, "Codex 安装或版本问题", "Codex 安裝或版本問題", "a Codex installation or version problem"),
        IncidentCategory.DuplicateInstallation => Get(language, "重复安装", "重複安裝", "multiple Codex installations"),
        IncidentCategory.DesktopApplication => Get(language, "Codex 桌面应用异常", "Codex 桌面應用程式異常", "a Codex desktop app problem"),
        IncidentCategory.CodexService => Get(language, "Codex 后台服务异常", "Codex 後台服務異常", "a Codex service problem"),
        IncidentCategory.TaskRecovery => Get(language, "Codex 任务恢复异常", "Codex 任務恢復異常", "a Codex task recovery problem"),
        IncidentCategory.DoctorFailure => Get(language, "Codex 官方体检自身异常", "Codex 官方檢查本身異常", "a problem with the official Codex check"),
        _ => Get(language, "暂时无法判断的问题", "暫時無法判斷的問題", "an unidentified problem")
    };

    public static string BuildPublicReport(DiagnosticReport report, UiLanguage language)
    {
        if (language == UiLanguage.SimplifiedChinese) return report.PublicReportMarkdown;
        var b = new StringBuilder();
        b.AppendLine(Get(language, "# Codex SOS 检查材料", "# Codex SOS 檢查資料", "# Codex SOS diagnostic report"));
        b.AppendLine();
        b.AppendLine(Get(language,
            "> 由非官方社区工具 Codex SOS 在本机整理。请在公开前查看隐私复核说明。",
            "> 由非官方社群工具 Codex SOS 在本機整理。公開前請查看隱私複核說明。",
            "> Prepared locally by the unofficial community tool Codex SOS. Review the privacy notes before sharing."));
        AppendHeading(b, language, "发生了什么", "發生了什麼", "What happened");
        b.AppendLine(string.IsNullOrWhiteSpace(report.PublicEvidence.Description)
            ? Get(language, "用户没有补充文字描述。", "使用者沒有補充文字描述。", "No written description was provided.")
            : report.PublicEvidence.Description.Trim());
        if (!string.IsNullOrWhiteSpace(report.PublicEvidence.OcrText))
        {
            b.AppendLine();
            b.AppendLine(Get(language, "从截图中识别出的错误文字：", "從截圖中辨識出的錯誤文字：", "Error text recognized from the screenshot:"));
            b.AppendLine();
            b.AppendLine($"> {report.PublicEvidence.OcrText.Trim().Replace("\n", "\n> ", StringComparison.Ordinal)}");
        }
        AppendHeading(b, language, "检查结论", "檢查結論", "Check result");
        b.AppendLine(DiagnosisSummary(language, report.Diagnosis));
        b.AppendLine();
        b.AppendLine(Get(language, $"建议：{SafeNextStep(language, report.Diagnosis)}", $"建議：{SafeNextStep(language, report.Diagnosis)}", $"Suggested next step: {SafeNextStep(language, report.Diagnosis)}"));
        b.AppendLine();
        b.AppendLine(Get(language, "依据：", "依據：", "Evidence:"));
        foreach (var item in report.Diagnosis.Evidence)
            b.AppendLine($"- {DiagnosticDetail(language, item)}");
        AppendHeading(b, language, "环境", "環境", "Environment");
        b.AppendLine($"- Windows: {report.System.WindowsVersion}");
        b.AppendLine($"- {Get(language, "电脑类型", "電腦類型", "Architecture")}: {report.System.Architecture}");
        b.AppendLine($"- {Get(language, "使用方式", "使用方式", "Codex interface")}: {Surface(language, report.System.Surface)}");
        b.AppendLine($"- {Get(language, "Codex 版本", "Codex 版本", "Codex version")}: {report.Doctor.CodexVersion ?? report.System.CodexVersion ?? Get(language, "暂时无法确定", "暫時無法確定", "Unknown")}");
        b.AppendLine($"- {Get(language, "Codex 是否正在运行", "Codex 是否正在執行", "Codex running")}: {(report.System.CodexIsRunning ? Get(language, "是", "是", "Yes") : Get(language, "没有发现", "未發現", "Not found"))}");
        b.AppendLine($"- {Get(language, "可能存在不同安装", "可能存在不同安裝", "Possible multiple installations")}: {(report.System.PossibleDuplicateInstall ? Get(language, "可能有关", "可能有關", "Possibly related") : Get(language, "暂未发现", "暫未發現", "Not found"))}");
        AppendHeading(b, language, "Codex 官方体检", "Codex 官方檢查", "Official Codex check");
        b.AppendLine(DoctorSummary(language, report.Doctor));
        foreach (var check in report.Doctor.Checks.Where(c => c.Status is "warning" or "fail").Take(12))
            b.AppendLine($"- {check.Id}: {check.Summary}");
        AppendHeading(b, language, "官方服务状态", "官方服務狀態", "Official service status");
        b.AppendLine(ServiceStatus(language, report.ServiceStatus));
        AppendHeading(b, language, "相似的公开问题", "相似的公開問題", "Similar public issues");
        b.AppendLine(SimilarSummary(language, report.SimilarIssues));
        foreach (var match in report.SimilarIssues.Matches.Take(5))
            b.AppendLine($"- [#{match.Issue.Number} {match.Issue.Title}]({match.Issue.HtmlUrl}) — {Tier(language, match.Tier)}; {Get(language, "依据", "依據", "Reason")}: {string.Join(", ", match.Reasons.Select(reason => MatchReason(language, reason)))}");
        if (report.FaultEvents.Count > 0)
        {
            AppendHeading(b, language, "故障时间附近的 Windows 记录", "故障時間附近的 Windows 記錄", "Windows records near the time of the problem");
            foreach (var faultEvent in report.FaultEvents.Take(8))
                b.AppendLine($"- {faultEvent.Timestamp:O}: {faultEvent.Application} / {faultEvent.FaultModule ?? Get(language, "未提供模块", "未提供模組", "No module provided")} / {faultEvent.ExceptionCode ?? Get(language, "未提供代码", "未提供代碼", "No code provided")}");
        }
        AppendHeading(b, language, "边界", "界限", "Limits");
        b.AppendLine(Get(language, "- 这些线索不能证明已经找到根因。", "- 這些線索不能證明已找到根因。", "- These clues do not prove that the root cause has been found."));
        b.AppendLine(Get(language, "- 原截图没有放进这份公开材料。", "- 原始截圖沒有放進這份公開資料。", "- The original screenshot is not included."));
        b.AppendLine(Get(language, "- Codex SOS 没有读取完整对话、提示词、项目代码或 auth.json。", "- Codex SOS 沒有讀取完整對話、提示詞、專案程式碼或 auth.json。", "- Codex SOS did not read full chats, prompts, project code, or auth.json."));
        b.AppendLine(Get(language, "- Codex 官方体检由 Codex 自己运行；SOS 只处理其返回结果。", "- Codex 官方檢查由 Codex 自己執行；SOS 只處理其回傳結果。", "- Codex itself runs the official check; SOS only processes the returned result."));
        b.AppendLine(Get(language, "- 没找到相似问题，不代表从来没人遇到过。", "- 沒找到相似問題，不代表從來沒有人遇到過。", "- No match does not mean nobody has ever had this problem."));
        b.AppendLine();
        b.AppendLine($"{Get(language, "材料编号", "資料編號", "Report ID")}: {report.RunId.ToString("N")[..8]} · {Get(language, "生成时间", "產生時間", "Created")}: {report.CreatedAt:O}");
        return b.ToString();
    }

    public static string BuildPrivacyReview(DiagnosticReport report, UiLanguage language)
    {
        if (language == UiLanguage.SimplifiedChinese) return report.PrivacyReviewMarkdown;
        var count = report.PrivacyFindings.Sum(finding => finding.Count);
        var b = new StringBuilder();
        b.AppendLine(Get(language, "# 隐私复核说明", "# 隱私複核說明", "# Privacy review"));
        AppendHeading(b, language, "我们准备公开这些信息", "我們準備公開這些資訊", "Information ready to share");
        b.AppendLine(Get(language, "- 你主动写下的一句话（已检查隐私）", "- 你主動寫下的一句話（已檢查隱私）", "- Your short description (privacy-checked)"));
        b.AppendLine(Get(language, "- 截图里识别出的错误文字（不包含原截图）", "- 截圖中辨識出的錯誤文字（不包含原始截圖）", "- Error text recognized from the screenshot (not the original image)"));
        b.AppendLine(Get(language, "- 版本、官方体检结论和相似公开问题链接", "- 版本、官方檢查結論和相似公開問題連結", "- Versions, official check results, and similar public issue links"));
        AppendHeading(b, language, "不会包含", "不會包含", "Not included");
        b.AppendLine(Get(language, "- 原截图、完整聊天、提示词、项目代码、账号文件、密钥或 cookie", "- 原始截圖、完整聊天、提示詞、專案程式碼、帳號檔案、金鑰或 Cookie", "- Original screenshot, full chats, prompts, project code, account files, keys, or cookies"));
        b.AppendLine();
        b.AppendLine(count == 0
            ? Get(language, "自动检查暂未发现需要遮住的内容。", "自動檢查暫未發現需要遮蔽的內容。", "The automatic check found nothing that needed redaction.")
            : Get(language, $"自动检查已经遮住 {count} 处可能的私人信息。", $"自動檢查已遮蔽 {count} 處可能的私人資訊。", $"The automatic check redacted {count} possible piece(s) of private information."));
        b.AppendLine();
        b.AppendLine(Get(language,
            "自动遮盖不能保证 100% 安全。保存或复制前请快速检查。Codex SOS 不会自动发布。",
            "自動遮蔽無法保證 100% 安全。儲存或複製前請快速檢查。Codex SOS 不會自動發佈。",
            "Automatic redaction cannot guarantee 100% safety. Review before saving or copying. Codex SOS never publishes automatically."));
        return b.ToString();
    }

    private static void AppendHeading(StringBuilder builder, UiLanguage language, string simplified, string traditional, string english)
    {
        builder.AppendLine();
        builder.AppendLine($"## {Get(language, simplified, traditional, english)}");
        builder.AppendLine();
    }

    private static string Surface(UiLanguage language, CodexSurface surface) => surface switch
    {
        CodexSurface.Desktop => Get(language, "Codex 桌面版", "Codex 桌面版", "Codex desktop app"),
        CodexSurface.Cli => Get(language, "Codex 命令行版", "Codex 命令列版", "Codex CLI"),
        _ => Get(language, "只能确定为 Codex", "只能確定為 Codex", "Codex (interface unknown)")
    };

    private static string ServiceStatus(UiLanguage language, ServiceStatusResult status)
    {
        if (!status.Succeeded)
            return Get(language, "暂时无法读取官方服务状态；这不会阻止本机检查。", "暫時無法讀取官方服務狀態；這不會阻止本機檢查。", "The official service status is unavailable; this does not block the local checks.");
        var plain = status.Indicator switch
        {
            "none" or "operational" => Get(language, "运行正常", "運作正常", "Operational"),
            "minor" or "degraded_performance" => Get(language, "部分性能下降", "部分效能下降", "Degraded performance"),
            "major" or "partial_outage" => Get(language, "部分服务中断", "部分服務中斷", "Partial outage"),
            "critical" or "major_outage" => Get(language, "大范围服务中断", "大範圍服務中斷", "Major outage"),
            "maintenance" or "under_maintenance" => Get(language, "维护中", "維護中", "Under maintenance"),
            _ => Get(language, "状态暂时无法解释", "狀態暫時無法解釋", "Status could not be interpreted")
        };
        return plain + Get(language, "（这是整体状态，不能排除个人或单个任务的故障。）", "（這是整體狀態，不能排除個人或單一任務的故障。）", " (This is an overall status and cannot rule out a problem affecting one person or task.)");
    }

    private static string DiagnosticDetail(UiLanguage language, string source)
    {
        if (language == UiLanguage.SimplifiedChinese) return source;
        return source switch
        {
            "截图或描述里出现了连接中断特征" => Get(language, source, "截圖或描述中出現連線中斷特徵", "The screenshot or description contains signs of a connection interruption"),
            "截图或描述里出现了任务恢复特征" => Get(language, source, "截圖或描述中出現任務恢復特徵", "The screenshot or description contains signs of a task recovery problem"),
            "截图或描述里出现了登录特征" => Get(language, source, "截圖或描述中出現登入特徵", "The screenshot or description contains signs of a sign-in problem"),
            "截图或描述里出现了桌面应用卡住或退出特征" => Get(language, source, "截圖或描述中出現桌面應用程式卡住或退出的特徵", "The screenshot or description contains signs that the desktop app froze or exited"),
            "描述明确提到了浏览器、网页、开机项目或另一款程序" => Get(language, source, "描述明確提到瀏覽器、網頁、開機項目或另一個程式", "The description explicitly mentions a browser, web page, startup item, or another program"),
            "截图或描述里出现了安装或版本特征" => Get(language, source, "截圖或描述中出現安裝或版本特徵", "The screenshot or description contains signs of an installation or version problem"),
            "官方体检这次没有完成" => Get(language, source, "官方檢查這次沒有完成", "The official check did not complete"),
            _ => source
        };
    }
}
