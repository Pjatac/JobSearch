using System.Text;
using JobWatcher.Utilities;
using Microsoft.Extensions.Logging;

namespace JobWatcher.App;

internal sealed class ManualRunFileLoggerProvider(string path) : ILoggerProvider
{
    private static readonly TimeSpan LogRetention = TimeSpan.FromHours(24);
    private readonly object writeLock = new();

    public ILogger CreateLogger(string categoryName) => new ManualRunFileLogger(this, categoryName);

    public void Dispose()
    {
    }

    public static string CreateLogPath()
    {
        var directory = Path.Combine(FileSystem.AppDataDirectory, "data", "diagnostics", "manual-runs");
        return CreateLogPath(directory, DateTimeOffset.UtcNow);
    }

    internal static string CreateLogPath(string directory, DateTimeOffset now)
    {
        Directory.CreateDirectory(directory);
        DeleteExpiredLogs(directory, now);
        return Path.Combine(directory, $"run-{now:yyyyMMddTHHmmssfffZ}.log");
    }

    private static void DeleteExpiredLogs(string directory, DateTimeOffset now)
    {
        LogFileRetention.DeleteOlderThan(directory, "run-*.log", now, LogRetention);
    }

    private void Write(LogLevel logLevel, string categoryName, string message, Exception? exception)
    {
        try
        {
            var entry = $"{DateTimeOffset.UtcNow:O} | {logLevel,-11} | {categoryName} | {message}";
            if (exception is not null)
            {
                entry = $"{entry}{Environment.NewLine}{exception}";
            }

            lock (writeLock)
            {
                File.AppendAllText(path, $"{entry}{Environment.NewLine}", new UTF8Encoding(false));
            }
        }
        catch
        {
            // Diagnostics must never interfere with vacancy collection.
        }
    }

    private sealed class ManualRunFileLogger(ManualRunFileLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                provider.Write(logLevel, categoryName, formatter(state, exception), exception);
            }
        }
    }
}
