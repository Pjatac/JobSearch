using JobWatcher.Utilities;

namespace JobWatcher.Tests;

public sealed class LogFileRetentionTests
{
    [Fact]
    public void DeletesOnlyMatchingFilesOlderThanRetention()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"jobwatcher-log-retention-{Guid.NewGuid():N}")).FullName;
        try
        {
            var now = new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);
            var oldRunLog = Path.Combine(directory, "run-20260829T080000000Z.log");
            var recentRunLog = Path.Combine(directory, "run-20260830T083000000Z.log");
            var oldOtherLog = Path.Combine(directory, "other-20260829T080000000Z.log");
            File.WriteAllText(oldRunLog, "old");
            File.WriteAllText(recentRunLog, "recent");
            File.WriteAllText(oldOtherLog, "other");
            File.SetLastWriteTimeUtc(oldRunLog, now.AddHours(-25).UtcDateTime);
            File.SetLastWriteTimeUtc(recentRunLog, now.AddMinutes(-30).UtcDateTime);
            File.SetLastWriteTimeUtc(oldOtherLog, now.AddHours(-25).UtcDateTime);

            LogFileRetention.DeleteOlderThan(directory, "run-*.log", now, TimeSpan.FromHours(24));

            Assert.False(File.Exists(oldRunLog));
            Assert.True(File.Exists(recentRunLog));
            Assert.True(File.Exists(oldOtherLog));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
