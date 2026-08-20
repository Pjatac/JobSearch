using JobWatcher.Configuration;

namespace JobWatcher.App;

public partial class ClassificationPage : ContentPage
{
    private readonly JobWatcherSettingsStore settingsStore;
    private IReadOnlyList<JobSourceOptions> sources = [];
    private string? settingsPath;
    private string? defaultSettingsJson;

    public ClassificationPage(JobWatcherSettingsStore settingsStore)
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
        sources = options.Sources;
        Show(options.Classification);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (settingsPath is null || defaultSettingsJson is null || !int.TryParse(DescriptionScanLengthEntry.Text, out var scanLength) || scanLength < 0)
        {
            await DisplayAlertAsync("Check the rules", "Description scan length must be zero or greater.", "OK");
            return;
        }

        var classification = new JobClassificationOptions
        {
            Enabled = EnabledSwitch.IsToggled,
            DescriptionScanLength = scanLength,
            IncludeSignals = SplitLines(IncludeSignalsEditor.Text),
            OtherPrimaryLanguages = SplitLines(OtherLanguagesEditor.Text),
            RoleMismatchSignals = SplitLines(RoleMismatchEditor.Text),
            JuniorSignals = SplitLines(JuniorSignalsEditor.Text),
            JuniorExperiencePatterns = SplitLines(JuniorPatternsEditor.Text),
            SeniorOverrideSignals = SplitLines(SeniorOverrideEditor.Text),
            FarCommuteLocations = SplitLines(FarCommuteEditor.Text),
            SpecialInterestLabel = SpecialInterestLabelEntry.Text?.Trim() ?? string.Empty,
            CyberSignals = SplitLines(CyberSignalsEditor.Text)
        };
        await settingsStore.UpdateAsync(settingsPath, defaultSettingsJson, current => current with { Classification = classification });
        await DisplayAlertAsync("Draft saved", "Classification rules were saved. Existing collection history was preserved.", "OK");
    }

    private void Show(JobClassificationOptions options)
    {
        EnabledSwitch.IsToggled = options.Enabled;
        DescriptionScanLengthEntry.Text = options.DescriptionScanLength.ToString();
        IncludeSignalsEditor.Text = JoinLines(options.IncludeSignals);
        OtherLanguagesEditor.Text = JoinLines(options.OtherPrimaryLanguages);
        RoleMismatchEditor.Text = JoinLines(options.RoleMismatchSignals);
        JuniorSignalsEditor.Text = JoinLines(options.JuniorSignals);
        JuniorPatternsEditor.Text = JoinLines(options.JuniorExperiencePatterns);
        SeniorOverrideEditor.Text = JoinLines(options.SeniorOverrideSignals);
        FarCommuteEditor.Text = JoinLines(options.FarCommuteLocations);
        SpecialInterestLabelEntry.Text = options.SpecialInterestLabel;
        CyberSignalsEditor.Text = JoinLines(options.CyberSignals);
    }

    private static IReadOnlyList<string> SplitLines(string? value) => string.IsNullOrWhiteSpace(value) ? [] : value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string JoinLines(IEnumerable<string> values) => string.Join(Environment.NewLine, values);
}
