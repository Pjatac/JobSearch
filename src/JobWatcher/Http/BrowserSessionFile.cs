using System.Net;

namespace JobWatcher.Http;

/// <summary>
/// An operator-supplied browser session: the raw request-header block copied out of a real
/// browser's DevTools Network tab ("view source" under Request Headers).
/// </summary>
/// <remarks>
/// <para>
/// The whole block is pasted verbatim rather than having individual values extracted by hand. The
/// <c>Cookie</c> header is long and is truncated in the DevTools display, so hand-extraction is
/// the step most likely to go wrong.
/// </para>
/// <para>
/// Cloudflare binds a <c>cf_clearance</c> cookie to the User-Agent of the browser that solved the
/// challenge, so the User-Agent travels with the cookies and is applied alongside them.
/// Accept-Language is taken from the same block so the response language matches the browser's.
/// </para>
/// <para>
/// The file holds a live session and is treated as a secret: it is never logged, and only cookie
/// names — never values — appear in log output.
/// </para>
/// </remarks>
public sealed record BrowserSessionFile
{
    public string? UserAgent { get; init; }
    public string? AcceptLanguage { get; init; }

    /// <summary>The raw <c>Cookie</c> request header value: <c>name=value; name=value</c>.</summary>
    public string? Cookie { get; init; }

    public static string GetDefaultPath(string dataDirectory)
    {
        return Path.Combine(dataDirectory, "secrets", "glassdoor-session.txt");
    }

    public static BrowserSessionFile? Load(string path)
    {
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
    }

    /// <summary>
    /// Reads a raw request-header block. Returns <c>null</c> when it carries no cookies, since a
    /// session without them is the anonymous case the caller already handles.
    /// </summary>
    public static BrowserSessionFile? Parse(string rawHeaders)
    {
        string? userAgent = null;
        string? acceptLanguage = null;
        string? cookie = null;

        var lines = rawHeaders.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = StripCurlSyntax(lines[index].Trim());

            // HTTP/2 pseudo-headers (":authority", ":method", ...) start with the separator.
            if (trimmed.Length == 0 || trimmed[0] == ':' || trimmed[0] == '#')
            {
                continue;
            }

            var separator = trimmed.IndexOf(':', StringComparison.Ordinal);
            var tabSeparator = trimmed.IndexOf('\t', StringComparison.Ordinal);
            string name;
            string value;
            if (separator > 0)
            {
                name = trimmed[..separator].Trim();
                value = trimmed[(separator + 1)..].Trim();
            }
            else if (tabSeparator > 0)
            {
                name = trimmed[..tabSeparator].Trim();
                value = trimmed[(tabSeparator + 1)..].Trim();
            }
            else if (TrySplitWhitespaceSeparatedSessionHeader(trimmed, out name, out value))
            {
            }
            else if (IsSessionHeaderName(trimmed) && index + 1 < lines.Length)
            {
                name = trimmed;
                value = lines[++index].Trim();
            }
            else
            {
                continue;
            }

            if (value.Length == 0)
            {
                continue;
            }

            switch (name.ToLowerInvariant())
            {
                case "cookie":
                    cookie = value;
                    break;
                case "user-agent":
                    userAgent = value;
                    break;
                case "accept-language":
                    acceptLanguage = value;
                    break;
            }
        }

        return string.IsNullOrWhiteSpace(cookie)
            ? null
            : new BrowserSessionFile { UserAgent = userAgent, AcceptLanguage = acceptLanguage, Cookie = cookie };
    }

    private static bool IsSessionHeaderName(string name)
    {
        return name.Equals("cookie", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("user-agent", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("accept-language", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TrySplitWhitespaceSeparatedSessionHeader(string line, out string name, out string value)
    {
        var separator = line.IndexOfAny([' ', '\t']);
        if (separator <= 0)
        {
            name = string.Empty;
            value = string.Empty;
            return false;
        }

        name = line[..separator].Trim();
        value = line[separator..].Trim();
        return value.Length > 0 && IsSessionHeaderName(name);
    }

    /// <summary>
    /// Reduces one line of a copied cURL command to a bare <c>name: value</c> header.
    /// </summary>
    /// <remarks>
    /// DevTools no longer offers a "view source" toggle for request headers in current Chrome, so
    /// "Copy as cURL" is often the practical way to export them. Accepting that shape too means
    /// whichever menu item is available works, with no hand-editing.
    /// </remarks>
    private static string StripCurlSyntax(string line)
    {
        // Line continuations: "\" in bash, "^" in cmd.
        var value = line.TrimEnd('\\', '^', ' ');

        foreach (var flag in (string[])["-H ", "--header ", "-b ", "--cookie "])
        {
            if (!value.StartsWith(flag, StringComparison.Ordinal))
            {
                continue;
            }

            value = value[flag.Length..].Trim();

            // cmd escapes embedded quotes as ^"
            value = value.Replace("^\"", "\"", StringComparison.Ordinal);

            if (value.Length >= 2 && (value[0] == '\'' || value[0] == '"') && value[^1] == value[0])
            {
                value = value[1..^1];
            }

            // "-b" and "--cookie" carry the cookie value without a header name.
            if (flag is "-b " or "--cookie " && !value.StartsWith("cookie:", StringComparison.OrdinalIgnoreCase))
            {
                value = $"cookie: {value}";
            }

            return value.Trim();
        }

        return value;
    }

    /// <summary>
    /// Parses the raw header value into <paramref name="container"/> for <paramref name="domain"/>.
    /// Returns the cookie names that were loaded, for logging without disclosing values.
    /// </summary>
    /// <param name="skippedNames">Names of cookies the container refused, for diagnostics.</param>
    public IReadOnlyList<string> SeedCookies(CookieContainer container, string domain, out IReadOnlyList<string> skippedNames)
    {
        var skipped = new List<string>();
        skippedNames = skipped;

        if (string.IsNullOrWhiteSpace(Cookie))
        {
            return [];
        }

        var names = new List<string>();
        foreach (var pair in Cookie.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var name = pair[..separator].Trim();
            var value = pair[(separator + 1)..].Trim();
            if (name.Length == 0)
            {
                continue;
            }

            // Real cookie jars carry values that CookieContainer rejects — g_state, for one, holds
            // raw JSON with quotes and commas. Such a cookie is skipped rather than allowed to
            // take down startup, since the ones that matter for access are plain tokens.
            try
            {
                container.Add(new Cookie(name, value, "/", domain));
                names.Add(name);
            }
            catch (CookieException)
            {
                skipped.Add(name);
            }
        }

        return names;
    }
}
