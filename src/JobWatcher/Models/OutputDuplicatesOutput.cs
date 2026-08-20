namespace JobWatcher.Models;

/// <summary>
/// A duplicate review of the user-facing output itself, rather than of the raw snapshots.
/// </summary>
/// <remarks>
/// This complements <see cref="DuplicateCandidatesOutput"/>, which scores fuzzy matches across
/// different sites over full snapshots. This report answers a narrower question — "is anything in
/// the list I am about to read the same job twice?" — and so runs over the deduplicated
/// <c>newJobs</c>, includes pairs from the same site, and uses exact keys instead of scores.
/// </remarks>
public sealed record OutputDuplicatesOutput
{
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required int ReviewedJobCount { get; init; }
    public required int DuplicateGroupCount { get; init; }

    /// <summary>Jobs that would be removed by keeping one entry per duplicate group.</summary>
    public required int RedundantJobCount { get; init; }

    public required int SharedDescriptionGroupCount { get; init; }

    /// <summary>Groups where the same job appears more than once.</summary>
    public IReadOnlyList<OutputDuplicateGroup> DuplicateGroups { get; init; } = [];

    /// <summary>
    /// Groups of different jobs that share an identical description. Not duplicates: employers
    /// reuse boilerplate, and Glassdoor's search API returns a short teaser rather than the posting
    /// text. Reported so a repeated description is not mistaken for a repeated job.
    /// </summary>
    public IReadOnlyList<OutputDuplicateGroup> SharedDescriptionGroups { get; init; } = [];
}

public sealed record OutputDuplicateGroup
{
    /// <summary>Why the members were grouped, for example <c>same-title-and-company</c>.</summary>
    public required IReadOnlyList<string> Reasons { get; init; }

    public required int Count { get; init; }
    public IReadOnlyList<OutputDuplicateMember> Members { get; init; } = [];
}

public sealed record OutputDuplicateMember
{
    public required string Source { get; init; }
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public string? Company { get; init; }
    public required string Url { get; init; }
}
