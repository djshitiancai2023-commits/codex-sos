namespace CodexSOS.Core;

public sealed class DiagnosticOrchestrator
{
    private readonly IDoctorRunner _doctor;
    private readonly ISystemCollector _system;
    private readonly IFaultEventCollector _faultEvents;
    private readonly IIssueSearchClient _issues;
    private readonly IServiceStatusClient _status;
    private readonly PrivacyRedactor _redactor;
    private readonly StableTermExtractor _terms;
    private readonly DiagnosisEngine _diagnosis;
    private readonly SimilarIssueMatcher _matcher;
    private readonly PublicReportBuilder _reports;

    public DiagnosticOrchestrator(
        IDoctorRunner doctor,
        ISystemCollector system,
        IFaultEventCollector faultEvents,
        IIssueSearchClient issues,
        IServiceStatusClient status,
        PrivacyRedactor redactor,
        StableTermExtractor terms,
        DiagnosisEngine diagnosis,
        SimilarIssueMatcher matcher,
        PublicReportBuilder reports)
    {
        _doctor = doctor;
        _system = system;
        _faultEvents = faultEvents;
        _issues = issues;
        _status = status;
        _redactor = redactor;
        _terms = terms;
        _diagnosis = diagnosis;
        _matcher = matcher;
        _reports = reports;
    }

    public async Task<DiagnosticReport> RunAsync(
        UserEvidence rawEvidence,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("正在保护你提供的信息…");
        var descriptionPrivacy = _redactor.Redact(rawEvidence.Description);
        var ocrPrivacy = _redactor.Redact(rawEvidence.OcrText);
        var publicEvidence = rawEvidence with
        {
            Description = descriptionPrivacy.SanitizedText,
            OcrText = ocrPrivacy.SanitizedText
        };
        // Extract only allow-listed signals before redaction.  The raw text
        // never leaves this local variable; the normalized constants are
        // appended to a separate diagnostic-only copy so a greedy path mask
        // cannot hide a real fixed error phrase from classification/search.
        var safeSignals = _terms.ExtractSafeSignals(rawEvidence.Description, rawEvidence.OcrText);
        var diagnosticEvidence = publicEvidence with
        {
            Description = AppendSafeSignals(publicEvidence.Description, safeSignals),
            OcrText = AppendSafeSignals(publicEvidence.OcrText, safeSignals)
        };

        progress?.Report("正在运行 Codex 官方体检，并查看这台电脑的基本情况…");
        var doctorTask = SafeDoctor(cancellationToken);
        var systemTask = SafeSystem(cancellationToken);
        var eventTask = SafeEvents(rawEvidence.StartedAt.AddMinutes(-10), cancellationToken);
        var statusTask = SafeStatus(cancellationToken);
        await Task.WhenAll(doctorTask, systemTask, eventTask, statusTask).ConfigureAwait(false);

        var doctor = await doctorTask.ConfigureAwait(false);
        var system = await systemTask.ConfigureAwait(false);
        var events = await eventTask.ConfigureAwait(false);
        var status = await statusTask.ConfigureAwait(false);
        var diagnosis = _diagnosis.Diagnose(diagnosticEvidence, system, doctor, events, status);

        progress?.Report("正在寻找相似的 Codex 公开问题…");
        var faultSearchEvidence = BuildFaultSearchEvidence(events);
        var stableTerms = _terms.Extract(
            diagnosticEvidence.Description,
            diagnosticEvidence.OcrText,
            faultSearchEvidence,
            string.Join('\n', doctor.Checks.Where(c => c.Status is "warning" or "fail").Select(c => c.Summary)));
        var search = await SafeIssueSearch(stableTerms, cancellationToken).ConfigureAwait(false);
        var similarity = _matcher.Match(
            $"{diagnosticEvidence.Description}\n{diagnosticEvidence.OcrText}\n{faultSearchEvidence}",
            diagnosis.Candidates,
            system.Surface,
            search.Issues,
            search.State,
            search.FallbackUrl);

        progress?.Report("正在做最后一次隐私检查…");
        var runId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var built = _reports.Build(runId, now, publicEvidence, system, doctor, diagnosis,
            similarity, status, events);
        var allFindings = descriptionPrivacy.Findings
            .Concat(ocrPrivacy.Findings)
            .Concat(built.Findings)
            .GroupBy(finding => finding.Kind, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PrivacyFinding(group.Key, group.Sum(finding => finding.Count)))
            .OrderBy(finding => finding.Kind, StringComparer.Ordinal)
            .ToArray();
        var privacyReview = PublicReportBuilder.BuildPrivacyReview(allFindings);

        progress?.Report("检查完成");
        return new DiagnosticReport(runId, now, publicEvidence, system, doctor, diagnosis,
            similarity, status, events, allFindings, ScreenshotSaved: false,
            built.PublicReport, privacyReview);
    }

    private static string AppendSafeSignals(string? sanitizedText, IReadOnlyList<string> safeSignals) =>
        safeSignals.Count == 0
            ? sanitizedText ?? string.Empty
            : string.Join('\n', new[] { sanitizedText ?? string.Empty }.Concat(safeSignals));

    private static string BuildFaultSearchEvidence(IReadOnlyList<FaultEvent> events) =>
        string.Join('\n', events
            .SelectMany(item => new[] { FileNameOnly(item.FaultModule), item.ExceptionCode })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!));

    private static string? FileNameOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var separator = Math.Max(trimmed.LastIndexOf('\\'), trimmed.LastIndexOf('/'));
        return separator >= 0 && separator + 1 < trimmed.Length
            ? trimmed[(separator + 1)..]
            : trimmed;
    }

    private async Task<DoctorResult> SafeDoctor(CancellationToken cancellationToken)
    {
        try { return await _doctor.RunAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return DoctorResult.Unavailable("Codex 官方体检这次无法运行，但其他检查已继续完成。"); }
    }

    private async Task<SystemFacts> SafeSystem(CancellationToken cancellationToken)
    {
        try { return await _system.CollectAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new("暂时无法确定", "暂时无法确定", CodexSurface.Unknown, null, false, false, []); }
    }

    private async Task<IReadOnlyList<FaultEvent>> SafeEvents(DateTimeOffset since, CancellationToken cancellationToken)
    {
        try { return await _faultEvents.CollectAsync(since, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return []; }
    }

    private async Task<ServiceStatusResult> SafeStatus(CancellationToken cancellationToken)
    {
        try { return await _status.GetAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return ServiceStatusResult.Unavailable(); }
    }

    private async Task<IssueSearchResult> SafeIssueSearch(
        IReadOnlyList<string> terms,
        CancellationToken cancellationToken)
    {
        if (terms.Count == 0)
        {
            return IssueSearchResult.NoUsableTerms();
        }

        try { return await _issues.SearchAsync(terms, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            var query = Uri.EscapeDataString(string.Join(' ', terms.Take(4)));
            return IssueSearchResult.Unavailable(
                $"https://github.com/openai/codex/issues?q=is%3Aissue+{query}");
        }
    }
}
