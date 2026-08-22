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

        var logPath = ManualRunFileLoggerProvider.CreateLogPath();
        var fileLoggerProvider = new ManualRunFileLoggerProvider(logPath);
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddProvider(fileLoggerProvider);
        });
        services.AddSingleton<IOptions<JobWatcherOptions>>(Options.Create(options));
        services.AddSingleton<IJobWatcherRunObserver>(new ProgressObserver(onProgress));
        services.AddJobWatcherCollector();
        await using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<ManualRunService>>();
        logger.LogInformation(
            "Manual run started. Enabled profiles: {EnabledProfileCount}. Request timeout: {RequestTimeoutSeconds}s. Log: {LogPath}",
            options.Sources.Count(source => source.Enabled),
            options.RequestTimeoutSeconds,
            logPath);

        try
        {
            var exitCode = await provider.GetRequiredService<JobWatcherRunner>().RunAsync(cancellationToken);
            logger.LogInformation("Manual run finished with exit code {ExitCode}", exitCode);
            runState.NotifyRunCompleted();
            return exitCode;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Manual run was cancelled");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Manual run stopped with an unhandled exception");
            throw;
        }
    }

    private sealed class ProgressObserver(Action<RunProgressUpdate> onProgress) : IJobWatcherRunObserver
    {
        public void SourceStarted(string source) => onProgress(new RunProgressUpdate(source, "running", null, null));
        public void SourceFinished(JobWatcher.Models.SourceOutput sourceOutput) => onProgress(new RunProgressUpdate(sourceOutput.Source, sourceOutput.Status, sourceOutput.Error, sourceOutput));
    }
}

public sealed record RunProgressUpdate(string Source, string Status, string? Error, JobWatcher.Models.SourceOutput? Output);
