using JobWatcher.Configuration;
using JobWatcher.Models;
using JobWatcher.Persistence;
using JobWatcher.Services;
using JobWatcher.Sources;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobWatcher.Tests;

public sealed class JobWatcherRunnerOutputTests
{
    [Fact]
    public async Task DeduplicatesNewJobsAcrossSourcesInUserOutput()
    {
        using var temp = new TempDirectory();
        var options = Options.Create(new JobWatcherOptions
        {
            DataDirectory = temp.Path,
            Sources =
            [
                new JobSourceOptions { Name = "JobKarov-Software", Adapter = "JobKarov", Url = "https://example.test/software" },
                new JobSourceOptions { Name = "JobKarov-Cyber", Adapter = "JobKarov", Url = "https://example.test/cyber" }
            ]
        });

        var runner = new JobWatcherRunner(
            options,
            [new StaticSource("JobKarov", Vacancy("duplicate"))],
            new JsonSnapshotStore(options, NullLogger<JsonSnapshotStore>.Instance),
            new JobComparisonService(),
            new JobClassificationService(options),
            new DuplicateCandidateService(),
            new OutputDuplicateService(),
            NullLogger<JobWatcherRunner>.Instance);

        var exitCode = await runner.RunAsync(CancellationToken.None);

        var output = await LoadOutputAsync(temp.Path);
        Assert.Equal(0, exitCode);
        Assert.Equal(1, output!.TotalNewJobs);
        Assert.Equal(1, output.Sources[0].NewCount);
        Assert.Equal(0, output.Sources[1].NewCount);
        Assert.Single(output.Sources[0].NewJobs);
        Assert.Empty(output.Sources[1].NewJobs);
        Assert.Equal(1, output.Sources[0].ClassificationSummary.Review);
        Assert.NotNull(output.Sources[0].NewJobs[0].Classification);
    }

    [Fact]
    public async Task DeduplicatesNewJobsAcrossSitesByTitleAndCompany()
    {
        using var temp = new TempDirectory();
        var options = Options.Create(new JobWatcherOptions
        {
            DataDirectory = temp.Path,
            Sources =
            [
                new JobSourceOptions { Name = "JobKarov-Software", Adapter = "JobKarov", Url = "https://example.test/jobkarov" },
                new JobSourceOptions { Name = "Drushim-Backend", Adapter = "Drushim", Url = "https://example.test/drushim" }
            ]
        });

        var runner = new JobWatcherRunner(
            options,
            [
                new StaticSource("JobKarov", Vacancy("jobkarov-1", "Backend Engineer", "Acme", "https://jobkarov.example/1")),
                new StaticSource("Drushim", Vacancy("drushim-1", " backend  engineer ", "ACME", "https://drushim.example/2"))
            ],
            new JsonSnapshotStore(options, NullLogger<JsonSnapshotStore>.Instance),
            new JobComparisonService(),
            new JobClassificationService(options),
            new DuplicateCandidateService(),
            new OutputDuplicateService(),
            NullLogger<JobWatcherRunner>.Instance);

        var exitCode = await runner.RunAsync(CancellationToken.None);

        var output = await LoadOutputAsync(temp.Path);
        Assert.Equal(0, exitCode);
        Assert.Equal(1, output!.TotalNewJobs);
        Assert.Equal(1, output.Sources[0].NewCount);
        Assert.Equal(0, output.Sources[1].NewCount);

        var duplicateOutput = await LoadDuplicateOutputAsync(temp.Path);
        Assert.Equal(1, duplicateOutput!.CandidateCount);
    }

    [Fact]
    public async Task PersistsRawSnapshotWithoutPresentationClassification()
    {
        using var temp = new TempDirectory();
        var options = Options.Create(new JobWatcherOptions
        {
            DataDirectory = temp.Path,
            Classification = new JobClassificationOptions
            {
                IncludeSignals = ["C#", "Backend"]
            },
            Sources =
            [
                new JobSourceOptions { Name = "JobKarov-Software", Adapter = "JobKarov", Url = "https://example.test/software" }
            ]
        });
        var store = new JsonSnapshotStore(options, NullLogger<JsonSnapshotStore>.Instance);
        var runner = CreateRunner(
            options,
            [new StaticSource("JobKarov", Vacancy("backend-1", "Senior C# Backend Engineer"))],
            store);

        var exitCode = await runner.RunAsync(CancellationToken.None);

        var output = await LoadOutputAsync(temp.Path);
        var snapshot = await store.LoadAsync("JobKarov-Software", CancellationToken.None);
        Assert.Equal(0, exitCode);
        Assert.Equal("relevant", Assert.Single(output!.Sources[0].NewJobs).Classification?.Classification);
        Assert.Null(Assert.Single(snapshot!.Vacancies).Classification);
    }

    [Fact]
    public async Task DeletesStaleHistoryOutputsAfterSuccessfulRun()
    {
        using var temp = new TempDirectory();
        var options = Options.Create(new JobWatcherOptions
        {
            DataDirectory = temp.Path,
            OutputHistoryRetentionCount = 2,
            Sources =
            [
                new JobSourceOptions { Name = "JobKarov-Software", Adapter = "JobKarov", Url = "https://example.test/software" }
            ]
        });

        var runner = new JobWatcherRunner(
            options,
            [new StaticSource("JobKarov", Vacancy("stable"))],
            new JsonSnapshotStore(options, NullLogger<JsonSnapshotStore>.Instance),
            new JobComparisonService(),
            new JobClassificationService(options),
            new DuplicateCandidateService(),
            new OutputDuplicateService(),
            NullLogger<JobWatcherRunner>.Instance);

        Assert.Equal(0, await runner.RunAsync(CancellationToken.None));
        await Task.Delay(20);
        Assert.Equal(0, await runner.RunAsync(CancellationToken.None));
        await Task.Delay(20);
        Assert.Equal(0, await runner.RunAsync(CancellationToken.None));

        var historyFiles = Directory.GetFiles(Path.Combine(temp.Path, "output", "history"), "*.json");
        Assert.Equal(2, historyFiles.Length);
    }

    [Fact]
    public async Task OptionalSourceFailureDoesNotAffectExitCode()
    {
        using var temp = new TempDirectory();
        var options = Options.Create(new JobWatcherOptions
        {
            DataDirectory = temp.Path,
            Sources =
            [
                new JobSourceOptions { Name = "JobKarov-Software", Adapter = "JobKarov", Url = "https://example.test/software" },
                new JobSourceOptions { Name = "Glassdoor-Backend", Adapter = "Glassdoor", Optional = true, Url = "https://example.test/glassdoor" }
            ]
        });

        var runner = CreateRunner(
            options,
            [
                new StaticSource("JobKarov", Vacancy("jobkarov-1")),
                new FailingSource("Glassdoor", "Glassdoor served an anti-bot challenge page.")
            ]);

        var exitCode = await runner.RunAsync(CancellationToken.None);

        var output = await LoadOutputAsync(temp.Path);
        Assert.Equal(0, exitCode);
        Assert.True(output!.HasFailures);
        Assert.Equal("failed", output.Sources[1].Status);
        Assert.True(output.Sources[1].Optional);
    }

    [Fact]
    public async Task RequiredSourceFailureStillFailsTheRun()
    {
        using var temp = new TempDirectory();
        var options = Options.Create(new JobWatcherOptions
        {
            DataDirectory = temp.Path,
            Sources =
            [
                new JobSourceOptions { Name = "JobKarov-Software", Adapter = "JobKarov", Url = "https://example.test/software" },
                new JobSourceOptions { Name = "Glassdoor-Backend", Adapter = "Glassdoor", Url = "https://example.test/glassdoor" }
            ]
        });

        var runner = CreateRunner(
            options,
            [
                new StaticSource("JobKarov", Vacancy("jobkarov-1")),
                new FailingSource("Glassdoor", "HTTP 403 Forbidden")
            ]);

        var exitCode = await runner.RunAsync(CancellationToken.None);

        var output = await LoadOutputAsync(temp.Path);
        Assert.Equal(1, exitCode);
        Assert.False(output!.Sources[1].Optional);
    }

    [Fact]
    public async Task FailedSourceKeepsItsPreviousSnapshot()
    {
        using var temp = new TempDirectory();
        var options = Options.Create(new JobWatcherOptions
        {
            DataDirectory = temp.Path,
            Sources =
            [
                new JobSourceOptions { Name = "JobKarov-Software", Adapter = "JobKarov", Url = "https://example.test/software" },
                new JobSourceOptions { Name = "Glassdoor-Backend", Adapter = "Glassdoor", Optional = true, Url = "https://example.test/glassdoor" }
            ]
        });

        var store = new JsonSnapshotStore(options, NullLogger<JsonSnapshotStore>.Instance);
        var snapshotPath = store.GetSnapshotPath("Glassdoor-Backend");

        // First run: Glassdoor succeeds and writes a snapshot.
        Assert.Equal(0, await CreateRunner(options, [
            new StaticSource("JobKarov", Vacancy("jobkarov-1")),
            new StaticSource("Glassdoor", Vacancy("glassdoor-1"))
        ], store).RunAsync(CancellationToken.None));
        Assert.True(File.Exists(snapshotPath));

        // Second run: Glassdoor is blocked. Its snapshot must survive, otherwise the next
        // successful run would report every vacancy as new again.
        Assert.Equal(0, await CreateRunner(options, [
            new StaticSource("JobKarov", Vacancy("jobkarov-1")),
            new FailingSource("Glassdoor", "Glassdoor served an anti-bot challenge page.")
        ], store).RunAsync(CancellationToken.None));

        Assert.True(File.Exists(snapshotPath));
    }

    [Fact]
    public async Task PartialFailedSourceWritesOutputButDoesNotReplaceSnapshot()
    {
        using var temp = new TempDirectory();
        var options = Options.Create(new JobWatcherOptions
        {
            DataDirectory = temp.Path,
            Sources =
            [
                new JobSourceOptions { Name = "DevJobs-Backend", Adapter = "DevJobs", Url = "https://example.test/devjobs" }
            ]
        });
        var store = new JsonSnapshotStore(options, NullLogger<JsonSnapshotStore>.Instance);
        await store.SaveAsync(new SourceSnapshot
        {
            Source = "DevJobs-Backend",
            CollectedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            Vacancies = [Vacancy("stable") with { Source = "DevJobs-Backend" }]
        }, CancellationToken.None);

        var exitCode = await CreateRunner(options, [
            new PartialSource("DevJobs", [Vacancy("stable"), Vacancy("partial-new")], "Detail request timed out.")
        ], store).RunAsync(CancellationToken.None);

        var output = await LoadOutputAsync(temp.Path);
        var snapshot = await store.LoadAsync("DevJobs-Backend", CancellationToken.None);
        Assert.Equal(1, exitCode);
        Assert.True(output!.HasFailures);
        Assert.Equal("partial_failed", output.Sources[0].Status);
        Assert.Equal("Detail request timed out.", output.Sources[0].Error);
        Assert.Equal(1, output.Sources[0].NewCount);
        Assert.Equal("partial-new", Assert.Single(output.Sources[0].NewJobs).ExternalId);
        Assert.Equal("stable", Assert.Single(snapshot!.Vacancies).ExternalId);
    }

    [Fact]
    public async Task CancelledSourceNotifiesObserverBeforeRunStops()
    {
        using var temp = new TempDirectory();
        using var cts = new CancellationTokenSource();
        var updates = new List<SourceOutput>();
        var options = Options.Create(new JobWatcherOptions
        {
            DataDirectory = temp.Path,
            Sources =
            [
                new JobSourceOptions { Name = "DevJobs-Backend", Adapter = "DevJobs", Url = "https://example.test/devjobs" }
            ]
        });
        var runner = new JobWatcherRunner(
            options,
            [new CancelledSource("DevJobs")],
            new JsonSnapshotStore(options, NullLogger<JsonSnapshotStore>.Instance),
            new JobComparisonService(),
            new JobClassificationService(options),
            new DuplicateCandidateService(),
            new OutputDuplicateService(),
            NullLogger<JobWatcherRunner>.Instance,
            [new RecordingObserver(updates)]);

        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => runner.RunAsync(cts.Token));
        var cancelled = Assert.Single(updates);
        Assert.Equal("DevJobs-Backend", cancelled.Source);
        Assert.Equal("cancelled", cancelled.Status);
    }

    private static JobWatcherRunner CreateRunner(
        IOptions<JobWatcherOptions> options,
        IEnumerable<IJobSource> sources,
        ISnapshotStore? snapshotStore = null)
    {
        return new JobWatcherRunner(
            options,
            sources,
            snapshotStore ?? new JsonSnapshotStore(options, NullLogger<JsonSnapshotStore>.Instance),
            new JobComparisonService(),
            new JobClassificationService(options),
            new DuplicateCandidateService(),
            new OutputDuplicateService(),
            NullLogger<JobWatcherRunner>.Instance);
    }

    private static async Task<RunOutput?> LoadOutputAsync(string dataDirectory)
    {
        await using var stream = File.OpenRead(Path.Combine(dataDirectory, "output", "new-jobs.json"));
        return await JobWatcher.Utilities.JsonDefaults.DeserializeAsync<RunOutput>(stream, CancellationToken.None);
    }

    private static async Task<DuplicateCandidatesOutput?> LoadDuplicateOutputAsync(string dataDirectory)
    {
        await using var stream = File.OpenRead(Path.Combine(dataDirectory, "output", "duplicate-candidates.json"));
        return await JobWatcher.Utilities.JsonDefaults.DeserializeAsync<DuplicateCandidatesOutput>(stream, CancellationToken.None);
    }

    private static JobVacancy Vacancy(string id, string title = "Backend Engineer", string? company = null, string? url = null)
    {
        return new JobVacancy
        {
            Source = "JobKarov",
            ExternalId = id,
            Title = title,
            Company = company,
            Url = url ?? $"https://www.jobkarov.com/Search/Site/{id}",
            CollectedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private sealed class StaticSource(string name, JobVacancy vacancy) : IJobSource
    {
        public string Name { get; } = name;

        public Task<SourceRunResult> FetchAsync(JobSourceOptions options, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SourceRunResult
            {
                Source = options.Name,
                Success = true,
                Snapshot = new SourceSnapshot
                {
                    Source = options.Name,
                    CollectedAtUtc = collectedAtUtc,
                    Vacancies = [vacancy with { Source = Name, CollectedAtUtc = collectedAtUtc }]
                }
            });
        }
    }

    private sealed class FailingSource(string name, string error) : IJobSource
    {
        public string Name { get; } = name;

        public Task<SourceRunResult> FetchAsync(JobSourceOptions options, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SourceRunResult
            {
                Source = options.Name,
                Success = false,
                Error = error
            });
        }
    }

    private sealed class PartialSource(string name, IReadOnlyList<JobVacancy> vacancies, string error) : IJobSource
    {
        public string Name { get; } = name;

        public Task<SourceRunResult> FetchAsync(JobSourceOptions options, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SourceRunResult
            {
                Source = options.Name,
                Success = false,
                IsPartial = true,
                Snapshot = new SourceSnapshot
                {
                    Source = options.Name,
                    CollectedAtUtc = collectedAtUtc,
                    Vacancies = vacancies.Select(vacancy => vacancy with { Source = options.Name, CollectedAtUtc = collectedAtUtc }).ToList()
                },
                Error = error
            });
        }
    }

    private sealed class CancelledSource(string name) : IJobSource
    {
        public string Name { get; } = name;

        public Task<SourceRunResult> FetchAsync(JobSourceOptions options, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class RecordingObserver(List<SourceOutput> updates) : IJobWatcherRunObserver
    {
        public void SourceStarted(string source)
        {
        }

        public void SourceFinished(SourceOutput sourceOutput)
        {
            updates.Add(sourceOutput);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"jobwatcher-tests-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
