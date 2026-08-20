using System.Text.Json;
using JobWatcher.Configuration;
using JobWatcher.Models;
using JobWatcher.Utilities;

namespace JobWatcher.App;

public partial class ResultsPage : ContentPage
{
    private readonly JobWatcherSettingsStore settingsStore;
    private RunOutput? output;

    public ResultsPage(RunStateService runState, JobWatcherSettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
        InitializeComponent();
        ClassificationPicker.ItemsSource = new[] { "Relevant", "Review", "Excluded", "All" };
        ClassificationPicker.SelectedIndex = 0;
        SortPicker.ItemsSource = new[] { "Newest", "Title", "Company" };
        SortPicker.SelectedIndex = 0;
        runState.RunCompleted += (_, _) => MainThread.BeginInvokeOnMainThread(LoadOutput);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadQuickFilterAsync();
        LoadOutput();
    }

    private void OnRefreshClicked(object? sender, EventArgs e) => LoadOutput();
    private void OnFilterChanged(object? sender, EventArgs e) => ShowJobs();
    private void OnFilterChanged(object? sender, TextChangedEventArgs e) => ShowJobs();
    private void OnFilterChanged(object? sender, ToggledEventArgs e) => ShowJobs();

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string url } && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await Launcher.Default.OpenAsync(uri);
        }
    }

    private void OnDetailsClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ResultItem item })
        {
            return;
        }

        DetailsTitleLabel.Text = item.Title;
        DetailsMetadataLabel.Text = item.DetailsMetadata;
        DetailsDescriptionLabel.Text = item.Description;
        DetailsOpenButton.CommandParameter = item.Url;
        DetailsOverlay.IsVisible = true;
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

        output = JsonSerializer.Deserialize<RunOutput>(File.ReadAllText(path), JsonDefaults.Options);
        if (output is null)
        {
            StatusLabel.Text = "Could not read the latest output";
            return;
        }

        StatusLabel.Text = output.HasFailures ? "Latest run completed with failures" : "Latest run completed";
        UpdatedLabel.Text = $"Updated {output.GeneratedAtUtc.LocalDateTime:g}";
        var sources = new List<string> { "All" };
        sources.AddRange(output.Sources.Select(source => source.Source).Distinct(StringComparer.OrdinalIgnoreCase).Order());
        SourcePicker.ItemsSource = sources;
        SourcePicker.SelectedIndex = 0;
        SummaryLabel.Text = $"{output.GeneratedAtUtc.LocalDateTime:g} | {output.TotalNewJobs} new jobs";
        ShowJobs();
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
        var jobs = output.Sources.SelectMany(source => source.NewJobs)
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
        var items = jobs.Select(job => new ResultItem(job)).ToList();
        Jobs.ItemsSource = items;
        SummaryLabel.Text = $"{items.Count} shown · {output.TotalNewJobs} new";
        Jobs.IsVisible = items.Count > 0;
        EmptyState.IsVisible = items.Count == 0;
        EmptyStateDetailLabel.Text = "Adjust the filters or choose another classification.";
    }

    private sealed record ResultItem
    {
        public ResultItem(JobVacancy job)
        {
            Title = job.Title;
            Company = string.IsNullOrWhiteSpace(job.Company) ? "Company not provided" : job.Company;
            Classification = job.Classification?.Classification ?? "review";
            SourceLabel = job.Source.Split('-', 2)[0];
            Metadata = string.Join(" | ", new[]
            {
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
