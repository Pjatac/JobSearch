using JobWatcher.Models;
using JobWatcher.Utilities;

namespace JobWatcher.Services;

public sealed class JobComparisonService
{
    public JobDiff Compare(SourceSnapshot? previousSnapshot, SourceSnapshot currentSnapshot)
    {
        var previous = Deduplicate(previousSnapshot?.Vacancies ?? []);
        var current = Deduplicate(currentSnapshot.Vacancies);
        var warnings = new List<string>();

        if (previous.Count >= 10 && current.Count < previous.Count * 0.5)
        {
            warnings.Add($"Current count {current.Count} is less than 50% of previous count {previous.Count}.");
        }

        return new JobDiff
        {
            IsInitialRun = previousSnapshot is null,
            PreviousCount = previous.Count,
            CurrentCount = current.Count,
            NewVacancies = current.Where(pair => !previous.ContainsKey(pair.Key)).Select(pair => pair.Value).ToList(),
            UnchangedVacancies = current.Where(pair => previous.ContainsKey(pair.Key)).Select(pair => pair.Value).ToList(),
            RemovedVacancies = previous.Where(pair => !current.ContainsKey(pair.Key)).Select(pair => pair.Value).ToList(),
            Warnings = warnings
        };
    }

    private static Dictionary<string, JobVacancy> Deduplicate(IEnumerable<JobVacancy> vacancies)
    {
        var result = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
        foreach (var vacancy in vacancies)
        {
            result.TryAdd(VacancyIdentity.CreateKey(vacancy), vacancy);
        }

        return result;
    }
}
