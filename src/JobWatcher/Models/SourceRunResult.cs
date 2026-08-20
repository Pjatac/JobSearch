namespace JobWatcher.Models;

public sealed record SourceRunResult
{
    public required string Source { get; init; }
    public required bool Success { get; init; }
    public SourceSnapshot? Snapshot { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string? Error { get; init; }
}
