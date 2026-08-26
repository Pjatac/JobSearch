using JobWatcher.Configuration;
using JobWatcher.Sources.AllJobs;
using JobWatcher.Sources.Drushim;
using JobWatcher.Sources.DevJobs;
using JobWatcher.Sources.JobKarov;
using JobWatcher.Sources.SecretTelAviv;

namespace JobWatcher.App;

public partial class SourceProfilesPage : ContentPage
{
    private readonly JobWatcherSettingsStore settingsStore;
    private readonly List<JobSourceOptions> sources = [];
    private string? settingsPath;
    private string? defaultSettingsJson;
    private int selectedIndex = -1;
    private Entry? nameEntry;
    private Switch? enabledSwitch;
    private Switch? optionalSwitch;
    private Entry? minimumExpectedEntry;
    private Entry? maximumVacancyAgeDaysEntry;
    private Entry? directUrlEntry;
    private Entry? jobKarovBaseUrlEntry;
    private Entry? jobKarovSpecialityEntry;
    private Entry? jobKarovRolesEntry;
    private Entry? jobKarovAreasEntry;
    private Entry? jobKarovSizeEntry;
    private Entry? drushimBaseUrlEntry;
    private Entry? drushimCategoryEntry;
    private Entry? drushimSubcategoryEntry;
    private Entry? drushimSubcategoriesEntry;
    private Entry? drushimAreasEntry;
    private Entry? drushimScopesEntry;
    private Entry? drushimExperienceRangeEntry;
    private Entry? drushimGeoLexEntry;
    private Switch? drushimIncludeAreaAroundSwitch;
    private Entry? drushimExperienceEntry;
    private Entry? drushimRangeEntry;
    private Entry? allJobsBaseUrlEntry;
    private Entry? allJobsPositionEntry;
    private Entry? allJobsPositionsEntry;
    private Entry? allJobsTypesEntry;
    private Entry? allJobsSourceEntry;
    private Entry? allJobsDurationEntry;
    private Entry? allJobsExcludeEntry;
    private Entry? allJobsRegionEntry;
    private Entry? allJobsMaxPagesEntry;
    private Entry? jobSwipeBaseUrlEntry;
    private Microsoft.Maui.Controls.Editor? jobSwipeSearchUrlsEditor;
    private Entry? jobSwipeMaxDetailsEntry;
    private Entry? glassdoorBaseUrlEntry;
    private Microsoft.Maui.Controls.Editor? glassdoorSearchUrlsEditor;
    private Entry? glassdoorDelayEntry;
    private Entry? glassdoorMaxPagesEntry;
    private Entry? glassdoorJobsPerPageEntry;
    private Entry? secretTelAvivBaseUrlEntry;
    private Entry? secretTelAvivSearchUrlEntry;
    private Entry? secretTelAvivMaxDetailsEntry;
    private Entry? devJobsBaseUrlEntry;
    private Entry? devJobsSearchUrlEntry;
    private Entry? devJobsMaxPagesEntry;
    private Entry? devJobsMaxDetailsEntry;
    private Label? generatedUrlLabel;

    public SourceProfilesPage(JobWatcherSettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (settingsPath is not null)
        {
            return;
        }

        settingsPath = Path.Combine(FileSystem.AppDataDirectory, "settings", "jobwatcher.json");
        defaultSettingsJson = await DefaultSettingsLoader.ReadAsync();
        var options = await settingsStore.LoadOrCreateAsync(settingsPath, defaultSettingsJson);
        sources.AddRange(options.Sources);
        RefreshProfiles();
    }

    private void OnProfileSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ProfileListItem selected)
        {
            return;
        }

        selectedIndex = sources.FindIndex(source => string.Equals(source.Name, selected.Name, StringComparison.OrdinalIgnoreCase));
        if (selectedIndex < 0)
        {
            return;
        }

        var source = sources[selectedIndex];
        Editor.Children.Clear();
        Editor.Children.Add(new Label { Text = source.Name, FontSize = 22, FontAttributes = FontAttributes.Bold });
        Editor.Children.Add(new Label { Text = source.Adapter ?? source.Name, TextColor = Colors.Gray });

        nameEntry = AddEntry("Profile name", source.Name);
        enabledSwitch = AddSwitch("Enabled", source.Enabled);
        optionalSwitch = AddSwitch("Optional source", source.Optional);
        minimumExpectedEntry = AddEntry("Minimum expected vacancies", source.MinimumExpectedVacancies.ToString());
        maximumVacancyAgeDaysEntry = AddEntry("Maximum vacancy age days", source.MaximumVacancyAgeDays?.ToString() ?? string.Empty);
        directUrlEntry = AddEntry("Direct search URL override", source.Url ?? string.Empty);
        AddJobKarovFields(source);
        AddDrushimFields(source);
        AddAllJobsFields(source);
        AddJobSwipeFields(source);
        AddGlassdoorFields(source);
        AddSecretTelAvivFields(source);
        AddDevJobsFields(source);
        AddGeneratedUrlPreview(source);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (settingsPath is null || defaultSettingsJson is null)
        {
            return;
        }

        if (!await TryApplySelectedDraftAsync())
        {
            return;
        }

        await settingsStore.UpdateAsync(settingsPath, defaultSettingsJson, current => current with { Sources = sources.ToList() });
        RefreshProfiles(sources[selectedIndex].Name);
        await DisplayAlertAsync("Draft saved", "Search profiles were saved. Existing collection history was preserved.", "OK");
    }

    private async void OnNewClicked(object? sender, EventArgs e)
    {
        var adapter = await DisplayActionSheetAsync("New search profile", "Cancel", null, "JobKarov", "Drushim", "AllJobs", "JobSwipeCo", "Glassdoor", "SecretTelAviv", "DevJobs");
        if (string.IsNullOrWhiteSpace(adapter) || adapter == "Cancel")
        {
            return;
        }

        var profile = CreateNewProfile(adapter);
        sources.Add(profile);
        RefreshProfiles(profile.Name);
    }

    private void OnDuplicateClicked(object? sender, EventArgs e)
    {
        if (selectedIndex < 0)
        {
            return;
        }

        var source = sources[selectedIndex];
        var duplicate = CopyProfile(source, CreateUniqueName($"{source.Name} copy"));
        sources.Add(duplicate);
        RefreshProfiles(duplicate.Name);
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (selectedIndex < 0)
        {
            return;
        }

        var source = sources[selectedIndex];
        if (!await DisplayAlertAsync("Delete profile", $"Delete '{source.Name}'? This does not delete collection history.", "Delete", "Cancel"))
        {
            return;
        }

        sources.RemoveAt(selectedIndex);
        selectedIndex = -1;
        Editor.Children.Clear();
        Editor.Children.Add(new Label { Text = "Select a profile", FontSize = 20, TextColor = Colors.Gray });
        RefreshProfiles();
    }

    private async void OnClearAllClicked(object? sender, EventArgs e)
    {
        if (settingsPath is null || defaultSettingsJson is null || sources.Count == 0)
        {
            return;
        }

        var confirmed = await DisplayAlertAsync(
            "Clear all search profiles",
            "This removes every search profile from your saved settings. Snapshots, JSON output history, and the Glassdoor session are kept, so earlier results may not be comparable to a later configuration.",
            "Clear all",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        sources.Clear();
        selectedIndex = -1;
        Editor.Children.Clear();
        Editor.Children.Add(new Label { Text = "Create a profile to begin", FontSize = 20, TextColor = Colors.Gray });
        RefreshProfiles();
        await settingsStore.UpdateAsync(settingsPath, defaultSettingsJson, current => current with { Sources = [] });
        await DisplayAlertAsync("Profiles cleared", "Your search profile list is now empty. Existing collection history was preserved.", "OK");
    }

    private void RefreshProfiles(string? selectName = null)
    {
        var items = sources.Select(source => new ProfileListItem(source.Name, source.Adapter ?? source.Name)).ToList();
        Profiles.ItemsSource = items;
        if (selectName is not null)
        {
            Profiles.SelectedItem = items.FirstOrDefault(item => item.Name == selectName);
        }
    }

    private JobSourceOptions CreateNewProfile(string adapter)
    {
        var name = CreateUniqueName($"New {adapter} profile");
        return adapter switch
        {
            "JobKarov" => new JobSourceOptions { Name = name, Adapter = adapter, JobKarovFilter = new JobKarovFilterOptions { Speciality = string.Empty } },
            "Drushim" => new JobSourceOptions { Name = name, Adapter = adapter, DrushimFilter = new DrushimFilterOptions { CategoryId = 0 } },
            "AllJobs" => new JobSourceOptions { Name = name, Adapter = adapter, AllJobsFilter = new AllJobsFilterOptions() },
            "JobSwipeCo" => new JobSourceOptions { Name = name, Adapter = adapter, JobSwipeCoFilter = new JobSwipeCoFilterOptions() },
            "Glassdoor" => new JobSourceOptions { Name = name, Adapter = adapter, Optional = true, GlassdoorFilter = new GlassdoorFilterOptions() },
            "SecretTelAviv" => new JobSourceOptions { Name = name, Adapter = adapter, SecretTelAvivFilter = new SecretTelAvivFilterOptions() },
            "DevJobs" => new JobSourceOptions { Name = name, Adapter = adapter, DevJobsFilter = new DevJobsFilterOptions() },
            _ => throw new ArgumentOutOfRangeException(nameof(adapter))
        };
    }

    private string CreateUniqueName(string proposedName)
    {
        var candidate = proposedName;
        var suffix = 2;
        while (sources.Any(source => string.Equals(source.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{proposedName} {suffix++}";
        }

        return candidate;
    }

    private static JobSourceOptions CopyProfile(JobSourceOptions source, string name)
    {
        return new JobSourceOptions
        {
            Name = name,
            Adapter = source.Adapter,
            Enabled = source.Enabled,
            Optional = source.Optional,
            Url = source.Url,
            MinimumExpectedVacancies = source.MinimumExpectedVacancies,
            MaximumVacancyAgeDays = source.MaximumVacancyAgeDays,
            JobKarovFilter = source.JobKarovFilter,
            DrushimFilter = source.DrushimFilter,
            AllJobsFilter = source.AllJobsFilter,
            JobSwipeCoFilter = source.JobSwipeCoFilter,
            GlassdoorFilter = source.GlassdoorFilter,
            SecretTelAvivFilter = source.SecretTelAvivFilter,
            DevJobsFilter = source.DevJobsFilter
        };
    }

    private Entry AddEntry(string label, string value)
    {
        Editor.Children.Add(new Label { Text = label, FontAttributes = FontAttributes.Bold });
        var entry = new Entry { Text = value };
        entry.TextChanged += OnEditorValueChanged;
        Editor.Children.Add(entry);
        return entry;
    }

    private Switch AddSwitch(string label, bool value)
    {
        var toggle = new Switch { IsToggled = value };
        toggle.Toggled += OnEditorValueChanged;
        Editor.Children.Add(new HorizontalStackLayout
        {
            Spacing = 12,
            Children = { new Label { Text = label, VerticalOptions = LayoutOptions.Center }, toggle }
        });
        return toggle;
    }

    private void AddGeneratedUrlPreview(JobSourceOptions source)
    {
        if (source.Adapter is not ("JobKarov" or "Drushim" or "AllJobs"))
        {
            return;
        }

        Editor.Children.Add(new BoxView { HeightRequest = 1, Margin = new Thickness(0, 8), BackgroundColor = Colors.LightGray });
        Editor.Children.Add(new Label { Text = "Generated request URL", FontSize = 18, FontAttributes = FontAttributes.Bold });
        generatedUrlLabel = new Label { TextColor = Colors.Gray, LineBreakMode = LineBreakMode.WordWrap };
        Editor.Children.Add(generatedUrlLabel);
        UpdateGeneratedUrlPreview();
    }

    private void OnEditorValueChanged(object? sender, EventArgs e) => UpdateGeneratedUrlPreview();
    private void OnEditorValueChanged(object? sender, TextChangedEventArgs e) => UpdateGeneratedUrlPreview();

    private void UpdateGeneratedUrlPreview()
    {
        if (generatedUrlLabel is null || selectedIndex < 0)
        {
            return;
        }

        try
        {
            var source = sources[selectedIndex];
            var preview = source.Adapter switch
            {
                "JobKarov" => JobKarovUrlBuilder.Build(BuildJobKarovPreview(source)),
                "Drushim" => DrushimUrlBuilder.Build(BuildDrushimPreview(source)),
                "AllJobs" => AllJobsUrlBuilder.Build(BuildAllJobsPreview(source), 1),
                _ => string.Empty
            };
            generatedUrlLabel.Text = preview;
        }
        catch (Exception)
        {
            generatedUrlLabel.Text = "Preview unavailable until the URL fields form a valid URL.";
        }
    }

    private JobSourceOptions BuildJobKarovPreview(JobSourceOptions source)
    {
        _ = TryParseInt(jobKarovSizeEntry?.Text, out var size);
        return new JobSourceOptions
        {
            Name = source.Name,
            Adapter = source.Adapter,
            Url = directUrlEntry?.Text?.Trim(),
            JobKarovFilter = new JobKarovFilterOptions
            {
                BaseUrl = jobKarovBaseUrlEntry?.Text?.Trim() ?? string.Empty,
                Speciality = jobKarovSpecialityEntry?.Text?.Trim() ?? string.Empty,
                Roles = SplitIds(jobKarovRolesEntry?.Text),
                Areas = SplitIds(jobKarovAreasEntry?.Text),
                Size = size
            }
        };
    }

    private JobSourceOptions BuildDrushimPreview(JobSourceOptions source)
    {
        _ = TryParseInt(drushimCategoryEntry?.Text, out var categoryId);
        _ = TryParseOptionalInt(drushimSubcategoryEntry?.Text, out var subcategoryId);
        _ = TryParseIntList(drushimSubcategoriesEntry?.Text, out var subcategoryIds);
        _ = TryParseIntList(drushimAreasEntry?.Text, out var areaIds);
        _ = TryParseIntList(drushimScopesEntry?.Text, out var scopes);
        _ = TryParseOptionalInt(drushimGeoLexEntry?.Text, out var geoLexId);
        _ = TryParseOptionalInt(drushimExperienceEntry?.Text, out var experience);
        _ = TryParseOptionalInt(drushimRangeEntry?.Text, out var range);
        return new JobSourceOptions
        {
            Name = source.Name,
            Adapter = source.Adapter,
            Url = directUrlEntry?.Text?.Trim(),
            DrushimFilter = new DrushimFilterOptions
            {
                BaseUrl = drushimBaseUrlEntry?.Text?.Trim() ?? string.Empty,
                CategoryId = categoryId,
                SubcategoryId = subcategoryId,
                SubcategoryIds = subcategoryIds,
                AreaIds = areaIds,
                Scopes = scopes,
                ExperienceRange = drushimExperienceRangeEntry?.Text?.Trim(),
                GeoLexId = geoLexId,
                IncludeAreaAround = drushimIncludeAreaAroundSwitch?.IsToggled ?? false,
                Experience = experience,
                Range = range
            }
        };
    }

    private JobSourceOptions BuildAllJobsPreview(JobSourceOptions source)
    {
        _ = TryParseInt(allJobsPositionEntry?.Text, out var position);
        _ = TryParseIntList(allJobsPositionsEntry?.Text, out var positions);
        _ = TryParseIntList(allJobsTypesEntry?.Text, out var types);
        _ = TryParseOptionalInt(allJobsSourceEntry?.Text, out var sourceId);
        _ = TryParseOptionalInt(allJobsDurationEntry?.Text, out var duration);
        return new JobSourceOptions
        {
            Name = source.Name,
            Adapter = source.Adapter,
            Url = directUrlEntry?.Text?.Trim(),
            AllJobsFilter = new AllJobsFilterOptions
            {
                BaseUrl = allJobsBaseUrlEntry?.Text?.Trim() ?? string.Empty,
                Position = position,
                Positions = positions,
                Types = types,
                Source = sourceId,
                Duration = duration,
                Exclude = allJobsExcludeEntry?.Text?.Trim(),
                Region = allJobsRegionEntry?.Text?.Trim()
            }
        };
    }

    private async Task<bool> TryApplySelectedDraftAsync()
    {
        if (selectedIndex < 0 || nameEntry is null || enabledSwitch is null || optionalSwitch is null || minimumExpectedEntry is null || maximumVacancyAgeDaysEntry is null || directUrlEntry is null)
        {
            return true;
        }

        if (!int.TryParse(minimumExpectedEntry.Text, out var minimumExpected) || minimumExpected < 0)
        {
            await DisplayAlertAsync("Check the profile", "Minimum expected vacancies must be zero or greater.", "OK");
            return false;
        }

        if (!TryParseOptionalInt(maximumVacancyAgeDaysEntry.Text, out var maximumVacancyAgeDays) || maximumVacancyAgeDays is <= 0)
        {
            await DisplayAlertAsync("Check the profile", "Maximum vacancy age days must be a positive whole number when specified.", "OK");
            return false;
        }

        var name = nameEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlertAsync("Check the profile", "Profile name is required.", "OK");
            return false;
        }

        if (sources.Where((_, index) => index != selectedIndex).Any(source => string.Equals(source.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            await DisplayAlertAsync("Check the profile", "Profile names must be unique.", "OK");
            return false;
        }

        var previous = sources[selectedIndex];
        var jobKarovFilter = previous.JobKarovFilter;
        if (string.Equals(previous.Adapter, "JobKarov", StringComparison.OrdinalIgnoreCase) && jobKarovBaseUrlEntry is not null)
        {
            if (!int.TryParse(jobKarovSizeEntry?.Text, out var size) || size < 0)
            {
                await DisplayAlertAsync("Check the profile", "JobKarov company size must be zero or greater.", "OK");
                return false;
            }

            jobKarovFilter = new JobKarovFilterOptions
            {
                BaseUrl = jobKarovBaseUrlEntry.Text?.Trim() ?? string.Empty,
                Speciality = jobKarovSpecialityEntry?.Text?.Trim() ?? string.Empty,
                Roles = SplitIds(jobKarovRolesEntry?.Text),
                Areas = SplitIds(jobKarovAreasEntry?.Text),
                Size = size
            };
        }

        var drushimFilter = previous.DrushimFilter;
        if (string.Equals(previous.Adapter, "Drushim", StringComparison.OrdinalIgnoreCase) && drushimBaseUrlEntry is not null)
        {
            if (!TryParseInt(drushimCategoryEntry?.Text, out var categoryId) ||
                !TryParseOptionalInt(drushimSubcategoryEntry?.Text, out var subcategoryId) ||
                !TryParseIntList(drushimSubcategoriesEntry?.Text, out var subcategoryIds) ||
                !TryParseIntList(drushimAreasEntry?.Text, out var areaIds) ||
                !TryParseIntList(drushimScopesEntry?.Text, out var scopes) ||
                !TryParseOptionalInt(drushimGeoLexEntry?.Text, out var geoLexId) ||
                !TryParseOptionalInt(drushimExperienceEntry?.Text, out var experience) ||
                !TryParseOptionalInt(drushimRangeEntry?.Text, out var range))
            {
                await DisplayAlertAsync("Check the profile", "Drushim numeric fields must contain whole numbers separated by commas.", "OK");
                return false;
            }

            drushimFilter = new DrushimFilterOptions
            {
                BaseUrl = drushimBaseUrlEntry.Text?.Trim() ?? string.Empty,
                CategoryId = categoryId,
                SubcategoryId = subcategoryId,
                SubcategoryIds = subcategoryIds,
                AreaIds = areaIds,
                Scopes = scopes,
                ExperienceRange = drushimExperienceRangeEntry?.Text?.Trim(),
                GeoLexId = geoLexId,
                IncludeAreaAround = drushimIncludeAreaAroundSwitch?.IsToggled ?? false,
                Experience = experience,
                Range = range
            };
        }

        var allJobsFilter = previous.AllJobsFilter;
        if (string.Equals(previous.Adapter, "AllJobs", StringComparison.OrdinalIgnoreCase) && allJobsBaseUrlEntry is not null)
        {
            if (!TryParseInt(allJobsPositionEntry?.Text, out var position) ||
                !TryParseIntList(allJobsPositionsEntry?.Text, out var positions) ||
                !TryParseIntList(allJobsTypesEntry?.Text, out var types) ||
                !TryParseOptionalInt(allJobsSourceEntry?.Text, out var sourceId) ||
                !TryParseOptionalInt(allJobsDurationEntry?.Text, out var duration) ||
                !TryParseInt(allJobsMaxPagesEntry?.Text, out var maxPages) || maxPages < 0)
            {
                await DisplayAlertAsync("Check the profile", "AllJobs numeric fields must contain whole numbers separated by commas.", "OK");
                return false;
            }

            allJobsFilter = new AllJobsFilterOptions
            {
                BaseUrl = allJobsBaseUrlEntry.Text?.Trim() ?? string.Empty,
                Position = position,
                Positions = positions,
                Types = types,
                Source = sourceId,
                Duration = duration,
                Exclude = allJobsExcludeEntry?.Text?.Trim(),
                Region = allJobsRegionEntry?.Text?.Trim(),
                MaxPages = maxPages
            };
        }

        var jobSwipeFilter = previous.JobSwipeCoFilter;
        if (string.Equals(previous.Adapter, "JobSwipeCo", StringComparison.OrdinalIgnoreCase) && jobSwipeBaseUrlEntry is not null)
        {
            if (!TryParseInt(jobSwipeMaxDetailsEntry?.Text, out var maxDetails) || maxDetails < 0)
            {
                await DisplayAlertAsync("Check the profile", "JobSwipe.co maximum details must be zero or greater.", "OK");
                return false;
            }

            jobSwipeFilter = new JobSwipeCoFilterOptions
            {
                BaseUrl = jobSwipeBaseUrlEntry.Text?.Trim() ?? string.Empty,
                SearchUrls = SplitLines(jobSwipeSearchUrlsEditor?.Text),
                MaxDetailsPerSearch = maxDetails
            };
        }

        var glassdoorFilter = previous.GlassdoorFilter;
        if (string.Equals(previous.Adapter, "Glassdoor", StringComparison.OrdinalIgnoreCase) && glassdoorBaseUrlEntry is not null)
        {
            if (!TryParseDouble(glassdoorDelayEntry?.Text, out var delay) || delay < 0 ||
                !TryParseInt(glassdoorMaxPagesEntry?.Text, out var maxPages) || maxPages < 0 ||
                !TryParseInt(glassdoorJobsPerPageEntry?.Text, out var jobsPerPage) || jobsPerPage < 0)
            {
                await DisplayAlertAsync("Check the profile", "Glassdoor delay and page limits must be zero or greater.", "OK");
                return false;
            }

            glassdoorFilter = new GlassdoorFilterOptions
            {
                BaseUrl = glassdoorBaseUrlEntry.Text?.Trim() ?? string.Empty,
                SearchUrls = SplitLines(glassdoorSearchUrlsEditor?.Text),
                RequestDelaySeconds = delay,
                MaxPages = maxPages,
                JobsPerPage = jobsPerPage
            };
        }

        var secretTelAvivFilter = previous.SecretTelAvivFilter;
        if (string.Equals(previous.Adapter, "SecretTelAviv", StringComparison.OrdinalIgnoreCase) &&
            secretTelAvivBaseUrlEntry is not null &&
            secretTelAvivSearchUrlEntry is not null)
        {
            var baseUrl = secretTelAvivBaseUrlEntry.Text?.Trim() ?? string.Empty;
            var searchUrl = secretTelAvivSearchUrlEntry.Text?.Trim() ?? string.Empty;
            var candidate = new SecretTelAvivFilterOptions { BaseUrl = baseUrl, SearchUrl = searchUrl };
            try
            {
                var resolvedUrl = SecretTelAvivUrlBuilder.Build(candidate);
                if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsedBaseUrl) ||
                    !string.Equals(parsedBaseUrl.Host, "jobs.secrettelaviv.com", StringComparison.OrdinalIgnoreCase) ||
                    !Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var parsedSearchUrl) ||
                    !string.Equals(parsedSearchUrl.Host, "jobs.secrettelaviv.com", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException();
                }
            }
            catch (InvalidOperationException)
            {
                await DisplayAlertAsync("Check the profile", "Secret Tel Aviv needs a jobs.secrettelaviv.com base URL and a search path or URL on the same host.", "OK");
                return false;
            }

            if (!TryParseInt(secretTelAvivMaxDetailsEntry?.Text, out var maxDetails) || maxDetails < 0)
            {
                await DisplayAlertAsync("Check the profile", "Secret Tel Aviv maximum detail pages must be zero or greater.", "OK");
                return false;
            }

            secretTelAvivFilter = new SecretTelAvivFilterOptions { BaseUrl = baseUrl, SearchUrl = searchUrl, MaxDetailsPerSearch = maxDetails };
        }

        var devJobsFilter = previous.DevJobsFilter;
        if (string.Equals(previous.Adapter, "DevJobs", StringComparison.OrdinalIgnoreCase) &&
            devJobsBaseUrlEntry is not null && devJobsSearchUrlEntry is not null)
        {
            var baseUrl = devJobsBaseUrlEntry.Text?.Trim() ?? string.Empty;
            var searchUrl = devJobsSearchUrlEntry.Text?.Trim() ?? string.Empty;
            var candidate = new DevJobsFilterOptions { BaseUrl = baseUrl, SearchUrl = searchUrl };
            try
            {
                var resolvedUrl = DevJobsUrlBuilder.Build(candidate, 1);
                if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsedBaseUrl) ||
                    !string.Equals(parsedBaseUrl.Host, "devjobs.co.il", StringComparison.OrdinalIgnoreCase) ||
                    !Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var parsedSearchUrl) ||
                    !string.Equals(parsedSearchUrl.Host, "devjobs.co.il", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException();
                }
            }
            catch (InvalidOperationException)
            {
                await DisplayAlertAsync("Check the profile", "DevJobs needs a devjobs.co.il base URL and a search path or URL on the same host.", "OK");
                return false;
            }

            if (!TryParseInt(devJobsMaxPagesEntry?.Text, out var maxPages) || maxPages <= 0 ||
                !TryParseInt(devJobsMaxDetailsEntry?.Text, out var maxDetails) || maxDetails < 0)
            {
                await DisplayAlertAsync("Check the profile", "DevJobs maximum pages must be positive and maximum detail pages must be zero or greater.", "OK");
                return false;
            }

            devJobsFilter = new DevJobsFilterOptions
            {
                BaseUrl = baseUrl,
                SearchUrl = searchUrl,
                MaxPages = maxPages,
                MaxDetailsPerPage = maxDetails
            };
        }

        sources[selectedIndex] = new JobSourceOptions
        {
            Name = name,
            Adapter = previous.Adapter,
            Enabled = enabledSwitch.IsToggled,
            Optional = optionalSwitch.IsToggled,
            Url = directUrlEntry.Text?.Trim(),
            MinimumExpectedVacancies = minimumExpected,
            MaximumVacancyAgeDays = maximumVacancyAgeDays,
            JobKarovFilter = jobKarovFilter,
            DrushimFilter = drushimFilter,
            AllJobsFilter = allJobsFilter,
            JobSwipeCoFilter = jobSwipeFilter,
            GlassdoorFilter = glassdoorFilter,
            SecretTelAvivFilter = secretTelAvivFilter,
            DevJobsFilter = devJobsFilter
        };
        return true;
    }

    private void AddJobKarovFields(JobSourceOptions source)
    {
        if (!string.Equals(source.Adapter, "JobKarov", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var filter = source.JobKarovFilter ?? new JobKarovFilterOptions { Speciality = string.Empty };
        Editor.Children.Add(new BoxView { HeightRequest = 1, Margin = new Thickness(0, 8), BackgroundColor = Colors.LightGray });
        Editor.Children.Add(new Label { Text = "JobKarov search", FontSize = 18, FontAttributes = FontAttributes.Bold });
        jobKarovBaseUrlEntry = AddEntry("Base URL", filter.BaseUrl);
        jobKarovSpecialityEntry = AddEntry("Speciality ID", filter.Speciality);
        AddKnownValuesHint("2119 - Software; 3921 - Cybersecurity; 1857 - Information Systems.");
        jobKarovRolesEntry = AddEntry("Role IDs", string.Join(", ", filter.Roles));
        AddKnownValuesHint("3893 - Backend; 2163 - .NET; 2155 - C#; 3131 - Software Engineer; 2177 - Senior Programmer.");
        jobKarovAreasEntry = AddEntry("Area IDs", string.Join(", ", filter.Areas));
        AddKnownValuesHint("50 - Hasharon; 70 - Center.");
        jobKarovSizeEntry = AddEntry("Company size", filter.Size.ToString());
    }

    private void AddDrushimFields(JobSourceOptions source)
    {
        if (!string.Equals(source.Adapter, "Drushim", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var filter = source.DrushimFilter ?? new DrushimFilterOptions { CategoryId = 0 };
        Editor.Children.Add(new BoxView { HeightRequest = 1, Margin = new Thickness(0, 8), BackgroundColor = Colors.LightGray });
        Editor.Children.Add(new Label { Text = "Drushim search", FontSize = 18, FontAttributes = FontAttributes.Bold });
        drushimBaseUrlEntry = AddEntry("Base URL", filter.BaseUrl);
        drushimCategoryEntry = AddEntry("Category ID", filter.CategoryId.ToString());
        AddKnownValuesHint("6 - Software.");
        drushimSubcategoryEntry = AddEntry("Single subcategory ID", filter.SubcategoryId?.ToString() ?? string.Empty);
        AddKnownValuesHint("616 - Backend; 69 - .NET; 183 - Programmer; 372 - C#; 380 - Software Engineer; 209 - High-tech general.");
        drushimSubcategoriesEntry = AddEntry("Subcategory IDs", string.Join(", ", filter.SubcategoryIds));
        AddKnownValuesHint("616 - Backend; 69 - .NET; 183 - Programmer; 372 - C#; 380 - Software Engineer; 209 - High-tech general.");
        drushimAreasEntry = AddEntry("Area IDs", string.Join(", ", filter.AreaIds));
        AddKnownValuesHint("1-14 - Center and Hasharon coverage used by the default profile.");
        drushimScopesEntry = AddEntry("Scope IDs", string.Join(", ", filter.Scopes));
        AddKnownValuesHint("1 - Full-time.");
        drushimExperienceRangeEntry = AddEntry("Experience range", filter.ExperienceRange ?? string.Empty);
        drushimGeoLexEntry = AddEntry("GeoLex ID", filter.GeoLexId?.ToString() ?? string.Empty);
        drushimIncludeAreaAroundSwitch = AddSwitch("Include nearby area", filter.IncludeAreaAround);
        drushimExperienceEntry = AddEntry("Experience", filter.Experience?.ToString() ?? string.Empty);
        drushimRangeEntry = AddEntry("Range", filter.Range?.ToString() ?? string.Empty);
    }

    private void AddAllJobsFields(JobSourceOptions source)
    {
        if (!string.Equals(source.Adapter, "AllJobs", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var filter = source.AllJobsFilter ?? new AllJobsFilterOptions();
        Editor.Children.Add(new BoxView { HeightRequest = 1, Margin = new Thickness(0, 8), BackgroundColor = Colors.LightGray });
        Editor.Children.Add(new Label { Text = "AllJobs search", FontSize = 18, FontAttributes = FontAttributes.Bold });
        allJobsBaseUrlEntry = AddEntry("Base URL", filter.BaseUrl);
        allJobsPositionEntry = AddEntry("Single position ID", filter.Position.ToString());
        AddKnownValuesHint("1759 - Backend Programmer; 1994 - Backend Engineer; 1152 - .NET; 1203 - C#; 1848 - Senior Backend Developer.");
        allJobsPositionsEntry = AddEntry("Position IDs", string.Join(", ", filter.Positions));
        AddKnownValuesHint("1759 - Backend Programmer; 1994 - Backend Engineer; 1152 - .NET; 1203 - C#; 1848 - Senior Backend Developer.");
        allJobsTypesEntry = AddEntry("Employment type IDs", string.Join(", ", filter.Types));
        AddKnownValuesHint("4 - Full-time.");
        allJobsSourceEntry = AddEntry("Source ID", filter.Source?.ToString() ?? string.Empty);
        allJobsDurationEntry = AddEntry("Duration", filter.Duration?.ToString() ?? string.Empty);
        AddKnownValuesHint("25 - Past 25 days.");
        allJobsExcludeEntry = AddEntry("Exclude phrase", filter.Exclude ?? string.Empty);
        allJobsRegionEntry = AddEntry("Region", filter.Region ?? string.Empty);
        AddKnownValuesHint("2 - Center; 6 - Hasharon.");
        allJobsMaxPagesEntry = AddEntry("Maximum pages", filter.MaxPages.ToString());
    }

    private void AddJobSwipeFields(JobSourceOptions source)
    {
        if (!string.Equals(source.Adapter, "JobSwipeCo", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var filter = source.JobSwipeCoFilter ?? new JobSwipeCoFilterOptions();
        Editor.Children.Add(new Label { Text = "JobSwipe.co search", FontSize = 18, FontAttributes = FontAttributes.Bold });
        jobSwipeBaseUrlEntry = AddEntry("Base URL", filter.BaseUrl);
        jobSwipeSearchUrlsEditor = AddEditor("Search URLs", string.Join(Environment.NewLine, filter.SearchUrls));
        jobSwipeMaxDetailsEntry = AddEntry("Maximum detail pages per search", filter.MaxDetailsPerSearch.ToString());
    }

    private void AddGlassdoorFields(JobSourceOptions source)
    {
        if (!string.Equals(source.Adapter, "Glassdoor", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var filter = source.GlassdoorFilter ?? new GlassdoorFilterOptions();
        Editor.Children.Add(new Label { Text = "Glassdoor search", FontSize = 18, FontAttributes = FontAttributes.Bold });
        var hasSession = File.Exists(Path.Combine(FileSystem.AppDataDirectory, "data", "secrets", "glassdoor-session.txt"));
        Editor.Children.Add(new Label
        {
            Text = hasSession ? "Session saved" : "Session required before this source can run",
            TextColor = hasSession ? Colors.Teal : Colors.Gray
        });
        var configureSessionButton = new Button { Text = "Configure session" };
        configureSessionButton.Clicked += OnConfigureGlassdoorSessionClicked;
        Editor.Children.Add(configureSessionButton);
        glassdoorBaseUrlEntry = AddEntry("Base URL", filter.BaseUrl);
        glassdoorSearchUrlsEditor = AddEditor("Search URLs", string.Join(Environment.NewLine, filter.SearchUrls));
        glassdoorDelayEntry = AddEntry("Request delay seconds", filter.RequestDelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        glassdoorMaxPagesEntry = AddEntry("Maximum pages", filter.MaxPages.ToString());
        glassdoorJobsPerPageEntry = AddEntry("Jobs per page", filter.JobsPerPage.ToString());
    }

    private void AddSecretTelAvivFields(JobSourceOptions source)
    {
        if (!string.Equals(source.Adapter, "SecretTelAviv", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var filter = source.SecretTelAvivFilter ?? new SecretTelAvivFilterOptions();
        Editor.Children.Add(new BoxView { HeightRequest = 1, Margin = new Thickness(0, 8), BackgroundColor = Colors.LightGray });
        Editor.Children.Add(new Label { Text = "Secret Tel Aviv search", FontSize = 18, FontAttributes = FontAttributes.Bold });
        secretTelAvivBaseUrlEntry = AddEntry("Base URL", filter.BaseUrl);
        secretTelAvivSearchUrlEntry = AddEntry("Search URL", filter.SearchUrl);
        AddKnownValuesHint("Use the path or full URL produced by the site search. Known locations: Tel Aviv / Ramat Gan, Herzliya, Raanana, Haifa, Jerusalem, Beersheva. Known categories include Back End, DevOps, Front End, Full Stack, Mobile, Quality, Cyber, and Security.");
        secretTelAvivMaxDetailsEntry = AddEntry("Maximum detail pages per search", filter.MaxDetailsPerSearch.ToString());
    }

    private void AddDevJobsFields(JobSourceOptions source)
    {
        if (!string.Equals(source.Adapter, "DevJobs", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var filter = source.DevJobsFilter ?? new DevJobsFilterOptions();
        Editor.Children.Add(new BoxView { HeightRequest = 1, Margin = new Thickness(0, 8), BackgroundColor = Colors.LightGray });
        Editor.Children.Add(new Label { Text = "DevJobs search", FontSize = 18, FontAttributes = FontAttributes.Bold });
        devJobsBaseUrlEntry = AddEntry("Base URL", filter.BaseUrl);
        devJobsSearchUrlEntry = AddEntry("Search URL", filter.SearchUrl);
        AddKnownValuesHint("Use the path or full URL produced by the DevJobs filters. The collector requests numbered pages automatically.");
        devJobsMaxPagesEntry = AddEntry("Maximum pages", filter.MaxPages.ToString());
        devJobsMaxDetailsEntry = AddEntry("Maximum detail pages per result page", filter.MaxDetailsPerPage.ToString());
    }

    private void AddKnownValuesHint(string text)
    {
        Editor.Children.Add(new Label
        {
            Text = $"Known values: {text}",
            TextColor = Colors.Gray,
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap
        });
    }

    private async void OnConfigureGlassdoorSessionClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("glassdoor-session");
    }

    private Microsoft.Maui.Controls.Editor AddEditor(string label, string value)
    {
        Editor.Children.Add(new Label { Text = label, FontAttributes = FontAttributes.Bold });
        var editor = new Microsoft.Maui.Controls.Editor { Text = value, AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 100 };
        Editor.Children.Add(editor);
        return editor;
    }

    private static IReadOnlyList<string> SplitIds(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyList<string> SplitLines(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool TryParseInt(string? value, out int result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = 0;
            return true;
        }

        return int.TryParse(value, out result);
    }

    private static bool TryParseOptionalInt(string? value, out int? result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = null;
            return true;
        }

        var success = int.TryParse(value, out var parsed);
        result = success ? parsed : null;
        return success;
    }

    private static bool TryParseDouble(string? value, out double result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = 0;
            return true;
        }

        return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseIntList(string? value, out IReadOnlyList<int> result)
    {
        var values = new List<int>();
        foreach (var item in SplitIds(value))
        {
            if (!int.TryParse(item, out var parsed))
            {
                result = [];
                return false;
            }

            values.Add(parsed);
        }

        result = values;
        return true;
    }

    private sealed record ProfileListItem(string Name, string Adapter);
}
