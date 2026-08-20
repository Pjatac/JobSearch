namespace JobWatcher.Models;

public sealed record JobVacancy
{
    public required string Source { get; init; }
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public string? Company { get; init; }
    public string? Location { get; init; }
    public required string Url { get; init; }
    public string? Description { get; init; }
    public DateOnly? DatePosted { get; init; }
    public DateOnly? ValidThrough { get; init; }
    public IReadOnlyList<string> EmploymentTypes { get; init; } = [];
    public required DateTimeOffset CollectedAtUtc { get; init; }
    public JobClassification? Classification { get; init; }
}

public sealed record JobClassification
{
    public required string Classification { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = [];
    public required JobClassificationFlags Flags { get; init; }
}

public sealed record JobClassificationFlags
{
    public required bool FarCommute { get; init; }
    public required bool Cyber { get; init; }
}
