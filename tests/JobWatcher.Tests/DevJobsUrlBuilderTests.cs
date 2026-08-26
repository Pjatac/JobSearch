using JobWatcher.Configuration;
using JobWatcher.Sources.DevJobs;

namespace JobWatcher.Tests;

public sealed class DevJobsUrlBuilderTests
{
    [Fact]
    public void AddsRequestedPageToRelativeSearchUrl()
    {
        var url = DevJobsUrlBuilder.Build(new DevJobsFilterOptions
        {
            BaseUrl = "https://devjobs.co.il",
            SearchUrl = "/jobs-grid?developerTypes=Backend&district=Hasharon"
        }, 2);

        Assert.Equal("https://devjobs.co.il/jobs-grid?developerTypes=Backend&district=Hasharon&page=2", url);
    }

    [Fact]
    public void ReplacesExistingPageInSearchUrl()
    {
        var url = DevJobsUrlBuilder.Build(new DevJobsFilterOptions
        {
            BaseUrl = "https://devjobs.co.il",
            SearchUrl = "https://devjobs.co.il/jobs-grid?developerTypes=Backend&page=7"
        }, 1);

        Assert.Equal("https://devjobs.co.il/jobs-grid?developerTypes=Backend&page=1", url);
    }
}
