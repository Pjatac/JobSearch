using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JobWatcher.Configuration;

namespace JobWatcher.App;

public sealed class DashboardViewModel(JobWatcherSettingsStore settingsStore, ManualRunService manualRunService, SourceProfileValidator profileValidator) : INotifyPropertyChanged
{
    private bool initialized;
    private string status = "Loading configuration...";
    private string classificationSummary = string.Empty;
    private string glassdoorSessionStatus = string.Empty;
    private JobWatcherOptions? options;
    private CancellationTokenSource? runCancellation;
    private bool isRunning;

    public ObservableCollection<SourceProfileSummary> Sources { get; } = [];
    public ObservableCollection<SourceRunStatus> RunStatuses { get; } = [];

    public string Status
    {
        get => status;
        private set => SetField(ref status, value);
    }

    public string ClassificationSummary
    {
        get => classificationSummary;
        private set => SetField(ref classificationSummary, value);
    }

    public string GlassdoorSessionStatus
    {
        get => glassdoorSessionStatus;
        private set => SetField(ref glassdoorSessionStatus, value);
    }

    public bool IsRunning
    {
        get => isRunning;
        private set
        {
            if (SetField(ref isRunning, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RunButtonText)));
            }
        }
    }

    public string RunButtonText => IsRunning ? "Cancel" : "Run";

    public async Task InitializeAsync()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        try
        {
            options = await LoadSettingsAsync();

            var classification = options.Classification;
            ClassificationSummary = $"{classification.IncludeSignals.Count} include signals, {classification.RoleMismatchSignals.Count} role exclusions, {classification.OtherPrimaryLanguages.Count} specialization exclusions";
            Status = Sources.Count == 0
                ? "No search profiles configured"
                : $"{Sources.Count(profile => profile.IsActive)} of {Sources.Count} profiles enabled";
        }
        catch (Exception ex)
        {
            Status = $"Configuration could not be loaded: {ex.Message}";
        }
    }

    public async Task ToggleRunAsync()
    {
        if (IsRunning)
        {
            runCancellation?.Cancel();
            Status = "Cancelling run...";
            return;
        }

        options = await LoadSettingsAsync();
        var invalidProfiles = options.Sources
            .Where(source => source.Enabled)
            .Select(source => (Source: source, Validation: profileValidator.Validate(source)))
            .Where(item => !item.Validation.IsValid)
            .ToList();
        if (invalidProfiles.Count > 0)
        {
            RunStatuses.Clear();
            foreach (var invalid in invalidProfiles)
            {
                RunStatuses.Add(new SourceRunStatus(invalid.Source.Name, "needs attention", string.Join(" ", invalid.Validation.Errors)));
            }

            Status = $"Fix {invalidProfiles.Count} enabled profile(s) before running";
            return;
        }

        runCancellation = new CancellationTokenSource();
        IsRunning = true;
        RunStatuses.Clear();
        try
        {
            Status = $"Running {options.Sources.Count(source => source.Enabled)} configured sources...";
            var exitCode = await manualRunService.RunAsync(options, update => MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateRunStatus(update);
                Status = update.Status == "running"
                    ? $"Running {update.Source}..."
                    : string.IsNullOrWhiteSpace(update.Error)
                        ? $"{update.Source}: {update.Status}"
                        : $"{update.Source}: {update.Error}";
            }), runCancellation.Token);
            Status = exitCode == 0 ? "Run completed" : $"Run completed with exit code {exitCode}";
        }
        catch (OperationCanceledException)
        {
            Status = "Run cancelled";
        }
        finally
        {
            runCancellation.Dispose();
            runCancellation = null;
            IsRunning = false;
        }
    }

    private async Task<JobWatcherOptions> LoadSettingsAsync()
    {
        var defaultJson = await ReadDefaultSettingsAsync();
        var settingsPath = Path.Combine(FileSystem.AppDataDirectory, "settings", "jobwatcher.json");
        var loaded = await settingsStore.LoadOrCreateAsync(settingsPath, defaultJson);
        var hasGlassdoorSession = HasGlassdoorSession();
        GlassdoorSessionStatus = hasGlassdoorSession
            ? "Glassdoor session saved"
            : "Glassdoor is disabled until a session is saved";
        Sources.Clear();
        foreach (var source in loaded.Sources)
        {
            Sources.Add(SourceProfileSummary.From(source, hasGlassdoorSession));
        }

        return hasGlassdoorSession ? loaded : DisableGlassdoorWithoutSession(loaded);
    }

    private static bool HasGlassdoorSession() => File.Exists(Path.Combine(FileSystem.AppDataDirectory, "data", "secrets", "glassdoor-session.txt"));

    private static JobWatcherOptions DisableGlassdoorWithoutSession(JobWatcherOptions options)
    {
        return new JobWatcherOptions
        {
            DataDirectory = options.DataDirectory,
            RequestTimeoutSeconds = options.RequestTimeoutSeconds,
            OutputHistoryRetentionCount = options.OutputHistoryRetentionCount,
            Classification = options.Classification,
            Sources = options.Sources.Select(source => IsGlassdoor(source) ? CopyAsDisabled(source) : source).ToList()
        };
    }

    private static bool IsGlassdoor(JobSourceOptions source) => string.Equals(source.Adapter, "Glassdoor", StringComparison.OrdinalIgnoreCase);

    private static JobSourceOptions CopyAsDisabled(JobSourceOptions source)
    {
        return new JobSourceOptions
        {
            Name = source.Name,
            Adapter = source.Adapter,
            Enabled = false,
            Optional = source.Optional,
            Url = source.Url,
            MinimumExpectedVacancies = source.MinimumExpectedVacancies,
            MaximumVacancyAgeDays = source.MaximumVacancyAgeDays,
            JobKarovFilter = source.JobKarovFilter,
            DrushimFilter = source.DrushimFilter,
            AllJobsFilter = source.AllJobsFilter,
            JobSwipeCoFilter = source.JobSwipeCoFilter,
            GlassdoorFilter = source.GlassdoorFilter,
            SecretTelAvivFilter = source.SecretTelAvivFilter
        };
    }

    private void UpdateRunStatus(RunProgressUpdate update)
    {
        var existing = RunStatuses.FirstOrDefault(status => string.Equals(status.Source, update.Source, StringComparison.OrdinalIgnoreCase));
        var next = new SourceRunStatus(update.Source, update.Status, update.Error, update.Output);
        if (existing is null)
        {
            RunStatuses.Add(next);
        }
        else
        {
            RunStatuses[RunStatuses.IndexOf(existing)] = next;
        }
    }

    private static async Task<string> ReadDefaultSettingsAsync()
    {
        return await DefaultSettingsLoader.ReadAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed record SourceProfileSummary(string Name, string Adapter, bool Enabled, bool Optional, string Details, bool IsDisabledByMissingSession)
{
    public bool IsActive => Enabled && !IsDisabledByMissingSession;
    public string Status => IsDisabledByMissingSession ? "Disabled" : Enabled ? "Enabled" : "Paused";

    public static SourceProfileSummary From(JobSourceOptions source, bool hasGlassdoorSession)
    {
        var adapter = source.Adapter ?? source.Name;
        var isDisabledByMissingSession = string.Equals(adapter, "Glassdoor", StringComparison.OrdinalIgnoreCase) && !hasGlassdoorSession;
        var details = adapter switch
        {
            "JobKarov" when source.JobKarovFilter is { } filter => $"Speciality {filter.Speciality} | {filter.Roles.Count} roles | {filter.Areas.Count} areas",
            "Drushim" when source.DrushimFilter is { } filter => $"Category {filter.CategoryId} | {filter.SubcategoryIds.Count} subcategories | {filter.AreaIds.Count} areas",
            "AllJobs" when source.AllJobsFilter is { } filter => $"{filter.Positions.Count} positions | {filter.Types.Count} employment types | {filter.MaxPages} pages",
            "JobSwipeCo" when source.JobSwipeCoFilter is { } filter => $"{filter.SearchUrls.Count} searches | {filter.MaxDetailsPerSearch} detail pages per search",
            "Glassdoor" when source.GlassdoorFilter is { } filter => $"{filter.SearchUrls.Count} searches | {filter.MaxPages} pages | optional",
            "SecretTelAviv" when source.SecretTelAvivFilter is { } filter => $"Search URL | {filter.MaxDetailsPerSearch} detail pages",
            _ when !string.IsNullOrWhiteSpace(source.Url) => "Direct search URL",
            _ => "Configuration needs attention"
        };

        if (isDisabledByMissingSession)
        {
            details = $"{details} | session required";
        }

        return new SourceProfileSummary(source.Name, adapter, source.Enabled, source.Optional, details, isDisabledByMissingSession);
    }
}

public sealed record SourceRunStatus(string Source, string Status, string? Error, JobWatcher.Models.SourceOutput? Output = null)
{
    public string Detail
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Error))
            {
                return Error;
            }

            if (Output is null || Status != "success")
            {
                return Status;
            }

            var summary = Output.ClassificationSummary;
            return $"{Output.NewCount} new | {Output.PreviousCount} previous | {Output.CurrentCount} current | R {summary.Relevant}, Review {summary.Review}, Excluded {summary.Excluded}";
        }
    }
}
