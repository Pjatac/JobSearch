using JobWatcher.Configuration;
using JobWatcher.Sources.SecretTelAviv;

namespace JobWatcher.Tests;

public sealed class SecretTelAvivUrlBuilderTests
{
    [Fact]
    public void BuildsAbsoluteSearchUrlFromBaseUrlAndRelativePath()
    {
        var url = SecretTelAvivUrlBuilder.Build(new SecretTelAvivFilterOptions
        {
            BaseUrl = "https://jobs.secrettelaviv.com",
            SearchUrl = "/list/find/?query=Back+End"
        });

        Assert.Equal("https://jobs.secrettelaviv.com/list/find/?query=Back+End", url);
    }

    [Fact]
    public void KeepsExistingAbsoluteSearchUrl()
    {
        var url = SecretTelAvivUrlBuilder.Build(new SecretTelAvivFilterOptions
        {
            BaseUrl = "https://jobs.secrettelaviv.com",
            SearchUrl = "https://jobs.secrettelaviv.com/list/find/?query=Back+End"
        });

        Assert.Equal("https://jobs.secrettelaviv.com/list/find/?query=Back+End", url);
    }
}
