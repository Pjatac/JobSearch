using Microsoft.Extensions.Logging;

namespace JobWatcher.Http;

public static class HttpRequestRetryPolicy
{
    private const int MaxAttempts = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public static Task<HttpResponseMessage> GetAsync(
        HttpClient client,
        string url,
        ILogger logger,
        string sourceName,
        CancellationToken cancellationToken,
        TimeSpan? retryDelay = null)
    {
        return SendAsync(
            client,
            () => new HttpRequestMessage(HttpMethod.Get, url),
            logger,
            sourceName,
            $"GET {url}",
            cancellationToken,
            retryDelay);
    }

    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        Func<HttpRequestMessage> createRequest,
        ILogger logger,
        string sourceName,
        string operation,
        CancellationToken cancellationToken,
        TimeSpan? retryDelay = null)
    {
        var effectiveRetryDelay = retryDelay ?? RetryDelay;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var request = createRequest();
                return await client.SendAsync(request, cancellationToken);
            }
            catch (Exception exception) when (IsTransientRequestTimeout(exception, cancellationToken) && attempt < MaxAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Source {Source}: {Operation} timed out or was cancelled internally on attempt {Attempt}/{MaxAttempts}; retrying in {RetryDelaySeconds}s.",
                    sourceName,
                    operation,
                    attempt,
                    MaxAttempts,
                    effectiveRetryDelay.TotalSeconds);
                await Task.Delay(effectiveRetryDelay, cancellationToken);
            }
            catch (Exception exception) when (IsTransientRequestTimeout(exception, cancellationToken))
            {
                throw new HttpRequestTimeoutException(
                    $"Source {sourceName}: {operation} timed out or was cancelled internally after {MaxAttempts} attempts.",
                    exception);
            }
        }

        throw new InvalidOperationException("Unreachable HTTP retry state.");
    }

    private static bool IsTransientRequestTimeout(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is OperationCanceledException or TimeoutException ||
            exception.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("session timeout", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class HttpRequestTimeoutException(string message, Exception? innerException = null) : TimeoutException(message, innerException);
