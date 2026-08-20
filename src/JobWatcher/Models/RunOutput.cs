namespace JobWatcher.Models;

public sealed record RunOutput
{
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required bool HasFailures { get; init; }
    public required int TotalNewJobs { get; init; }
    public IReadOnlyList<SourceOutput> Sources { get; init; } = [];
}

public sealed record SourceOutput
{
    public required string Source { get; init; }
    public required string Status { get; init; }

    /// <summary>True when this source's failure was not counted towards the process exit code.</summary>
    public bool Optional { get; init; }

    public bool IsInitialRun { get; init; }
    public int PreviousCount { get; init; }
    public int CurrentCount { get; init; }
    public int NewCount { get; init; }
    public int RemovedCount { get; init; }
    public ClassificationSummary ClassificationSummary { get; init; } = new();
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<JobVacancy> NewJobs { get; init; } = [];
    public string? Error { get; init; }
}

public sealed record ClassificationSummary
{
    public int Relevant { get; init; }
    public int Review { get; init; }
    public int Excluded { get; init; }
}
