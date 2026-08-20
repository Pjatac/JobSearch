using JobWatcher.Configuration;
using JobWatcher.Models;
using JobWatcher.Persistence;
using JobWatcher.Sources;
using JobWatcher.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobWatcher.Services;

public sealed class JobWatcherRunner(
    IOptions<JobWatcherOptions> options,
    IEnumerable<IJobSource> sources,
    ISnapshotStore snapshotStore,
    JobComparisonService comparisonService,
    JobClassificationService classificationService,
    DuplicateCandidateService duplicateCandidateService,
    OutputDuplicateService outputDuplicateService,
    ILogger<JobWatcherRunner> logger,
    IEnumerable<IJobWatcherRunObserver>? observers = null)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var generatedAtUtc = DateTimeOffset.UtcNow;
        logger.LogInformation("JobWatcher started at {GeneratedAtUtc}", generatedAtUtc);
        EnsureDirectories();

        var sourceOutputs = new List<SourceOutput>();
        var enabledCount = 0;
        var successCount = 0;
        var failureCount = 0;
        var requiredFailureCount = 0;
        var successfulSnapshotSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var successfulSnapshots = new List<SourceSnapshot>();

        // Snapshot retention removes snapshots of sources that are no longer configured. A source
        // that ran and failed keeps its snapshot: deleting it would make the next successful run
        // report every vacancy as new, which matters for sources that fail intermittently.
        var retainedSnapshotSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceOptions in options.Value.Sources)
        {
            if (!sourceOptions.Enabled)
            {
                var disabledOutput = new SourceOutput { Source = sourceOptions.Name, Status = "disabled" };
                sourceOutputs.Add(disabledOutput);
                NotifyFinished(disabledOutput);
                continue;
            }

            enabledCount++;
            retainedSnapshotSources.Add(sourceOptions.Name);
            NotifyStarted(sourceOptions.Name);
            var adapterName = sourceOptions.Adapter ?? sourceOptions.Name;
            var source = sources.FirstOrDefault(s => string.Equals(s.Name, adapterName, StringComparison.OrdinalIgnoreCase));
            if (source is null)
            {
                failureCount++;
                if (!sourceOptions.Optional)
                {
                    requiredFailureCount++;
                }

                var failedOutput = new SourceOutput
                {
                    Source = sourceOptions.Name,
                    Status = "failed",
                    Optional = sourceOptions.Optional,
                    Error = $"No source adapter registered for '{adapterName}'."
                };
                sourceOutputs.Add(failedOutput);
                NotifyFinished(failedOutput);
                continue;
            }

            var runResult = await source.FetchAsync(sourceOptions, generatedAtUtc, cancellationToken);
            if (!runResult.Success || runResult.Snapshot is null)
            {
                failureCount++;
                if (!sourceOptions.Optional)
                {
                    requiredFailureCount++;
                }

                var failedOutput = new SourceOutput
                {
                    Source = sourceOptions.Name,
                    Status = "failed",
                    Optional = sourceOptions.Optional,
                    Error = runResult.Error,
                    Warnings = runResult.Warnings
                };
                sourceOutputs.Add(failedOutput);
                NotifyFinished(failedOutput);

                if (sourceOptions.Optional)
                {
                    logger.LogWarning(
                        "Optional source {Source} failed and is not counted towards the exit code: {Error}",
                        sourceOptions.Name,
                        runResult.Error);
                }
                else
                {
                    logger.LogError("Source {Source} failed: {Error}", sourceOptions.Name, runResult.Error);
                }

                continue;
            }

            var previous = await snapshotStore.LoadAsync(sourceOptions.Name, cancellationToken);
            var diff = comparisonService.Compare(previous, runResult.Snapshot);
            var warnings = runResult.Warnings.Concat(diff.Warnings).ToList();
            var newJobs = diff.NewVacancies
                .Select(job => job with { Classification = classificationService.ClassifyJob(job) })
                .ToList();
            var classificationSummary = CreateClassificationSummary(newJobs);

            logger.LogInformation(
                "Source {Source}: previous {PreviousCount}, current {CurrentCount}, new {NewCount}, removed {RemovedCount}",
                sourceOptions.Name,
                diff.PreviousCount,
                diff.CurrentCount,
                diff.NewVacancies.Count,
                diff.RemovedVacancies.Count);

            foreach (var warning in warnings)
            {
                logger.LogWarning("Source {Source}: {Warning}", sourceOptions.Name, warning);
            }

            await snapshotStore.SaveAsync(runResult.Snapshot, cancellationToken);
            successfulSnapshotSources.Add(sourceOptions.Name);
            successfulSnapshots.Add(runResult.Snapshot);
            successCount++;
            var successfulOutput = new SourceOutput
            {
                Source = sourceOptions.Name,
                Status = "success",
                IsInitialRun = diff.IsInitialRun,
                PreviousCount = diff.PreviousCount,
                CurrentCount = diff.CurrentCount,
                NewCount = newJobs.Count,
                RemovedCount = diff.RemovedVacancies.Count,
                ClassificationSummary = classificationSummary,
                Warnings = warnings,
                NewJobs = newJobs
            };
            sourceOutputs.Add(successfulOutput);
            NotifyFinished(successfulOutput);
        }

        sourceOutputs = DeduplicateNewJobsForOutput(sourceOutputs);

        var output = new RunOutput
        {
            GeneratedAtUtc = generatedAtUtc,
            HasFailures = failureCount > 0,
            TotalNewJobs = sourceOutputs.Sum(s => s.NewCount),
            Sources = sourceOutputs
        };

        var outputPath = Path.Combine(options.Value.DataDirectory, "output", "new-jobs.json");
        await AtomicFileWriter.WriteJsonAsync(outputPath, output, cancellationToken);
        await WriteOutputDuplicatesAsync(generatedAtUtc, sourceOutputs, cancellationToken);
        await WriteDuplicateCandidatesOutputAsync(generatedAtUtc, successfulSnapshots, cancellationToken);
        await WriteHistoryOutputAsync(generatedAtUtc, output, cancellationToken);
        logger.LogInformation("Wrote run output to {OutputPath}", outputPath);

        if (enabledCount == 0 || successCount == 0)
        {
            return 2;
        }

        await snapshotStore.DeleteStaleSnapshotsAsync(retainedSnapshotSources, cancellationToken);

        return requiredFailureCount > 0 ? 1 : 0;
    }

    private void NotifyStarted(string source)
    {
        foreach (var observer in observers ?? [])
        {
            observer.SourceStarted(source);
        }
    }

    private void NotifyFinished(SourceOutput sourceOutput)
    {
        foreach (var observer in observers ?? [])
        {
            observer.SourceFinished(sourceOutput);
        }
    }

    private static List<SourceOutput> DeduplicateNewJobsForOutput(IEnumerable<SourceOutput> sourceOutputs)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduplicatedOutputs = new List<SourceOutput>();

        foreach (var sourceOutput in sourceOutputs)
        {
            var newJobs = new List<JobVacancy>();
            foreach (var job in sourceOutput.NewJobs)
            {
                var key = CreateOutputDeduplicationKey(job);
                if (seen.Add(key))
                {
                    newJobs.Add(job);
                }
            }

            deduplicatedOutputs.Add(sourceOutput with
            {
                NewJobs = newJobs,
                NewCount = newJobs.Count,
                ClassificationSummary = CreateClassificationSummary(newJobs)
            });
        }

        return deduplicatedOutputs;
    }

    private static string CreateOutputDeduplicationKey(JobVacancy job)
    {
        var normalizedTitle = NormalizeForOutputDeduplication(job.Title);
        var normalizedCompany = NormalizeForOutputDeduplication(job.Company);

        if (!string.IsNullOrWhiteSpace(normalizedTitle) && !string.IsNullOrWhiteSpace(normalizedCompany))
        {
            return $"title-company:{normalizedTitle}|{normalizedCompany}";
        }

        if (!string.IsNullOrWhiteSpace(job.Url))
        {
            return $"url:{job.Url.Trim().ToLowerInvariant()}";
        }

        return VacancyIdentity.CreateKey(job);
    }

    private static string? NormalizeForOutputDeduplication(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : string.Join(' ', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static ClassificationSummary CreateClassificationSummary(IReadOnlyList<JobVacancy> jobs)
    {
        return new ClassificationSummary
        {
            Relevant = jobs.Count(job => job.Classification?.Classification == "relevant"),
            Review = jobs.Count(job => job.Classification?.Classification == "review"),
            Excluded = jobs.Count(job => job.Classification?.Classification == "excluded")
        };
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(Path.Combine(options.Value.DataDirectory, "snapshots"));
        Directory.CreateDirectory(Path.Combine(options.Value.DataDirectory, "output"));
        Directory.CreateDirectory(Path.Combine(options.Value.DataDirectory, "output", "history"));
        Directory.CreateDirectory(Path.Combine(options.Value.DataDirectory, "diagnostics"));
    }

    private async Task WriteHistoryOutputAsync(DateTimeOffset generatedAtUtc, RunOutput output, CancellationToken cancellationToken)
    {
        var historyDirectory = Path.Combine(options.Value.DataDirectory, "output", "history");
        var path = Path.Combine(historyDirectory, $"{generatedAtUtc:yyyyMMddTHHmmssfffZ}.json");
        await AtomicFileWriter.WriteJsonAsync(path, output, cancellationToken);
        DeleteOldHistoryOutputs(historyDirectory);
    }

    private void DeleteOldHistoryOutputs(string historyDirectory)
    {
        var retentionCount = Math.Max(0, options.Value.OutputHistoryRetentionCount);
        if (retentionCount == 0)
        {
            return;
        }

        var staleFiles = Directory.GetFiles(historyDirectory, "*.json")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(retentionCount);

        foreach (var file in staleFiles)
        {
            file.Delete();
            logger.LogInformation("Deleted stale history output {Path}", file.FullName);
        }
    }

    private async Task WriteOutputDuplicatesAsync(
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<SourceOutput> sourceOutputs,
        CancellationToken cancellationToken)
    {
        var report = outputDuplicateService.Review(generatedAtUtc, sourceOutputs);
        var path = Path.Combine(options.Value.DataDirectory, "output", "new-jobs-duplicates.json");
        await AtomicFileWriter.WriteJsonAsync(path, report, cancellationToken);

        if (report.DuplicateGroupCount > 0)
        {
            logger.LogWarning(
                "Output review: {GroupCount} duplicate groups covering {RedundantCount} redundant entries out of {ReviewedCount} jobs; see {Path}",
                report.DuplicateGroupCount,
                report.RedundantJobCount,
                report.ReviewedJobCount,
                path);
        }
        else
        {
            logger.LogInformation("Output review: no duplicates among {ReviewedCount} jobs", report.ReviewedJobCount);
        }

        if (report.SharedDescriptionGroupCount > 0)
        {
            logger.LogInformation(
                "Output review: {GroupCount} groups of different jobs share an identical description (boilerplate or search-teaser text, not duplicates)",
                report.SharedDescriptionGroupCount);
        }
    }

    private async Task WriteDuplicateCandidatesOutputAsync(
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<SourceSnapshot> successfulSnapshots,
        CancellationToken cancellationToken)
    {
        var output = duplicateCandidateService.FindCandidates(generatedAtUtc, successfulSnapshots);
        var path = Path.Combine(options.Value.DataDirectory, "output", "duplicate-candidates.json");
        await AtomicFileWriter.WriteJsonAsync(path, output, cancellationToken);
        logger.LogInformation(
            "Wrote {CandidateCount} duplicate candidates to {OutputPath}",
            output.CandidateCount,
            path);
    }
}
