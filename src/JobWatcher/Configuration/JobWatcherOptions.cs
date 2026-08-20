namespace JobWatcher.Configuration;

public sealed class JobWatcherOptions
{
    public string DataDirectory { get; init; } = "data";
    public int RequestTimeoutSeconds { get; init; } = 30;
    public int OutputHistoryRetentionCount { get; init; } = 2;
    public JobClassificationOptions Classification { get; init; } = new();
    public IReadOnlyList<JobSourceOptions> Sources { get; init; } = [];
}

public sealed class JobClassificationOptions
{
    public bool Enabled { get; init; } = true;
    public int DescriptionScanLength { get; init; } = 400;
    public IReadOnlyList<string> IncludeSignals { get; init; } = [];
    public IReadOnlyList<string> OtherPrimaryLanguages { get; init; } = [];
    public IReadOnlyList<string> RoleMismatchSignals { get; init; } = [];
    public IReadOnlyList<string> JuniorSignals { get; init; } = [];
    public IReadOnlyList<string> JuniorExperiencePatterns { get; init; } = [];
    public IReadOnlyList<string> SeniorOverrideSignals { get; init; } = [];
    public IReadOnlyList<string> FarCommuteLocations { get; init; } = [];
    public string SpecialInterestLabel { get; init; } = "Cyber / security";
    public IReadOnlyList<string> CyberSignals { get; init; } = [];
}
