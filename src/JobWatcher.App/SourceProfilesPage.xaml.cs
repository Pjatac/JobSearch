using JobWatcher.Configuration;
using JobWatcher.Sources.AllJobs;
using JobWatcher.Sources.Drushim;
using JobWatcher.Sources.DevJobs;
using JobWatcher.Sources.JobKarov;
using JobWatcher.Sources.SecretTelAviv;

namespace JobWatcher.App;

public partial class SourceProfilesPage : ContentPage
{
    private static readonly string[] DevJobsDeveloperTypes = ["Software", "Frontend", "Backend", "Full Stack", "Mobile", "Game", "AI/ML", "Data & Analytics", "Cloud/DevOps", "Embedded"];
    private static readonly string[] DevJobsDistricts = ["Beer Sheva & South", "Eilat", "Haifa & North", "Hasharon", "Jerusalem", "Shfela", "Tel Aviv & Center"];
    private static readonly string[] DevJobsCities = ["Ashdod", "Ashkelon", "Be'er Sheva", "Beer Yaakov", "Binyamina", "Caesarea", "Dimona", "Even Yehuda", "Ganei Tikva", "Givat Shmuel", "Givatayim", "Hadera", "Haifa", "Hatzerim", "Herzliya", "Hod HaSharon", "Holon", "Jerusalem", "Kadima - Zoran", "Karmiel", "Kfar Netter", "Kfar Saba", "Kiryat Ata", "Kiryat Bialik", "Kiryat Gat", "Kiryat Ono", "Lod", "Migdal HaEmek", "Migdal Tefen", "Modi'in-Maccabim-Reut", "Nes Ziona", "Netanya", "Or Yehuda", "Petah Tikva", "Raanana", "Ramat Gan", "Ramat Ha-Sharon", "Rehovot", "Rishon Le Zion", "Rosh HaAyin", "Tel Aviv", "Tel Aviv-Yafo", "Tirat Carmel", "Yavne", "Yehud Monosson", "Yokneam Ilit"];
    private static readonly JobKarovCategory[] JobKarovCategories =
    [
        new("3921", "אבטחת מידע וסייבר"), new("1494", "אופטיקה"), new("1498", "אופנה וטקסטיל"), new("1504", "אחזקה ושירותי ניקיון"), new("1510", "אינטרנט ומדיה"), new("1544", "אלקטרוניקה וחשמל"), new("1557", "בידור מדיה ואומנות"), new("1570", "ביוטכנולוגיה"), new("1580", "ביטוח"), new("1597", "בינוי נדל\"ן ותשתיות"), new("1604", "הנדסאים"), new("1612", "חומרה"), new("1633", "חינוך והוראה"), new("2425", "טבע וחקלאות"), new("1645", "טיפוח ויופי"), new("1654", "יצוא ויבוא"), new("1663", "כלכלה כספים וחשבונאות"), new("1686", "כללי"), new("1708", "מדעי החברה"), new("1719", "מדעי החיים"), new("1734", "מדעים והנדסה"), new("1752", "מהנדסים"), new("1767", "מזון ומסעדנות"), new("1785", "מזכירות ואדמיניסטרציה"), new("1797", "מחשבים ותקשורת"), new("1825", "מכירות"), new("1844", "מלונאות תיירות וטיולים"), new("1857", "מערכות מידע"), new("1868", "משאבי אנוש"), new("1882", "משפטים ועריכת דין"), new("1900", "נהגים הפצה ושליחויות"), new("1910", "ניהול בכיר"), new("1928", "ניתוח מערכות"), new("1935", "סטודנטים ונוער"), new("2269", "סיעוד"), new("1951", "ספורט ואימון"), new("1959", "ספנות ותעופה"), new("1969", "עיצוב"), new("1997", "עריכה ותוכן"), new("2346", "פרסום, שיווק ויחסי ציבור"), new("2008", "קמעונאות"), new("2025", "רכב"), new("2039", "רכש קניינות ולוגיסטיקה"), new("2053", "רפואה בריאות וסיעוד"), new("2087", "רפואה משלימה"), new("2096", "שירות לקוחות"), new("2108", "שמירה וביטחון"), new("2119", "תוכנה"), new("2185", "תוכנה QA"), new("2199", "תעשייה וייצור")
    ];
    private static readonly JobKarovArea[] JobKarovAreas =
    [
        new("-2", "כל הארץ", "כללי"), new("-1", "סביבי", "כללי"),
        new("11", "צפון", "צפון"), new("12", "גליל עליון", "צפון"), new("13", "גליל תחתון", "צפון"), new("14", "הגולן", "צפון"), new("15", "הכנרת והסביבה", "צפון"), new("16", "חיפה והסביבה", "צפון"), new("17", "כרמיאל והסביבה", "צפון"), new("18", "נצרת - שפרעם והסביבה", "צפון"), new("19", "עכו - נהריה והסביבה", "צפון"), new("20", "קריות והסביבה", "צפון"), new("21", "ראש פינה החולה", "צפון"),
        new("30", "חדרה זכרון ועמקים", "חדרה זכרון ועמקים"), new("31", "זכרון וחוף הכרמל", "חדרה זכרון ועמקים"), new("32", "חדרה והסביבה", "חדרה זכרון ועמקים"), new("33", "יקנעם טבעון והסביבה", "חדרה זכרון ועמקים"), new("34", "עמק בית שאן", "חדרה זכרון ועמקים"), new("35", "עפולה והעמקים", "חדרה זכרון ועמקים"), new("36", "קיסריה והסביבה", "חדרה זכרון ועמקים"), new("37", "רמת מנשה", "חדרה זכרון ועמקים"),
        new("50", "השרון", "השרון"), new("51", "דרום השרון", "השרון"), new("52", "הוד השרון והסביבה", "השרון"), new("53", "נתניה והסביבה", "השרון"), new("54", "צפון השרון", "השרון"), new("55", "רמת השרון - הרצליה", "השרון"), new("56", "רעננה - כפר סבא", "השרון"),
        new("70", "מרכז", "מרכז"), new("71", "בני ברק - גבעת שמואל", "מרכז"), new("72", "בקעת אונו", "מרכז"), new("73", "חולון - בת ים", "מרכז"), new("74", "מודיעין והסביבה", "מרכז"), new("76", "פתח תקווה והסביבה", "מרכז"), new("77", "ראש העין והסביבה", "מרכז"), new("78", "ראשון לציון והסביבה", "מרכז"), new("79", "רמלה - לוד", "מרכז"), new("80", "רמת גן - גבעתיים", "מרכז"), new("81", "שוהם והסביבה", "מרכז"), new("82", "תל אביב", "מרכז"),
        new("90", "אזור ירושלים", "אזור ירושלים"), new("91", "בית שמש והסביבה", "אזור ירושלים"), new("92", "הרי יהודה - מבשרת והסביבה", "אזור ירושלים"), new("93", "ירושלים", "אזור ירושלים"), new("94", "מעלה אדומים והסביבה", "אזור ירושלים"),
        new("110", "יהודה שומרון ובקעת הירדן", "יהודה שומרון ובקעת הירדן"), new("111", "אריאל וישובי שומרון", "יהודה שומרון ובקעת הירדן"), new("112", "בקעת הירדן וצפון ים המלח", "יהודה שומרון ובקעת הירדן"), new("113", "גוש עציון", "יהודה שומרון ובקעת הירדן"), new("114", "ישובי דרום ההר", "יהודה שומרון ובקעת הירדן"),
        new("130", "שפלה מישור חוף דרומי", "שפלה מישור חוף דרומי"), new("131", "אשדוד - אשקלון והסביבה", "שפלה מישור חוף דרומי"), new("132", "גדרה - יבנה והסביבה", "שפלה מישור חוף דרומי"), new("133", "נס ציונה - רחובות", "שפלה מישור חוף דרומי"), new("134", "קרית גת והסביבה", "שפלה מישור חוף דרומי"), new("135", "שפלה", "שפלה מישור חוף דרומי"),
        new("150", "דרום", "דרום"), new("151", "אילת וערבה", "דרום"), new("152", "באר שבע והסביבה", "דרום"), new("153", "דרום ים המלח", "דרום"), new("154", "הנגב המערבי", "דרום"), new("155", "ישובי הנגב", "דרום")
    ];
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
    private readonly HashSet<string> jobKarovSelectedSpecialities = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string>? jobKarovCategoryDialogSelection;
    private readonly List<JobKarovCategoryOption> jobKarovCategoryOptions = JobKarovCategories.Select(category => new JobKarovCategoryOption(category)).ToList();
    private Label? jobKarovCategoriesSummary;
    private readonly HashSet<string> jobKarovSelectedRoles = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string>? jobKarovRoleDialogSelection;
    private readonly List<JobKarovRoleOption> jobKarovRoleOptions = JobKarovRoleCatalog.All.Select(role => new JobKarovRoleOption(role)).ToList();
    private Label? jobKarovRolesSummary;
    private Entry? jobKarovQueryEntry;
    private readonly HashSet<string> jobKarovSelectedAreas = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string>? jobKarovAreaDialogSelection;
    private readonly List<JobKarovAreaOption> jobKarovAreaOptions = JobKarovAreas.Select(area => new JobKarovAreaOption(area)).ToList();
    private Label? jobKarovAreasSummary;
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
    private Switch? devJobsUseUrlOverrideSwitch;
    private readonly Dictionary<string, CheckBox> devJobsDeveloperTypeChecks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CheckBox> devJobsDistrictChecks = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> devJobsSelectedCities = new(StringComparer.OrdinalIgnoreCase);
    private SearchBar? devJobsCitySearchBar;
    private VerticalStackLayout? devJobsCitiesLayout;
    private Entry? devJobsNameFilterEntry;
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

    private async void OnProfileSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ProfileListItem selected)
        {
            return;
        }

        var nextIndex = sources.FindIndex(source => string.Equals(source.Name, selected.Name, StringComparison.OrdinalIgnoreCase));
        if (nextIndex < 0 || nextIndex == selectedIndex)
        {
            return;
        }

        if (selectedIndex >= 0 && !await TryApplySelectedDraftAsync())
        {
            var currentName = sources[selectedIndex].Name;
            Profiles.SelectedItem = ((IEnumerable<ProfileListItem>?)Profiles.ItemsSource)
                ?.FirstOrDefault(item => string.Equals(item.Name, currentName, StringComparison.OrdinalIgnoreCase));
            return;
        }

        selectedIndex = nextIndex;

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
        return AddEntry(Editor, label, value);
    }

    private Entry AddEntry(VerticalStackLayout layout, string label, string value)
    {
        layout.Children.Add(new Label { Text = label, FontAttributes = FontAttributes.Bold });
        var entry = new Entry { Text = value };
        entry.TextChanged += OnEditorValueChanged;
        layout.Children.Add(entry);
        return entry;
    }

    private Switch AddSwitch(string label, bool value)
    {
        return AddSwitch(Editor, label, value);
    }

    private Switch AddSwitch(VerticalStackLayout layout, string label, bool value)
    {
        var toggle = new Switch { IsToggled = value };
        toggle.Toggled += OnEditorValueChanged;
        layout.Children.Add(new HorizontalStackLayout
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
        return new JobSourceOptions
        {
            Name = source.Name,
            Adapter = source.Adapter,
            Url = directUrlEntry?.Text?.Trim(),
            JobKarovFilter = new JobKarovFilterOptions
            {
                BaseUrl = jobKarovBaseUrlEntry?.Text?.Trim() ?? string.Empty,
                Query = jobKarovQueryEntry?.Text?.Trim() ?? string.Empty,
                Specialities = SelectedJobKarovSpecialities(),
                Speciality = string.Empty,
                Roles = jobKarovSelectedRoles.ToList(),
                Areas = SelectedJobKarovAreas(),
                Size = JobKarovUrlBuilder.FixedSearchSize
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
            jobKarovFilter = new JobKarovFilterOptions
            {
                BaseUrl = jobKarovBaseUrlEntry.Text?.Trim() ?? string.Empty,
                Query = jobKarovQueryEntry?.Text?.Trim() ?? string.Empty,
                Specialities = SelectedJobKarovSpecialities(),
                Speciality = string.Empty,
                Roles = jobKarovSelectedRoles.ToList(),
                Areas = SelectedJobKarovAreas(),
                Size = JobKarovUrlBuilder.FixedSearchSize
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
            var developerTypes = devJobsDeveloperTypeChecks
                .Where(pair => pair.Value.IsChecked)
                .Select(pair => pair.Key)
                .ToList();
            var districts = devJobsDistrictChecks
                .Where(pair => pair.Value.IsChecked)
                .Select(pair => pair.Key)
                .ToList();
            var cities = devJobsSelectedCities.OrderBy(city => city, StringComparer.OrdinalIgnoreCase).ToList();

            var useSearchUrlOverride = devJobsUseUrlOverrideSwitch?.IsToggled ?? false;
            var candidate = new DevJobsFilterOptions
            {
                BaseUrl = baseUrl,
                SearchUrl = searchUrl,
                UseSearchUrlOverride = useSearchUrlOverride,
                DeveloperTypes = developerTypes,
                Districts = districts,
                Cities = cities
            };
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
                UseSearchUrlOverride = useSearchUrlOverride,
                DeveloperTypes = developerTypes,
                Districts = districts,
                Cities = cities,
                NameFilter = devJobsNameFilterEntry?.Text?.Trim(),
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
        jobKarovQueryEntry = AddEntry("Search words", filter.Query);
        AddKnownValuesHint("Examples: C# .Net, Backend, ASP.NET. JobKarov sends this as the query parameter.");
        Editor.Children.Add(new Label { Text = "Categories", FontAttributes = FontAttributes.Bold });
        var selectedSpecialities = filter.Specialities.Count > 0 ? filter.Specialities : string.IsNullOrWhiteSpace(filter.Speciality) ? [] : [filter.Speciality];
        jobKarovSelectedSpecialities.Clear();
        foreach (var speciality in selectedSpecialities)
        {
            jobKarovSelectedSpecialities.Add(speciality);
        }

        var chooseCategoriesButton = new Button { Text = "Choose categories" };
        chooseCategoriesButton.Clicked += OnChooseJobKarovCategoriesClicked;
        Editor.Children.Add(chooseCategoriesButton);
        jobKarovCategoriesSummary = new Label { TextColor = Colors.Gray, FontSize = 12 };
        Editor.Children.Add(jobKarovCategoriesSummary);
        UpdateJobKarovCategoriesSummary();
        jobKarovSelectedRoles.Clear();
        jobKarovSelectedRoles.UnionWith(filter.Roles);
        var chooseRolesButton = new Button { Text = "Choose roles" };
        chooseRolesButton.Clicked += OnChooseJobKarovRolesClicked;
        Editor.Children.Add(chooseRolesButton);
        jobKarovRolesSummary = new Label { TextColor = Colors.Gray, FontSize = 12 };
        Editor.Children.Add(jobKarovRolesSummary);
        UpdateJobKarovRolesSummary();
        Editor.Children.Add(new Label { Text = "Locations", FontAttributes = FontAttributes.Bold });
        jobKarovSelectedAreas.Clear();
        jobKarovSelectedAreas.UnionWith(filter.Areas);
        var chooseAreasButton = new Button { Text = "Choose locations" };
        chooseAreasButton.Clicked += OnChooseJobKarovAreasClicked;
        Editor.Children.Add(chooseAreasButton);
        jobKarovAreasSummary = new Label { TextColor = Colors.Gray, FontSize = 12 };
        Editor.Children.Add(jobKarovAreasSummary);
        UpdateJobKarovAreasSummary();
        AddKnownValuesHint("The JobKarov size parameter is fixed to the verified working value: 2.");
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
        var legacyValues = GetDevJobsLegacyValues(filter);
        var selectedDeveloperTypes = filter.DeveloperTypes.Count > 0 ? filter.DeveloperTypes : legacyValues.DeveloperTypes;
        var selectedDistricts = filter.Districts.Count > 0
            ? filter.Districts
            : !string.IsNullOrWhiteSpace(filter.District) ? [filter.District] : legacyValues.Districts;
        var selectedCities = filter.Cities.Count > 0 ? filter.Cities : legacyValues.Cities;
        Editor.Children.Add(new BoxView { HeightRequest = 1, Margin = new Thickness(0, 8), BackgroundColor = Colors.LightGray });
        Editor.Children.Add(new Label { Text = "DevJobs search", FontSize = 18, FontAttributes = FontAttributes.Bold });
        Editor.Children.Add(new Label { Text = "Developer types", FontAttributes = FontAttributes.Bold });
        devJobsDeveloperTypeChecks.Clear();
        var developerTypesLayout = new VerticalStackLayout { Spacing = 6 };
        foreach (var developerType in DevJobsDeveloperTypes)
        {
            var checkBox = new CheckBox { IsChecked = selectedDeveloperTypes.Contains(developerType, StringComparer.OrdinalIgnoreCase) };
            checkBox.CheckedChanged += OnEditorValueChanged;
            devJobsDeveloperTypeChecks.Add(developerType, checkBox);
            developerTypesLayout.Children.Add(new HorizontalStackLayout
            {
                Spacing = 4,
                Children = { checkBox, new Label { Text = developerType, VerticalOptions = LayoutOptions.Center } }
            });
        }

        Editor.Children.Add(developerTypesLayout);
        Editor.Children.Add(new Label { Text = "Districts", FontAttributes = FontAttributes.Bold });
        devJobsDistrictChecks.Clear();
        var districtsLayout = new VerticalStackLayout { Spacing = 6 };
        foreach (var district in DevJobsDistricts)
        {
            var checkBox = new CheckBox { IsChecked = selectedDistricts.Contains(district, StringComparer.OrdinalIgnoreCase) };
            checkBox.CheckedChanged += OnEditorValueChanged;
            devJobsDistrictChecks.Add(district, checkBox);
            districtsLayout.Children.Add(new HorizontalStackLayout
            {
                Spacing = 4,
                Children = { checkBox, new Label { Text = district, VerticalOptions = LayoutOptions.Center } }
            });
        }

        Editor.Children.Add(districtsLayout);
        Editor.Children.Add(new Label { Text = "Cities", FontAttributes = FontAttributes.Bold });
        Editor.Children.Add(new Label
        {
            Text = "Choose one or more cities. Each selected district or city is searched separately, then results are combined.",
            TextColor = Colors.Gray,
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap
        });
        devJobsSelectedCities.Clear();
        foreach (var city in selectedCities)
        {
            devJobsSelectedCities.Add(city);
        }

        devJobsCitySearchBar = new SearchBar { Placeholder = "Find a city" };
        devJobsCitySearchBar.TextChanged += OnDevJobsCitySearchTextChanged;
        Editor.Children.Add(devJobsCitySearchBar);
        devJobsCitiesLayout = new VerticalStackLayout { Spacing = 6 };
        Editor.Children.Add(devJobsCitiesLayout);
        RebuildDevJobsCityOptions();
        devJobsNameFilterEntry = AddEntry("Job name or skill text", filter.NameFilter ?? string.Empty);
        AddKnownValuesHint("This is applied through the site's Livewire search after the URL filters load. Example: .NET.");
        devJobsMaxPagesEntry = AddEntry("Maximum pages", filter.MaxPages.ToString());
        devJobsMaxDetailsEntry = AddEntry("Maximum detail pages per result page", filter.MaxDetailsPerPage.ToString());

        var advancedContent = new VerticalStackLayout { Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
        devJobsUseUrlOverrideSwitch = AddSwitch(advancedContent, "Use custom search URL", filter.UseSearchUrlOverride);
        devJobsBaseUrlEntry = AddEntry(advancedContent, "Base URL", filter.BaseUrl);
        devJobsSearchUrlEntry = AddEntry(advancedContent, "Search URL override", filter.SearchUrl);
        advancedContent.Children.Add(new Label
        {
            Text = "When enabled, the URL override replaces the developer types, districts, and cities above.",
            TextColor = Colors.Gray,
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap
        });
        Editor.Children.Add(new BoxView { HeightRequest = 1, Margin = new Thickness(0, 8), BackgroundColor = Colors.LightGray });
        Editor.Children.Add(new Label { Text = "Advanced override", FontAttributes = FontAttributes.Bold });
        Editor.Children.Add(advancedContent);
    }

    private void OnDevJobsCitySearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        RebuildDevJobsCityOptions();
    }

    private void RebuildDevJobsCityOptions()
    {
        if (devJobsCitiesLayout is null)
        {
            return;
        }

        var searchText = devJobsCitySearchBar?.Text?.Trim() ?? string.Empty;
        var cities = DevJobsCities.Where(city => city.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();
        devJobsCitiesLayout.Children.Clear();
        foreach (var city in cities)
        {
            var checkBox = new CheckBox { IsChecked = devJobsSelectedCities.Contains(city) };
            checkBox.CheckedChanged += (_, args) =>
            {
                if (args.Value)
                {
                    devJobsSelectedCities.Add(city);
                }
                else
                {
                    devJobsSelectedCities.Remove(city);
                }

                OnEditorValueChanged(null, EventArgs.Empty);
            };
            devJobsCitiesLayout.Children.Add(new HorizontalStackLayout
            {
                Spacing = 4,
                Children = { checkBox, new Label { Text = city, VerticalOptions = LayoutOptions.Center } }
            });
        }

        if (cities.Count == 0)
        {
            devJobsCitiesLayout.Children.Add(new Label { Text = "No matching city", TextColor = Colors.Gray });
        }
    }

    private static (IReadOnlyList<string> DeveloperTypes, IReadOnlyList<string> Districts, IReadOnlyList<string> Cities) GetDevJobsLegacyValues(DevJobsFilterOptions filter)
    {
        if (!Uri.TryCreate(filter.BaseUrl, UriKind.Absolute, out var baseUri) || string.IsNullOrWhiteSpace(filter.SearchUrl))
        {
            return ([], [], []);
        }

        var searchUri = Uri.TryCreate(filter.SearchUrl, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(new Uri(baseUri.ToString().TrimEnd('/') + "/"), filter.SearchUrl.TrimStart('/'));
        var query = System.Web.HttpUtility.ParseQueryString(searchUri.Query);
        var developerTypes = (query["developerTypes"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var districts = (query["district"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cities = (query["city"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (developerTypes, districts, cities);
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

    private IReadOnlyList<string> SelectedJobKarovSpecialities() => jobKarovSelectedSpecialities.ToList();

    private void OnChooseJobKarovCategoriesClicked(object? sender, EventArgs e)
    {
        jobKarovCategoryDialogSelection = new HashSet<string>(jobKarovSelectedSpecialities, StringComparer.OrdinalIgnoreCase);
        foreach (var option in jobKarovCategoryOptions) option.IsSelected = jobKarovCategoryDialogSelection.Contains(option.Category.Id);
        RebuildJobKarovCategoryDialog();
        JobKarovCategoriesDialog.IsVisible = true;
    }

    private void OnSelectAllJobKarovCategoriesClicked(object? sender, EventArgs e)
    {
        if (jobKarovCategoryDialogSelection is null)
        {
            return;
        }

        foreach (var option in jobKarovCategoryOptions) option.IsSelected = true;
        RebuildJobKarovCategoryDialog();
    }

    private void OnClearJobKarovCategoriesClicked(object? sender, EventArgs e)
    {
        foreach (var option in jobKarovCategoryOptions) option.IsSelected = false;
        RebuildJobKarovCategoryDialog();
    }

    private void OnCancelJobKarovCategoriesClicked(object? sender, EventArgs e)
    {
        jobKarovCategoryDialogSelection = null;
        JobKarovCategoriesDialog.IsVisible = false;
    }

    private void OnDoneJobKarovCategoriesClicked(object? sender, EventArgs e)
    {
        if (jobKarovCategoryDialogSelection is not null)
        {
            jobKarovSelectedSpecialities.Clear();
            jobKarovSelectedSpecialities.UnionWith(jobKarovCategoryOptions.Where(option => option.IsSelected).Select(option => option.Category.Id));
            UpdateJobKarovCategoriesSummary();
        }

        jobKarovCategoryDialogSelection = null;
        JobKarovCategoriesDialog.IsVisible = false;
    }

    private void RebuildJobKarovCategoryDialog()
    {
        if (jobKarovCategoryDialogSelection is null)
        {
            return;
        }

        JobKarovCategoriesColumns.ItemsSource = jobKarovCategoryOptions;
    }

    private void UpdateJobKarovCategoriesSummary()
    {
        if (jobKarovCategoriesSummary is not null)
        {
            jobKarovCategoriesSummary.Text = jobKarovSelectedSpecialities.Count == 0
                ? "No categories selected"
                : $"{jobKarovSelectedSpecialities.Count} categories selected";
        }
    }

    private void OnChooseJobKarovRolesClicked(object? sender, EventArgs e)
    {
        jobKarovRoleDialogSelection = new HashSet<string>(jobKarovSelectedRoles, StringComparer.OrdinalIgnoreCase);
        foreach (var option in jobKarovRoleOptions) option.IsSelected = jobKarovRoleDialogSelection.Contains(option.Role.Id);
        JobKarovRoleSearch.Text = string.Empty;
        RebuildJobKarovRoles();
        JobKarovRolesDialog.IsVisible = true;
    }

    private void OnJobKarovRoleSearchTextChanged(object? sender, TextChangedEventArgs e) => RebuildJobKarovRoles();

    private void RebuildJobKarovRoles()
    {
        if (jobKarovRoleDialogSelection is null) return;
        var term = JobKarovRoleSearch.Text?.Trim() ?? string.Empty;
        JobKarovRolesList.ItemsSource = jobKarovRoleOptions
            .Where(option => string.IsNullOrWhiteSpace(term) || option.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase))
            .GroupBy(option => option.Role.Category)
            .Select(group => new JobKarovRoleGroup(group.Key, group))
            .ToList();
    }

    private void OnCancelJobKarovRolesClicked(object? sender, EventArgs e) { jobKarovRoleDialogSelection = null; JobKarovRolesDialog.IsVisible = false; }

    private void OnDoneJobKarovRolesClicked(object? sender, EventArgs e)
    {
        if (jobKarovRoleDialogSelection is not null) { jobKarovSelectedRoles.Clear(); jobKarovSelectedRoles.UnionWith(jobKarovRoleOptions.Where(option => option.IsSelected).Select(option => option.Role.Id)); UpdateJobKarovRolesSummary(); }
        jobKarovRoleDialogSelection = null; JobKarovRolesDialog.IsVisible = false;
    }

    private void OnClearJobKarovRolesClicked(object? sender, EventArgs e)
    {
        foreach (var option in jobKarovRoleOptions) option.IsSelected = false;
        RebuildJobKarovRoles();
    }

    private void UpdateJobKarovRolesSummary()
    {
        if (jobKarovRolesSummary is not null) jobKarovRolesSummary.Text = jobKarovSelectedRoles.Count == 0 ? "No roles selected" : $"{jobKarovSelectedRoles.Count} roles selected";
    }

    private IReadOnlyList<string> SelectedJobKarovAreas() => jobKarovSelectedAreas.ToList();

    private void OnChooseJobKarovAreasClicked(object? sender, EventArgs e)
    {
        jobKarovAreaDialogSelection = new HashSet<string>(jobKarovSelectedAreas, StringComparer.OrdinalIgnoreCase);
        foreach (var option in jobKarovAreaOptions) option.IsSelected = jobKarovAreaDialogSelection.Contains(option.Area.Id);
        JobKarovAreaSearch.Text = string.Empty;
        RebuildJobKarovAreas();
        JobKarovAreasDialog.IsVisible = true;
    }

    private void OnJobKarovAreaSearchTextChanged(object? sender, TextChangedEventArgs e) => RebuildJobKarovAreas();

    private void RebuildJobKarovAreas()
    {
        if (jobKarovAreaDialogSelection is null) return;
        var term = JobKarovAreaSearch.Text?.Trim() ?? string.Empty;
        JobKarovAreasList.ItemsSource = jobKarovAreaOptions
            .Where(option => string.IsNullOrWhiteSpace(term) || option.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase))
            .GroupBy(option => option.Area.Region)
            .Select(group => new JobKarovAreaGroup(group.Key, group))
            .ToList();
    }

    private void OnSelectAllJobKarovAreasClicked(object? sender, EventArgs e)
    {
        if (jobKarovAreaDialogSelection is null)
        {
            return;
        }

        var term = JobKarovAreaSearch.Text?.Trim() ?? string.Empty;
        foreach (var option in jobKarovAreaOptions.Where(option => string.IsNullOrWhiteSpace(term) || option.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            option.IsSelected = true;
        }

        RebuildJobKarovAreas();
    }

    private void OnClearJobKarovAreasClicked(object? sender, EventArgs e)
    {
        foreach (var option in jobKarovAreaOptions) option.IsSelected = false;
        RebuildJobKarovAreas();
    }

    private void OnCancelJobKarovAreasClicked(object? sender, EventArgs e)
    {
        jobKarovAreaDialogSelection = null;
        JobKarovAreasDialog.IsVisible = false;
    }

    private void OnDoneJobKarovAreasClicked(object? sender, EventArgs e)
    {
        if (jobKarovAreaDialogSelection is not null)
        {
            jobKarovSelectedAreas.Clear();
            jobKarovSelectedAreas.UnionWith(jobKarovAreaOptions.Where(option => option.IsSelected).Select(option => option.Area.Id));
            UpdateJobKarovAreasSummary();
        }

        jobKarovAreaDialogSelection = null;
        JobKarovAreasDialog.IsVisible = false;
    }

    private void UpdateJobKarovAreasSummary()
    {
        if (jobKarovAreasSummary is not null) jobKarovAreasSummary.Text = jobKarovSelectedAreas.Count == 0 ? "No locations selected" : $"{jobKarovSelectedAreas.Count} locations selected";
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

    private sealed record JobKarovCategory(string Id, string Name);
    private sealed record JobKarovArea(string Id, string Name, string Region);
    private sealed class JobKarovCategoryOption(JobKarovCategory category)
    {
        public JobKarovCategory Category { get; } = category;
        public string DisplayName => $"{Category.Name} ({Category.Id})";
        public bool IsSelected { get; set; }
    }
    private sealed class JobKarovRoleOption(JobKarovRole role)
    {
        public JobKarovRole Role { get; } = role;
        public string DisplayName => $"{Role.Name} ({Role.Id})";
        public string SearchText => $"{Role.Name} {Role.Category} {Role.Id}";
        public bool IsSelected { get; set; }
    }
    private sealed class JobKarovRoleGroup(string category, IEnumerable<JobKarovRoleOption> roles) : List<JobKarovRoleOption>(roles)
    {
        public string Category { get; } = category;
    }
    private sealed class JobKarovAreaOption(JobKarovArea area)
    {
        public JobKarovArea Area { get; } = area;
        public string DisplayName => $"{Area.Name} ({Area.Id})";
        public string SearchText => $"{Area.Name} {Area.Region} {Area.Id}";
        public bool IsSelected { get; set; }
    }
    private sealed class JobKarovAreaGroup(string region, IEnumerable<JobKarovAreaOption> areas) : List<JobKarovAreaOption>(areas)
    {
        public string Region { get; } = region;
    }
    private sealed record ProfileListItem(string Name, string Adapter);
}
