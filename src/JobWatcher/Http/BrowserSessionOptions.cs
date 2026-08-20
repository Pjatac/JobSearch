using System.Text.RegularExpressions;
using TlsClient;

namespace JobWatcher.Http;

/// <summary>
/// Builds <see cref="TlsSessionOptions"/> whose HTTP headers match the browser the TLS preset
/// emulates.
/// </summary>
/// <remarks>
/// <para>
/// <c>TlsPresets</c> are "TLS and HTTP/2 presets": they cover the handshake and the HTTP/2 wire
/// behaviour, not the request headers. Out of the box a session sends
/// <c>Accept: */*</c> and no client-hint or fetch-metadata headers, which is what a generic HTTP
/// client sends, not what Chrome sends when it navigates to a page. That combination — a Chrome
/// TLS fingerprint carrying non-Chrome headers — is internally inconsistent, and consistency is
/// what anti-bot scoring looks at.
/// </para>
/// <para>
/// The header set below is a Chrome 133 top-level document navigation. It is fixed, not tuned:
/// values are derived from what the emulated browser sends, and header rotation as a way to get
/// past a block is explicitly out of scope.
/// </para>
/// </remarks>
public static partial class BrowserSessionOptions
{
    private const string DefaultChromeMajorVersion = "133";

    // Chrome 133 also advertises zstd, but TlsClient decompresses gzip, deflate and Brotli only.
    // Advertising an encoding the client cannot decode would return an unreadable body, so the
    // preset's value is kept.
    private const string AcceptEncoding = "gzip, deflate, br";

    private static readonly string[] ChromeNavigationHeaderOrder =
    [
        "sec-ch-ua",
        "sec-ch-ua-mobile",
        "sec-ch-ua-platform",
        "upgrade-insecure-requests",
        "user-agent",
        "accept",
        "sec-fetch-site",
        "sec-fetch-mode",
        "sec-fetch-user",
        "sec-fetch-dest",
        "accept-encoding",
        "accept-language",
        "priority"
    ];

    /// <summary>
    /// Chrome 133 navigating directly to a URL, as when the address bar is used: hence
    /// <c>sec-fetch-site: none</c> and <c>sec-fetch-user: ?1</c>.
    /// </summary>
    /// <param name="userAgent">
    /// Overrides the preset's User-Agent. Supplied when an operator-exported browser session is in
    /// use, because a Cloudflare <c>cf_clearance</c> cookie is bound to the User-Agent of the
    /// browser that solved the challenge and is rejected if the two disagree.
    /// </param>
    /// <param name="acceptLanguage">
    /// Overrides the default language preference, so responses come back in the same language the
    /// exporting browser would receive.
    /// </param>
    public static TlsSessionOptions Chrome133Navigation(string? userAgent = null, string? acceptLanguage = null)
    {
        var options = new TlsSessionOptions(TlsPresets.Chrome133)
        {
            HeaderOrder = ChromeNavigationHeaderOrder
        };

        // Without an exported session the preset owns User-Agent: overriding it would risk stating
        // a different browser version than the handshake does.
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            options.DefaultHeaders.Set("user-agent", userAgent);
        }

        options.DefaultHeaders.Set("sec-ch-ua", BuildSecChUa(userAgent));
        options.DefaultHeaders.Set("sec-ch-ua-mobile", "?0");
        options.DefaultHeaders.Set("sec-ch-ua-platform", "\"Windows\"");
        options.DefaultHeaders.Set("upgrade-insecure-requests", "1");
        options.DefaultHeaders.Set("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
        options.DefaultHeaders.Set("sec-fetch-site", "none");
        options.DefaultHeaders.Set("sec-fetch-mode", "navigate");
        options.DefaultHeaders.Set("sec-fetch-user", "?1");
        options.DefaultHeaders.Set("sec-fetch-dest", "document");
        options.DefaultHeaders.Set("accept-encoding", AcceptEncoding);
        options.DefaultHeaders.Set("accept-language", string.IsNullOrWhiteSpace(acceptLanguage) ? "en-US,en;q=0.9" : acceptLanguage);
        options.DefaultHeaders.Set("priority", "u=0, i");

        return options;
    }

    /// <summary>
    /// Builds a <c>sec-ch-ua</c> value whose version agrees with <paramref name="userAgent"/>.
    /// An exported session brings its own browser version, and a client hint that contradicts the
    /// User-Agent is exactly the kind of inconsistency this class exists to avoid.
    /// </summary>
    internal static string BuildSecChUa(string? userAgent)
    {
        var match = ChromeVersionRegex().Match(userAgent ?? string.Empty);
        var version = match.Success ? match.Groups["major"].Value : DefaultChromeMajorVersion;

        return $"\"Not(A:Brand\";v=\"99\", \"Google Chrome\";v=\"{version}\", \"Chromium\";v=\"{version}\"";
    }

    [GeneratedRegex(@"Chrome/(?<major>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex ChromeVersionRegex();
}
