namespace JobWatcher.Models;

public sealed record SourceSnapshot
{
    public required string Source { get; init; }
    public required DateTimeOffset CollectedAtUtc { get; init; }
    public IReadOnlyList<JobVacancy> Vacancies { get; init; } = [];
}
