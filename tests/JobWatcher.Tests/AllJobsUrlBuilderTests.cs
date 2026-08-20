using JobWatcher.Configuration;
using JobWatcher.Sources.AllJobs;

namespace JobWatcher.Tests;

public sealed class AllJobsUrlBuilderTests
{
    [Fact]
    public void UsesExplicitUrlAndReplacesPage()
    {
        var options = new JobSourceOptions
        {
            Name = "AllJobs",
            Url = "https://www.alljobs.co.il/SearchResultsGuest.aspx?page=1&position=1021&type=4&source=714&duration=25&exc=&region="
        };

        var url = AllJobsUrlBuilder.Build(options, page: 2);

        Assert.Equal("https://www.alljobs.co.il/SearchResultsGuest.aspx?page=2&position=1021&type=4&source=714&duration=25&exc=&region=", url);
    }

    [Fact]
    public void BuildsUrlFromSinglePositionFilter()
    {
        var options = new JobSourceOptions
        {
            Name = "AllJobs",
            AllJobsFilter = new AllJobsFilterOptions
            {
                Position = 1021,
                Types = [4],
                Source = 714,
                Duration = 25,
                Exclude = "",
                Region = ""
            }
        };

        var url = AllJobsUrlBuilder.Build(options, page: 3);

        Assert.Equal("https://www.alljobs.co.il/SearchResultsGuest.aspx?page=3&position=1021&type=4&source=714&duration=25&exc=&region=", url);
    }

    [Fact]
    public void BuildsUrlFromMultiPositionFilter()
    {
        var options = new JobSourceOptions
        {
            Name = "AllJobs",
            AllJobsFilter = new AllJobsFilterOptions
            {
                Positions = [1759, 1994, 1152, 1203, 1848],
                Types = [4],
                Duration = 25,
                Exclude = "",
                Region = "2,6"
            }
        };

        var url = AllJobsUrlBuilder.Build(options, page: 1);

        Assert.Equal("https://www.alljobs.co.il/SearchResultsGuest.aspx?page=1&position=1759%2c1994%2c1152%2c1203%2c1848&type=4&source=&duration=25&exc=&region=2%2c6", url);
    }
}
