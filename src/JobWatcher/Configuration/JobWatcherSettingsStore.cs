using JobWatcher.Utilities;

namespace JobWatcher.Configuration;

/// <summary>
/// Persists user-owned search profiles and classification rules separately from runtime defaults.
/// Keeping each user collection whole makes profile deletion and reordering deterministic.
/// </summary>
public sealed class JobWatcherSettingsStore
{
    public async Task<JobWatcherOptions> LoadOrCreateAsync(
        string settingsPath,
        string defaultSettingsJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultSettingsJson);

        if (File.Exists(settingsPath))
        {
            var savedJson = await File.ReadAllTextAsync(settingsPath, cancellationToken);
            var userSettings = DeserializeUserSettings(savedJson);
            if (userSettings is null)
            {
                userSettings = ToUserSettings(DeserializeDefaultSettings(savedJson));
                await SaveAsync(settingsPath, userSettings, cancellationToken);
            }

            return Merge(DeserializeDefaultSettings(defaultSettingsJson), userSettings);
        }

        var defaults = DeserializeDefaultSettings(defaultSettingsJson);
        var createdSettings = ToUserSettings(defaults);
        await SaveAsync(settingsPath, createdSettings, cancellationToken);
        return Merge(defaults, createdSettings);
    }

    public Task SaveAsync(
        string settingsPath,
        JobWatcherUserSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        ArgumentNullException.ThrowIfNull(settings);

        return AtomicFileWriter.WriteJsonAsync(
            settingsPath,
            settings,
            cancellationToken);
    }

    public async Task<JobWatcherOptions> UpdateAsync(
        string settingsPath,
        string defaultSettingsJson,
        Func<JobWatcherUserSettings, JobWatcherUserSettings> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        var defaults = DeserializeDefaultSettings(defaultSettingsJson);
        JobWatcherUserSettings? current = null;
        if (File.Exists(settingsPath))
        {
            var savedJson = await File.ReadAllTextAsync(settingsPath, cancellationToken);
            current = DeserializeUserSettings(savedJson) ?? ToUserSettings(DeserializeDefaultSettings(savedJson));
        }
        current ??= ToUserSettings(defaults);

        var next = update(current);
        await SaveAsync(settingsPath, next, cancellationToken);
        return Merge(defaults, next);
    }

    public Task SaveAsync(
        string settingsPath,
        JobWatcherOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SaveAsync(settingsPath, ToUserSettings(options), cancellationToken);
    }

    private static JobWatcherOptions DeserializeDefaultSettings(string json)
    {
        var document = System.Text.Json.JsonSerializer.Deserialize<JobWatcherSettingsDocument>(json, JsonDefaults.Options);
        if (document?.JobWatcher is null)
        {
            throw new InvalidOperationException("Settings document must contain a JobWatcher section.");
        }

        return document.JobWatcher;
    }

    private static JobWatcherUserSettings? DeserializeUserSettings(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("jobWatcher", out _)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<JobWatcherUserSettings>(json, JsonDefaults.Options)
                ?? throw new InvalidOperationException("User settings document is empty.");
    }

    private static JobWatcherUserSettings ToUserSettings(JobWatcherOptions options)
    {
        return new JobWatcherUserSettings
        {
            Sources = options.Sources,
            Classification = options.Classification
        };
    }

    private static JobWatcherOptions Merge(JobWatcherOptions defaults, JobWatcherUserSettings userSettings)
    {
        return new JobWatcherOptions
        {
            DataDirectory = defaults.DataDirectory,
            RequestTimeoutSeconds = defaults.RequestTimeoutSeconds,
            OutputHistoryRetentionCount = defaults.OutputHistoryRetentionCount,
            Sources = userSettings.Sources,
            Classification = userSettings.Classification
        };
    }
}

public sealed record JobWatcherSettingsDocument
{
    public required JobWatcherOptions JobWatcher { get; init; }
}

public sealed record JobWatcherUserSettings
{
    public IReadOnlyList<JobSourceOptions> Sources { get; init; } = [];
    public JobClassificationOptions Classification { get; init; } = new();
}
