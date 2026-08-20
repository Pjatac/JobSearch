using JobWatcher.Models;

namespace JobWatcher.Sources.Drushim;

public sealed record DrushimParseResult(
    IReadOnlyList<JobVacancy> Vacancies,
    IReadOnlyList<string> Warnings,
    int JobCardCount);
