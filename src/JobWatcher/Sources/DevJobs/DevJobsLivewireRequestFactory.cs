using System.Text.Json;
using System.Net.Http.Json;

namespace JobWatcher.Sources.DevJobs;

public static class DevJobsLivewireRequestFactory
{
    public static HttpRequestMessage CreateSearchRequest(Uri endpoint, DevJobsLivewireSession session, string nameFilter)
    {
        var updates = new Dictionary<string, object?> { ["nameFilter"] = nameFilter };
        return CreateRequest(endpoint, session, updates, [new DevJobsLivewireCall("searchNameText", [])]);
    }

    public static HttpRequestMessage CreatePageRequest(Uri endpoint, DevJobsLivewireSession session, int page)
    {
        return CreateRequest(endpoint, session, new Dictionary<string, object?>(), [new DevJobsLivewireCall("gotoPage", [page, "page"])]);
    }

    private static HttpRequestMessage CreateRequest(Uri endpoint, DevJobsLivewireSession session, IReadOnlyDictionary<string, object?> updates, IReadOnlyList<DevJobsLivewireCall> calls)
    {
        var payload = new
        {
            _token = session.CsrfToken,
            components = new[]
            {
                new
                {
                    snapshot = session.Snapshot,
                    updates,
                    calls = calls.Select(call => new { path = string.Empty, method = call.Method, @params = call.Parameters })
                }
            }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-Livewire", "true");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return request;
    }
}

public sealed record DevJobsLivewireCall(string Method, IReadOnlyList<object> Parameters);
