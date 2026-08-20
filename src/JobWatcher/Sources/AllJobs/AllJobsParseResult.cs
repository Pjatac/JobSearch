using JobWatcher.Models;

namespace JobWatcher.Sources.AllJobs;

public sealed record AllJobsParseResult(
    IReadOnlyList<JobVacancy> Vacancies,
    IReadOnlyList<string> Warnings,
    int JobCardCount,
    int? TotalPages,
    int? TotalJobs);
