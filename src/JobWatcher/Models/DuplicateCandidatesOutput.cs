namespace JobWatcher.Models;

public sealed record DuplicateCandidatesOutput
{
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public int CandidateCount { get; init; }
    public IReadOnlyList<DuplicateCandidate> Candidates { get; init; } = [];
}

public sealed record DuplicateCandidate
{
    public required double Score { get; init; }
    public IReadOnlyList<string> Reasons { get; init; } = [];
    public required DuplicateCandidateVacancy Left { get; init; }
    public required DuplicateCandidateVacancy Right { get; init; }
}

public sealed record DuplicateCandidateVacancy
{
    public required string Source { get; init; }
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public string? Company { get; init; }
    public string? Location { get; init; }
    public required string Url { get; init; }
}
