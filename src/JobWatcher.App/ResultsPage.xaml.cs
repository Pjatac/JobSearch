using System.Text.Json;
using JobWatcher.Configuration;
using JobWatcher.Models;
using JobWatcher.Utilities;

namespace JobWatcher.App;

public partial class ResultsPage : ContentPage
{
    private readonly JobWatcherSettingsStore settingsStore;
    private RunOutput? output;
    private string detailsClipboardText = string.Empty;

    public ResultsPage(RunStateService runState, JobWatcherSettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
        InitializeComponent();
        ViewModePicker.ItemsSource = new[] { "Latest run", "Latest per source" };
        ViewModePicker.SelectedIndex = 0;
        ClassificationPicker.ItemsSource = new[] { "Relevant", "Review", "Excluded", "All" };
        ClassificationPicker.SelectedIndex = 0;
        SortPicker.ItemsSource = new[] { "Newest", "Title", "Company" };
        SortPicker.SelectedIndex = 0;
        DetailsDescriptionLabel.HandlerChanged += (_, _) => EnableDetailsDescriptionSelection();
        runState.RunCompleted += (_, _) => MainThread.BeginInvokeOnMainThread(LoadOutput);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadQuickFilterAsync();
        LoadOutput();
    }

    private void OnRefreshClicked(object? sender, EventArgs e) => LoadOutput();
    private void OnViewModeChanged(object? sender, EventArgs e) => LoadOutput();
    private void OnFilterChanged(object? sender, EventArgs e) => ShowJobs();
    private void OnFilterChanged(object? sender, TextChangedEventArgs e) => ShowJobs();
    private void OnFilterChanged(object? sender, ToggledEventArgs e) => ShowJobs();

    private async void OnClearHistoryClicked(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlertAsync(
            "Clear collection history",
            "Remove saved snapshots and result output? Settings, Glassdoor session, and diagnostics are kept. The next successful run will treat current vacancies as new.",
            "Clear history",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        try
        {
            var dataDirectory = Path.Combine(FileSystem.AppDataDirectory, "data");
            DeleteDirectoryIfExists(Path.Combine(dataDirectory, "snapshots"));
            DeleteDirectoryIfExists(Path.Combine(dataDirectory, "output"));
            LoadOutput();
            await DisplayAlertAsync("History cleared", "Collection history was removed. Run collection to build a fresh baseline.", "OK");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await DisplayAlertAsync("Could not clear history", ex.Message, "OK");
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string url } && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await Launcher.Default.OpenAsync(uri);
        }
    }

    private async void OnDetailsClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ResultItem item })
        {
            return;
        }

        DetailsTitleLabel.Text = $"{item.ListNumber} {item.Title}";
        DetailsMetadataLabel.Text = item.DetailsMetadata;
        DetailsDescriptionLabel.Text = item.Description;
        DetailsOpenButton.CommandParameter = item.Url;
        detailsClipboardText = string.Join(Environment.NewLine + Environment.NewLine, new[]
        {
            DetailsTitleLabel.Text,
            DetailsMetadataLabel.Text,
            item.Description
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        DetailsOverlay.IsVisible = true;
        EnableDetailsDescriptionSelection();
        await DetailsDescriptionScrollView.ScrollToAsync(0, 0, animated: false);
    }

    private void EnableDetailsDescriptionSelection()
    {
#if WINDOWS
        if (DetailsDescriptionLabel.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBlock textBlock)
        {
            textBlock.IsTextSelectionEnabled = true;
        }
#endif
    }

    private async void OnCopyDetailsClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(detailsClipboardText))
        {
            await Clipboard.Default.SetTextAsync(detailsClipboardText);
        }
    }

    private void OnCloseDetailsClicked(object? sender, EventArgs e) => DetailsOverlay.IsVisible = false;

    private async Task LoadQuickFilterAsync()
    {
        var settingsPath = Path.Combine(FileSystem.AppDataDirectory, "settings", "jobwatcher.json");
        var defaults = await DefaultSettingsLoader.ReadAsync();
        var classification = (await settingsStore.LoadOrCreateAsync(settingsPath, defaults)).Classification;
        var label = classification.SpecialInterestLabel?.Trim() ?? string.Empty;
        SpecialInterestLabel.Text = label;
        SpecialInterestFilter.IsVisible = label.Length > 0 && classification.CyberSignals.Count > 0;
        ToolTipProperties.SetText(CyberSwitch, label.Length > 0
            ? $"Show only vacancies tagged with the {label} focus during collection."
            : "Show only vacancies tagged with the optional focus during collection.");
    }

    private void LoadOutput()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "data", "output", "new-jobs.json");
        if (!File.Exists(path))
        {
            output = null;
            StatusLabel.Text = "No completed run yet";
            UpdatedLabel.Text = string.Empty;
            SummaryLabel.Text = string.Empty;
            Jobs.ItemsSource = Array.Empty<ResultItem>();
            Jobs.IsVisible = false;
            EmptyState.IsVisible = true;
            EmptyStateDetailLabel.Text = "Run the collection to see vacancies here.";
            return;
        }

        var selectedSource = SourcePicker.SelectedItem as string ?? "All";
        output = IsLatestPerSourceMode()
            ? LoadLatestPerSourceOutput(path)
            : LoadRunOutput(path);
        if (output is null)
        {
            StatusLabel.Text = "Could not read the latest output";
            return;
        }

        var modeLabel = IsLatestPerSourceMode() ? "Latest per source" : "Latest run";
        StatusLabel.Text = output.HasFailures ? $"{modeLabel} completed with failures" : $"{modeLabel} completed";
        UpdatedLabel.Text = $"Updated {output.GeneratedAtUtc.LocalDateTime:g}";
        var sources = new List<string> { "All" };
        sources.AddRange(output.Sources.Select(source => source.Source).Distinct(StringComparer.OrdinalIgnoreCase).Order());
        SourcePicker.ItemsSource = sources;
        SourcePicker.SelectedIndex = Math.Max(0, sources.FindIndex(source => string.Equals(source, selectedSource, StringComparison.OrdinalIgnoreCase)));
        SummaryLabel.Text = $"{output.GeneratedAtUtc.LocalDateTime:g} | {output.TotalNewJobs} new jobs";
        ShowJobs();
    }

    private bool IsLatestPerSourceMode()
    {
        return string.Equals(ViewModePicker.SelectedItem as string, "Latest per source", StringComparison.OrdinalIgnoreCase);
    }

    private static RunOutput? LoadRunOutput(string path)
    {
        return JsonSerializer.Deserialize<RunOutput>(File.ReadAllText(path), JsonDefaults.Options);
    }

    private static RunOutput? LoadLatestPerSourceOutput(string currentOutputPath)
    {
        var outputDirectory = Path.GetDirectoryName(currentOutputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return LoadRunOutput(currentOutputPath);
        }

        var sourceOutputs = new Dictionary<string, (DateTimeOffset GeneratedAtUtc, SourceOutput Output)>(StringComparer.OrdinalIgnoreCase);
        foreach (var runOutput in EnumerateCandidateOutputs(outputDirectory, currentOutputPath))
        {
            foreach (var sourceOutput in runOutput.Sources.Where(source => source.Status != "disabled"))
            {
                if (!sourceOutputs.TryGetValue(sourceOutput.Source, out var existing) || runOutput.GeneratedAtUtc > existing.GeneratedAtUtc)
                {
                    sourceOutputs[sourceOutput.Source] = (runOutput.GeneratedAtUtc, sourceOutput);
                }
            }
        }

        var currentOutput = LoadRunOutput(currentOutputPath);
        if (currentOutput is not null)
        {
            foreach (var sourceOutput in currentOutput.Sources.Where(source => source.Status == "disabled"))
            {
                sourceOutputs.TryAdd(sourceOutput.Source, (currentOutput.GeneratedAtUtc, sourceOutput));
            }
        }

        if (sourceOutputs.Count == 0)
        {
            return LoadRunOutput(currentOutputPath);
        }

        var sources = sourceOutputs.Values
            .OrderBy(value => value.Output.Source, StringComparer.OrdinalIgnoreCase)
            .Select(value => value.Output)
            .ToList();
        var datedSources = sourceOutputs.Values.Where(value => value.Output.Status != "disabled").ToList();
        return new RunOutput
        {
            GeneratedAtUtc = datedSources.Count > 0
                ? datedSources.Max(value => value.GeneratedAtUtc)
                : sourceOutputs.Values.Max(value => value.GeneratedAtUtc),
            HasFailures = sources.Any(source => source.Status is not "success" and not "disabled"),
            TotalNewJobs = sources.Sum(source => source.NewCount),
            Sources = sources
        };
    }

    private static IEnumerable<RunOutput> EnumerateCandidateOutputs(string outputDirectory, string currentOutputPath)
    {
        foreach (var path in EnumerateCandidateOutputPaths(outputDirectory, currentOutputPath))
        {
            RunOutput? output = null;
            try
            {
                output = LoadRunOutput(path);
            }
            catch (JsonException)
            {
            }
            catch (IOException)
            {
            }

            if (output is not null)
            {
                yield return output;
            }
        }
    }

    private static IEnumerable<string> EnumerateCandidateOutputPaths(string outputDirectory, string currentOutputPath)
    {
        yield return currentOutputPath;

        var latestSourcesDirectory = Path.Combine(outputDirectory, "latest-sources");
        if (Directory.Exists(latestSourcesDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(latestSourcesDirectory, "*.json"))
            {
                yield return path;
            }
        }

        var historyDirectory = Path.Combine(outputDirectory, "history");
        if (Directory.Exists(historyDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(historyDirectory, "*.json"))
            {
                yield return path;
            }
        }
    }

    private void ShowJobs()
    {
        if (output is null)
        {
            return;
        }

        var selected = ClassificationPicker.SelectedItem as string ?? "Relevant";
        var source = SourcePicker.SelectedItem as string ?? "All";
        var location = LocationEntry.Text?.Trim() ?? string.Empty;
        var search = SearchBar.Text?.Trim() ?? string.Empty;
        var jobs = (output.Sources ?? []).SelectMany(source => source.NewJobs ?? [])
            .Where(job => selected == "All" || string.Equals(job.Classification?.Classification, selected.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
            .Where(job => source == "All" || string.Equals(job.Source, source, StringComparison.OrdinalIgnoreCase))
            .Where(job => string.IsNullOrWhiteSpace(location) || job.Location?.Contains(location, StringComparison.OrdinalIgnoreCase) == true)
            .Where(job => string.IsNullOrWhiteSpace(search) || job.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || job.Company?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
            .Where(job => !CyberSwitch.IsToggled || job.Classification?.Flags.Cyber == true)
            .Where(job => !FarCommuteSwitch.IsToggled || job.Classification?.Flags.FarCommute == true)
            .ToList();
        jobs = (SortPicker.SelectedItem as string) switch
        {
            "Title" => jobs.OrderBy(job => job.Title, StringComparer.OrdinalIgnoreCase).ToList(),
            "Company" => jobs.OrderBy(job => job.Company, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => jobs.OrderByDescending(job => job.DatePosted).ThenBy(job => job.Title, StringComparer.OrdinalIgnoreCase).ToList()
        };
        var items = jobs.Select((job, index) => new ResultItem(job, index + 1)).ToList();
        Jobs.ItemsSource = items;
        SummaryLabel.Text = $"{items.Count} shown · {output.TotalNewJobs} new";
        Jobs.IsVisible = items.Count > 0;
        EmptyState.IsVisible = items.Count == 0;
        var selectedSourceOutput = source == "All"
            ? null
            : (output.Sources ?? []).FirstOrDefault(item => string.Equals(item.Source, source, StringComparison.OrdinalIgnoreCase));
        EmptyStateDetailLabel.Text = selectedSourceOutput switch
        {
            { Status: "disabled" } => "This paused profile has no saved latest-per-source output yet. Enable it and run collection once.",
            { NewCount: 0, CurrentCount: > 0 } => $"This source has {selectedSourceOutput.CurrentCount} current vacancies, but no new vacancies in this view.",
            _ => "Adjust the filters or choose another classification."
        };
    }

    private sealed record ResultItem
    {
        public ResultItem(JobVacancy job, int index)
        {
            Index = index;
            Title = job.Title;
            Company = string.IsNullOrWhiteSpace(job.Company) ? "Company not provided" : job.Company;
            Classification = job.Classification?.Classification ?? "review";
            SourceLabel = job.Source.Split('-', 2)[0];
            Metadata = string.Join(" | ", new[]
            {
                FormatCompanyRating(job.CompanyRating),
                job.Location,
                job.DatePosted is { } datePosted ? $"Posted {datePosted:d}" : null,
                job.EmploymentTypes.Count > 0 ? string.Join(", ", job.EmploymentTypes) : null
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
            ReasonTags = (job.Classification?.Reasons ?? []).Select(ToReasonTag).ToList();
            Url = job.Url;
            Description = job.Description?.Trim() ?? string.Empty;
            DetailsMetadata = string.Join(" | ", new[]
            {
                Company,
                SourceLabel,
                Metadata
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        public int Index { get; }
        public string ListNumber => $"#{Index}";
        public string Title { get; }
        public string Company { get; }
        public string Classification { get; }
        public string SourceLabel { get; }
        public string Metadata { get; }
        public IReadOnlyList<string> ReasonTags { get; }
        public string Url { get; }
        public string Description { get; }
        public string DetailsMetadata { get; }
        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
        public string ClassificationBackground => Classification switch
        {
            "relevant" => "#E6F4F0",
            "excluded" => "#FDE8E7",
            _ => "#FFF3D6"
        };

        public string ClassificationForeground => Classification switch
        {
            "relevant" => "#147D72",
            "excluded" => "#A61B1B",
            _ => "#8A5A00"
        };

        private static string? FormatCompanyRating(double? rating)
        {
            return rating is > 0
                ? $"Rating {rating.Value:0.#}/5"
                : null;
        }

        private static string ToReasonTag(string reason)
        {
            var separator = reason.IndexOf(':');
            var kind = separator < 0 ? reason : reason[..separator];
            var value = separator < 0 ? string.Empty : reason[(separator + 1)..];
            return kind switch
            {
                "include-signal" => $"Matches {value}",
                "role-mismatch" => $"Different role: {value}",
                "other-language" => $"Other specialization: {value}",
                "junior" => $"Junior: {value}",
                "no-include-signal" => "No target match",
                "glassdoor-short-description" => "Short description",
                _ => reason
            };
        }
    }
}
