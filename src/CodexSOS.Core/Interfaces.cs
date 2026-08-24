namespace CodexSOS.Core;

public interface IDoctorRunner
{
    Task<DoctorResult> RunAsync(CancellationToken cancellationToken);
}

public interface ISystemCollector
{
    Task<SystemFacts> CollectAsync(CancellationToken cancellationToken);
}

public interface IFaultEventCollector
{
    Task<IReadOnlyList<FaultEvent>> CollectAsync(DateTimeOffset since, CancellationToken cancellationToken);
}

public interface IIssueSearchClient
{
    Task<IssueSearchResult> SearchAsync(
        IReadOnlyList<string> stableTerms,
        CancellationToken cancellationToken);
}

public interface IServiceStatusClient
{
    Task<ServiceStatusResult> GetAsync(CancellationToken cancellationToken);
}
