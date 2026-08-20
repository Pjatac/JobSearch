using System.Text.RegularExpressions;

namespace JobWatcher.Utilities;

public static partial class FileNames
{
    public static string ToSafeName(string value)
    {
        return UnsafeFileNameChars().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
    }

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeFileNameChars();
}
