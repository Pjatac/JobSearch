using JobWatcher.Models;

namespace JobWatcher.Persistence;

public interface ISnapshotStore
{
    Task<SourceSnapshot?> LoadAsync(string sourceName, CancellationToken cancellationToken);
    Task SaveAsync(SourceSnapshot snapshot, CancellationToken cancellationToken);
    Task DeleteStaleSnapshotsAsync(IReadOnlySet<string> retainedSourceNames, CancellationToken cancellationToken);
    string GetSnapshotPath(string sourceName);
}
