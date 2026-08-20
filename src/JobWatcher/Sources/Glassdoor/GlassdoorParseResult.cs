using JobWatcher.Models;

namespace JobWatcher.Sources.Glassdoor;

public sealed record GlassdoorParseResult(
    IReadOnlyList<JobVacancy> Vacancies,
    IReadOnlyList<string> Warnings,
    int JobCardCount,
    int JsonLdBlockCount,
    int ItemListCount,
    int? TotalJobs);
