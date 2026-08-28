using Microsoft.Extensions.Logging;

namespace JobWatcher.Http;

public static class HttpRequestRetryPolicy
{
    private const int MaxAttempts = 2;

    public static Task<HttpResponseMessage> GetAsync(
        HttpClient client,
        string url,
        ILogger logger,
        string sourceName,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            client,
            () => new HttpRequestMessage(HttpMethod.Get, url),
            logger,
            sourceName,
            $"GET {url}",
            cancellationToken);
    }

    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        Func<HttpRequestMessage> createRequest,
        ILogger logger,
        string sourceName,
        string operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var request = createRequest();
                return await client.SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < MaxAttempts)
            {
                logger.LogWarning(
                    "Source {Source}: {Operation} timed out or was cancelled internally; retrying once.",
                    sourceName,
                    operation);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new HttpRequestTimeoutException(
                    $"Source {sourceName}: {operation} timed out or was cancelled internally after {MaxAttempts} attempts.");
            }
        }

        throw new InvalidOperationException("Unreachable HTTP retry state.");
    }
}

public sealed class HttpRequestTimeoutException(string message) : TimeoutException(message);
