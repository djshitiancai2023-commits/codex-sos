using System.Text.Json;

namespace CodexSOS.Core;

public sealed class DoctorJsonParser
{
    public const int SupportedSchemaVersion = 1;
    private readonly PrivacyRedactor _redactor;

    public DoctorJsonParser(PrivacyRedactor redactor) => _redactor = redactor;

    public DoctorResult Parse(string json, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new DoctorResult(DoctorState.Malformed, null, [],
                "官方体检没有返回可用内容，但其他检查已继续完成。", exitCode);
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 32
            });

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Malformed(exitCode);
            }

            var schemaVersion = TryGetInt(root, "schemaVersion") ?? 0;
            var overallStatus = TryGetString(root, "overallStatus")?.ToLowerInvariant();
            var version = Clean(TryGetString(root, "codexVersion"));
            var checks = ParseChecks(root).ToArray();

            if (schemaVersion != SupportedSchemaVersion)
            {
                return new DoctorResult(DoctorState.UnknownSchema, version, checks,
                    "Codex 的官方体检格式已经变化。SOS 没有猜测内容，其他检查已继续完成。",
                    exitCode, schemaVersion);
            }

            var state = overallStatus switch
            {
                "ok" when exitCode == 0 => DoctorState.Ok,
                "warning" => DoctorState.Warning,
                "fail" => DoctorState.Failed,
                _ when checks.Any(c => string.Equals(c.Status, "fail", StringComparison.OrdinalIgnoreCase)) => DoctorState.Failed,
                _ when checks.Any(c => string.Equals(c.Status, "warning", StringComparison.OrdinalIgnoreCase)) => DoctorState.Warning,
                _ when exitCode != 0 => DoctorState.Failed,
                _ => DoctorState.Malformed
            };

            var summary = state switch
            {
                DoctorState.Ok => "Codex 官方体检暂未发现异常；这份检查无法单独解释所有运行中的故障。",
                DoctorState.Warning => "Codex 官方体检发现了可能有关的提醒。",
                DoctorState.Failed => "Codex 官方体检发现了异常；这仍只是线索，不等于已经确定根因。",
                _ => "官方体检内容无法完整识别，但其他检查已继续完成。"
            };

            return new DoctorResult(state, version, checks, summary, exitCode, schemaVersion);
        }
        catch (JsonException)
        {
            return Malformed(exitCode);
        }
    }

    private IEnumerable<DoctorCheck> ParseChecks(JsonElement root)
    {
        if (!root.TryGetProperty("checks", out var checks))
        {
            yield break;
        }

        if (checks.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in checks.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    yield return ParseCheck(property.Value, property.Name);
                }
            }
        }
        else if (checks.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in checks.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    yield return ParseCheck(item, "unknown");
                }
            }
        }
    }

    private DoctorCheck ParseCheck(JsonElement check, string fallbackId)
    {
        var id = Clean(TryGetString(check, "id")) ?? fallbackId;
        var status = Clean(TryGetString(check, "status")) ?? "unknown";
        var summary = Clean(TryGetString(check, "summary")) ?? "没有可公开的说明";
        var remediation = Clean(TryGetString(check, "remediation"));
        return new DoctorCheck(id, status, summary, remediation);
    }

    private string? Clean(string? value) => value is null ? null : _redactor.Redact(value).SanitizedText;

    private static string? TryGetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? TryGetInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;

    private static DoctorResult Malformed(int exitCode) =>
        new(DoctorState.Malformed, null, [],
            "官方体检返回的内容无法识别，但仍然完成了其他检查。", exitCode);
}
