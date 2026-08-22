namespace JobWatcher.Utilities;

public static class DiagnosticFileWriter
{
    public static async Task<string> WriteLatestAsync(
        string dataDirectory,
        string sourceName,
        DateTimeOffset collectedAtUtc,
        string extension,
        string content,
        CancellationToken cancellationToken)
    {
        var diagnosticsDirectory = Path.Combine(dataDirectory, "diagnostics");
        Directory.CreateDirectory(diagnosticsDirectory);

        var safeSourceName = FileNames.ToSafeName(sourceName);
        var path = Path.Combine(diagnosticsDirectory, $"{safeSourceName}-{collectedAtUtc:yyyyMMddTHHmmssZ}.{extension}");
        await File.WriteAllTextAsync(path, content, cancellationToken);
        DeleteOlderDiagnostics(diagnosticsDirectory, safeSourceName, path);
        return path;
    }

    private static void DeleteOlderDiagnostics(string diagnosticsDirectory, string safeSourceName, string currentPath)
    {
        foreach (var path in Directory.EnumerateFiles(diagnosticsDirectory, $"{safeSourceName}-*.*"))
        {
            if (string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A file open in an editor can remain until the next diagnostic is written.
            }
            catch (UnauthorizedAccessException)
            {
                // Diagnostics must not make collection fail because an old file cannot be deleted.
            }
        }
    }
}
