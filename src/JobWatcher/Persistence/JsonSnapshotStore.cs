using JobWatcher.Configuration;
using JobWatcher.Models;
using JobWatcher.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobWatcher.Persistence;

public sealed class JsonSnapshotStore(IOptions<JobWatcherOptions> options, ILogger<JsonSnapshotStore> logger) : ISnapshotStore
{
    public async Task<SourceSnapshot?> LoadAsync(string sourceName, CancellationToken cancellationToken)
    {
        var path = GetSnapshotPath(sourceName);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonDefaults.DeserializeAsync<SourceSnapshot>(stream, cancellationToken);
    }

    public async Task SaveAsync(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        var path = GetSnapshotPath(snapshot.Source);
        await AtomicFileWriter.WriteJsonAsync(path, snapshot, cancellationToken);
        logger.LogInformation("Saved snapshot for {Source} to {Path}", snapshot.Source, path);
    }

    public Task DeleteStaleSnapshotsAsync(IReadOnlySet<string> retainedSourceNames, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var retainedPaths = retainedSourceNames
            .Select(GetSnapshotPath)
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var snapshotsDirectory = Path.Combine(options.Value.DataDirectory, "snapshots");
        if (!Directory.Exists(snapshotsDirectory))
        {
            return Task.CompletedTask;
        }

        foreach (var snapshotPath in Directory.EnumerateFiles(snapshotsDirectory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(snapshotPath);
            if (retainedPaths.Contains(fullPath))
            {
                continue;
            }

            File.Delete(fullPath);
            logger.LogInformation("Deleted stale snapshot {Path}", fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetSnapshotPath(string sourceName)
    {
        return Path.Combine(options.Value.DataDirectory, "snapshots", $"{FileNames.ToSafeName(sourceName)}.json");
    }
}
