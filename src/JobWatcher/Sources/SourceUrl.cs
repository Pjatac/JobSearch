namespace JobWatcher.Sources;

internal static class SourceUrl
{
    public static bool TryCreateHttpAbsolute(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed) &&
            (string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

    public static string ToAbsoluteHttpUrl(Uri baseUri, string value)
    {
        return TryCreateHttpAbsolute(value, out var absolute)
            ? absolute.ToString()
            : new Uri(baseUri, value.TrimStart('/')).ToString();
    }
}
