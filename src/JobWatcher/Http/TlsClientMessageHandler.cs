using TlsClient;

namespace JobWatcher.Http;

/// <summary>
/// Bridges <see cref="HttpClient"/> onto a browser-fingerprinted <see cref="TlsSession"/>.
/// Sources keep using <see cref="IHttpClientFactory"/>; only the primary handler differs, so the
/// choice of "normal client or TLS-fingerprinted client" stays in composition root wiring.
/// </summary>
public sealed class TlsClientMessageHandler : HttpMessageHandler
{
    // Response headers that describe the wire encoding. TlsSession already decoded the body, so
    // forwarding them would describe the payload incorrectly.
    private static readonly string[] WireEncodingHeaders = ["content-length", "content-encoding", "transfer-encoding"];

    private readonly TlsSession _session;

    public TlsClientMessageHandler(TlsPreset preset)
        : this(new TlsSessionOptions(preset))
    {
    }

    public TlsClientMessageHandler(TlsSessionOptions options)
    {
        _session = new TlsSession(options);
    }

    /// <summary>The session cookie jar, for seeding an operator-exported browser session.</summary>
    public System.Net.CookieContainer Cookies => _session.Cookies;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // HttpClient defaults every request to HTTP/1.1 with RequestVersionOrLower, which pins the
        // connection to HTTP/1.1. A browser TLS fingerprint negotiating HTTP/1.1 is the exact
        // mismatch anti-bot services look for, so the request is normalised to what the emulated
        // browser would actually send.
        request.Version = System.Net.HttpVersion.Version20;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

        var tlsResponse = await _session.SendAsync(request, cancellationToken);

        var response = new HttpResponseMessage(tlsResponse.StatusCode)
        {
            ReasonPhrase = tlsResponse.ReasonPhrase,
            Version = tlsResponse.HttpVersion,
            RequestMessage = request,
            Content = new ByteArrayContent(tlsResponse.Body.ToArray())
        };

        // ByteArrayContent seeds its own Content-Type/Length; drop them so the server values win.
        response.Content.Headers.Clear();

        foreach (var (name, values) in tlsResponse.Headers)
        {
            if (WireEncodingHeaders.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!response.Headers.TryAddWithoutValidation(name, values))
            {
                response.Content.Headers.TryAddWithoutValidation(name, values);
            }
        }

        return response;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _session.Dispose();
        }

        base.Dispose(disposing);
    }
}
