using JobWatcher.Utilities;

namespace JobWatcher.Tests;

public sealed class DiagnosticFileWriterTests
{
    [Fact]
    public async Task NewDiagnosticReplacesOlderDiagnosticsForTheSameSource()
    {
        using var temp = new TempDirectory();
        var firstPath = await DiagnosticFileWriter.WriteLatestAsync(
            temp.Path,
            "Example Source",
            new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero),
            "html",
            "first response",
            CancellationToken.None);
        var otherSourcePath = await DiagnosticFileWriter.WriteLatestAsync(
            temp.Path,
            "Other Source",
            new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero),
            "html",
            "other response",
            CancellationToken.None);

        var latestPath = await DiagnosticFileWriter.WriteLatestAsync(
            temp.Path,
            "Example Source",
            new DateTimeOffset(2026, 8, 22, 10, 1, 0, TimeSpan.Zero),
            "json",
            "latest response",
            CancellationToken.None);

        Assert.False(File.Exists(firstPath));
        Assert.True(File.Exists(latestPath));
        Assert.True(File.Exists(otherSourcePath));
        Assert.Equal("latest response", await File.ReadAllTextAsync(latestPath));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"jobwatcher-tests-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
