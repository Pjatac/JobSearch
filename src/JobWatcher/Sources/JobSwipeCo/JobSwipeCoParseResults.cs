using JobWatcher.Models;

namespace JobWatcher.Sources.JobSwipeCo;

public sealed record JobSwipeCoSearchParseResult(
    IReadOnlyList<string> JobUrls,
    IReadOnlyList<string> Warnings,
    int JsonLdBlockCount,
    int ItemListCount);

public sealed record JobSwipeCoJobParseResult(
    JobVacancy? Vacancy,
    IReadOnlyList<string> Warnings,
    int JsonLdBlockCount,
    int JobPostingCount);
