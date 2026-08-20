using JobWatcher.Models;

namespace JobWatcher.Sources.JobKarov;

public sealed record JobKarovParseResult(
    IReadOnlyList<JobVacancy> Vacancies,
    IReadOnlyList<string> Warnings,
    int JsonLdBlockCount,
    int JobPostingObjectCount,
    int RequirementsMergedCount = 0);
