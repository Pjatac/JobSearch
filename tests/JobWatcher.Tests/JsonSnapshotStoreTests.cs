using JobWatcher.Configuration;
using JobWatcher.Models;
using JobWatcher.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobWatcher.Tests;

public sealed class JsonSnapshotStoreTests
{
    [Fact]
    public async Task MissingSnapshotReturnsNull()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path);

        var snapshot = await store.LoadAsync("JobKarov", CancellationToken.None);

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task SavesAndLoadsSnapshotRoundTrip()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path);

        await store.SaveAsync(Snapshot("JobKarov", "1"), CancellationToken.None);
        var loaded = await store.LoadAsync("JobKarov", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("1", Assert.Single(loaded.Vacancies).ExternalId);
    }

    [Fact]
    public async Task ReplacesExistingSnapshot()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path);

        await store.SaveAsync(Snapshot("JobKarov", "1"), CancellationToken.None);
        await store.SaveAsync(Snapshot("JobKarov", "2"), CancellationToken.None);
        var loaded = await store.LoadAsync("JobKarov", CancellationToken.None);

        Assert.Equal("2", Assert.Single(loaded!.Vacancies).ExternalId);
    }

    [Fact]
    public async Task DeletesStaleSnapshotsButKeepsRetainedSources()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp.Path);

        await store.SaveAsync(Snapshot("JobKarov-Software", "1"), CancellationToken.None);
        await store.SaveAsync(Snapshot("OldSource", "2"), CancellationToken.None);

        await store.DeleteStaleSnapshotsAsync(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "JobKarov-Software" },
            CancellationToken.None);

        Assert.True(File.Exists(store.GetSnapshotPath("JobKarov-Software")));
        Assert.False(File.Exists(store.GetSnapshotPath("OldSource")));
    }

    private static JsonSnapshotStore CreateStore(string dataDirectory)
    {
        return new JsonSnapshotStore(
            Options.Create(new JobWatcherOptions { DataDirectory = dataDirectory }),
            NullLogger<JsonSnapshotStore>.Instance);
    }

    private static SourceSnapshot Snapshot(string source, string id)
    {
        return new SourceSnapshot
        {
            Source = source,
            CollectedAtUtc = DateTimeOffset.UtcNow,
            Vacancies =
            [
                new JobVacancy
                {
                    Source = source,
                    ExternalId = id,
                    Title = "Title",
                    Url = $"https://www.jobkarov.com/Search/Site/{id}",
                    CollectedAtUtc = DateTimeOffset.UtcNow
                }
            ]
        };
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
