using System.Text.Json;
using JobWatcher.Sources.DevJobs;

namespace JobWatcher.Tests;

public sealed class DevJobsLivewireRequestFactoryTests
{
    private static readonly Uri Endpoint = new("https://devjobs.co.il/livewire/update");
    private static readonly DevJobsLivewireSession Session = new("test-token", "{\"memo\":{\"name\":\"find-job\"}}");

    [Fact]
    public async Task CreatesSearchNameTextRequest()
    {
        using var request = DevJobsLivewireRequestFactory.CreateSearchRequest(Endpoint, Session, ".NET");
        using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        var component = json.RootElement.GetProperty("components")[0];

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("true", Assert.Single(request.Headers.GetValues("X-Livewire")));
        Assert.Equal("test-token", json.RootElement.GetProperty("_token").GetString());
        Assert.Equal(".NET", component.GetProperty("updates").GetProperty("nameFilter").GetString());
        Assert.Equal("searchNameText", component.GetProperty("calls")[0].GetProperty("method").GetString());
    }

    [Fact]
    public async Task CreatesGotoPageRequest()
    {
        using var request = DevJobsLivewireRequestFactory.CreatePageRequest(Endpoint, Session, 3);
        using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        var call = json.RootElement.GetProperty("components")[0].GetProperty("calls")[0];

        Assert.Equal("gotoPage", call.GetProperty("method").GetString());
        Assert.Equal(3, call.GetProperty("params")[0].GetInt32());
        Assert.Equal("page", call.GetProperty("params")[1].GetString());
    }
}
