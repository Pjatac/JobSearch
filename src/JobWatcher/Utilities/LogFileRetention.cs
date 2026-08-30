namespace JobWatcher.Utilities;

public static class LogFileRetention
{
    public static void DeleteOlderThan(string directory, string searchPattern, DateTimeOffset now, TimeSpan retention)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        var cutoff = now - retention;
        foreach (var path in Directory.EnumerateFiles(directory, searchPattern))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff.UtcDateTime)
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Log retention must never interfere with the work that produced the log.
            }
        }
    }
}
