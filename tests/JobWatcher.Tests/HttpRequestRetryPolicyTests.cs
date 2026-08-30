using JobWatcher.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace JobWatcher.Tests;

public sealed class HttpRequestRetryPolicyTests
{
    [Fact]
    public async Task RetriesOnceAfterInternalCancellation()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHandler((_, _) =>
        {
            calls++;
            return calls == 1
                ? throw new TaskCanceledException("simulated timeout")
                : new HttpResponseMessage(HttpStatusCode.OK);
        }));

        using var response = await HttpRequestRetryPolicy.GetAsync(
            client,
            "https://example.test/jobs",
            NullLogger.Instance,
            "Example",
            CancellationToken.None,
            TimeSpan.Zero);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task DoesNotRetryHttpFailureResponses()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHandler((_, _) =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }));

        using var response = await HttpRequestRetryPolicy.GetAsync(
            client,
            "https://example.test/jobs",
            NullLogger.Instance,
            "Example",
            CancellationToken.None,
            TimeSpan.Zero);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RetriesOnceAfterSessionTimeoutException()
    {
        var calls = 0;
        using var client = new HttpClient(new StubHandler((_, _) =>
        {
            calls++;
            return calls == 1
                ? throw new InvalidOperationException("The request exceeded the 00:00:30 session timeout.")
                : new HttpResponseMessage(HttpStatusCode.OK);
        }));

        using var response = await HttpRequestRetryPolicy.GetAsync(
            client,
            "https://example.test/jobs",
            NullLogger.Instance,
            "Example",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task PreservesExternalCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var client = new HttpClient(new StubHandler((_, token) => throw new OperationCanceledException(token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HttpRequestRetryPolicy.GetAsync(
            client,
            "https://example.test/jobs",
            NullLogger.Instance,
            "Example",
            cts.Token));
    }

    [Fact]
    public async Task PreservesCancellationDuringRetryDelay()
    {
        var calls = 0;
        using var cts = new CancellationTokenSource();
        using var client = new HttpClient(new StubHandler((_, _) =>
        {
            calls++;
            cts.Cancel();
            throw new TaskCanceledException("simulated timeout");
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HttpRequestRetryPolicy.GetAsync(
            client,
            "https://example.test/jobs",
            NullLogger.Instance,
            "Example",
            cts.Token,
            TimeSpan.FromSeconds(5)));

        Assert.Equal(1, calls);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(send(request, cancellationToken));
        }
    }
}
