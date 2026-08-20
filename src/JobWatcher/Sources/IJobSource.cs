using JobWatcher.Configuration;
using JobWatcher.Models;

namespace JobWatcher.Sources;

public interface IJobSource
{
    string Name { get; }
    Task<SourceRunResult> FetchAsync(JobSourceOptions options, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken);
}
