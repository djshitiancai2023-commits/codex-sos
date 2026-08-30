namespace CodexSOS.Core;

public enum DoctorState
{
    Ok,
    Warning,
    Failed,
    Unsupported,
    TimedOut,
    Malformed,
    UnknownSchema,
    Unavailable
}

public enum IncidentCategory
{
    Login,
    Connection,
    InstallationOrVersion,
    DuplicateInstallation,
    DesktopApplication,
    CodexService,
    TaskRecovery,
    DoctorFailure,
    DoctorCannotExplain,
    Unknown
}

public enum ConfidenceLevel
{
    LikelyRelated,
    PossiblyRelated,
    NotFound,
    CannotDetermine
}

public enum CodexSurface
{
    Desktop,
    Cli,
    Unknown
}

public enum IssueSimilarityTier
{
    High,
    Possible,
    Coincidental
}

public enum IssueSearchState
{
    Completed,
    NoUsableTerms,
    Unavailable
}

public sealed record DoctorCheck(
    string Id,
    string Status,
    string Summary,
    string? Remediation = null);

public sealed record DoctorResult(
    DoctorState State,
    string? CodexVersion,
    IReadOnlyList<DoctorCheck> Checks,
    string PublicSummary,
    int? ExitCode = null,
    int SchemaVersion = 1)
{
    public static DoctorResult Unavailable(string summary) =>
        new(DoctorState.Unavailable, null, [], summary);
}

public sealed record SystemFacts(
    string WindowsVersion,
    string Architecture,
    CodexSurface Surface,
    string? CodexVersion,
    bool CodexIsRunning,
    bool PossibleDuplicateInstall,
    IReadOnlyList<string> SanitizedInstallHints);

public sealed record FaultEvent(
    DateTimeOffset Timestamp,
    string Application,
    string? FaultModule,
    string? ExceptionCode);

public sealed record ServiceStatusResult(
    bool Succeeded,
    bool IsCodexSpecific,
    string Indicator,
    string Description,
    DateTimeOffset CheckedAt)
{
    public static ServiceStatusResult Unavailable() =>
        new(false, false, "unknown", "暂时无法读取官方服务状态。", DateTimeOffset.UtcNow);
}

public sealed record PublicIssue(
    long Number,
    string Title,
    string Body,
    string HtmlUrl,
    string State,
    IReadOnlyList<string> Labels);

public sealed record IssueSearchResult(
    IReadOnlyList<PublicIssue> Issues,
    IssueSearchState State,
    string? FallbackUrl = null)
{
    public bool Succeeded => State == IssueSearchState.Completed;

    public static IssueSearchResult NoUsableTerms() =>
        new([], IssueSearchState.NoUsableTerms);

    public static IssueSearchResult Unavailable(string? fallbackUrl) =>
        new([], IssueSearchState.Unavailable, fallbackUrl);
}

public sealed record MatchedIssue(
    PublicIssue Issue,
    IssueSimilarityTier Tier,
    int Score,
    IReadOnlyList<string> Reasons);

public sealed record PrivacyFinding(string Kind, int Count);

public sealed record PrivacyResult(
    string SanitizedText,
    IReadOnlyList<PrivacyFinding> Findings,
    bool RequiresHumanReview);

public sealed record UserEvidence(
    string Description,
    string OcrText,
    bool ScreenshotProvided,
    DateTimeOffset StartedAt);

public sealed record Diagnosis(
    IncidentCategory Category,
    ConfidenceLevel Confidence,
    string PlainSummary,
    string SafeNextStep,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<IncidentCategory>? CandidateCategories = null,
    bool OfficialFeedbackAppropriate = true)
{
    public IReadOnlyList<IncidentCategory> Candidates =>
        CandidateCategories is { Count: > 0 } ? CandidateCategories : [Category];
}

public sealed record SimilarIssueSummary(
    IReadOnlyList<MatchedIssue> Matches,
    bool SearchSucceeded,
    string PlainSummary,
    string? BrowserFallbackUrl = null,
    IssueSearchState SearchState = IssueSearchState.Completed);

public sealed record DiagnosticReport(
    Guid RunId,
    DateTimeOffset CreatedAt,
    UserEvidence PublicEvidence,
    SystemFacts System,
    DoctorResult Doctor,
    Diagnosis Diagnosis,
    SimilarIssueSummary SimilarIssues,
    ServiceStatusResult ServiceStatus,
    IReadOnlyList<FaultEvent> FaultEvents,
    IReadOnlyList<PrivacyFinding> PrivacyFindings,
    bool ScreenshotSaved,
    string PublicReportMarkdown,
    string PrivacyReviewMarkdown);

public sealed record ClarifyingChoice(string Label, string Meaning, bool Recommended);

public sealed record ClarifyingQuestion(
    string Question,
    string RecommendationReason,
    IReadOnlyList<ClarifyingChoice> Choices);
