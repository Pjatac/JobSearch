namespace JobWatcher.Models;

public sealed record JobDiff
{
    public bool IsInitialRun { get; init; }
    public int PreviousCount { get; init; }
    public int CurrentCount { get; init; }
    public IReadOnlyList<JobVacancy> NewVacancies { get; init; } = [];
    public IReadOnlyList<JobVacancy> UnchangedVacancies { get; init; } = [];
    public IReadOnlyList<JobVacancy> RemovedVacancies { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
