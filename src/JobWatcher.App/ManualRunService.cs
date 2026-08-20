using JobWatcher.Configuration;
using JobWatcher.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobWatcher.App;

public sealed class ManualRunService(RunStateService runState)
{
    public async Task<int> RunAsync(JobWatcherOptions settings, Action<RunProgressUpdate> onProgress, CancellationToken cancellationToken)
    {
        var options = new JobWatcherOptions
        {
            DataDirectory = Path.Combine(FileSystem.AppDataDirectory, "data"),
            RequestTimeoutSeconds = settings.RequestTimeoutSeconds,
            OutputHistoryRetentionCount = settings.OutputHistoryRetentionCount,
            Sources = settings.Sources,
            Classification = settings.Classification
        };

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<IOptions<JobWatcherOptions>>(Options.Create(options));
        services.AddSingleton<IJobWatcherRunObserver>(new ProgressObserver(onProgress));
        services.AddJobWatcherCollector();
        await using var provider = services.BuildServiceProvider();
        var exitCode = await provider.GetRequiredService<JobWatcherRunner>().RunAsync(cancellationToken);
        runState.NotifyRunCompleted();
        return exitCode;
    }

    private sealed class ProgressObserver(Action<RunProgressUpdate> onProgress) : IJobWatcherRunObserver
    {
        public void SourceStarted(string source) => onProgress(new RunProgressUpdate(source, "running", null, null));
        public void SourceFinished(JobWatcher.Models.SourceOutput sourceOutput) => onProgress(new RunProgressUpdate(sourceOutput.Source, sourceOutput.Status, sourceOutput.Error, sourceOutput));
    }
}

public sealed record RunProgressUpdate(string Source, string Status, string? Error, JobWatcher.Models.SourceOutput? Output);
