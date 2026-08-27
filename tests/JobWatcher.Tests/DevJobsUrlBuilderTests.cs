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

    [Fact]
    public void BuildsSearchUrlFromStructuredDeveloperTypesAndLegacyDistrict()
    {
        var url = DevJobsUrlBuilder.Build(new DevJobsFilterOptions
        {
            BaseUrl = "https://devjobs.co.il",
            DeveloperTypes = ["Backend", "Full Stack"],
            District = "Hasharon"
        }, 2);

        Assert.Equal("https://devjobs.co.il/jobs-grid?developerTypes=Backend%2cFull+Stack&district=Hasharon&page=2", url);
    }

    [Fact]
    public void CreatesOneScopeForEachSelectedDistrictAndCity()
    {
        var filter = new DevJobsFilterOptions
        {
            Districts = ["Hasharon", "Tel Aviv & Center"],
            Cities = ["Herzliya", "Hod HaSharon"]
        };

        var scopes = DevJobsUrlBuilder.GetSearchScopes(filter);

        Assert.Equal(
        [
            new DevJobsSearchScope("Hasharon", null),
            new DevJobsSearchScope("Tel Aviv & Center", null),
            new DevJobsSearchScope(null, "Herzliya"),
            new DevJobsSearchScope(null, "Hod HaSharon")
        ],
        scopes);
    }

    [Fact]
    public void BuildsSearchUrlForCityScope()
    {
        var url = DevJobsUrlBuilder.Build(new DevJobsFilterOptions
        {
            BaseUrl = "https://devjobs.co.il",
            DeveloperTypes = ["Backend"]
        }, 1, new DevJobsSearchScope(null, "Hod HaSharon"));

        Assert.Equal("https://devjobs.co.il/jobs-grid?developerTypes=Backend&city=Hod+HaSharon&page=1", url);
    }
}
