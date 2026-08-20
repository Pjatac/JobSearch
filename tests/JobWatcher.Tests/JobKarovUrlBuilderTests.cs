using JobWatcher.Configuration;
using JobWatcher.Sources.JobKarov;

namespace JobWatcher.Tests;

public sealed class JobKarovUrlBuilderTests
{
    [Fact]
    public void UsesExplicitUrlWhenConfigured()
    {
        var options = new JobSourceOptions
        {
            Name = "JobKarov",
            Url = "https://www.jobkarov.com/Search/?speciality=2119"
        };

        var url = JobKarovUrlBuilder.Build(options);

        Assert.Equal("https://www.jobkarov.com/Search/?speciality=2119", url);
    }

    [Fact]
    public void BuildsUrlFromJobKarovFilter()
    {
        var options = new JobSourceOptions
        {
            Name = "JobKarov-Software",
            JobKarovFilter = new JobKarovFilterOptions
            {
                Speciality = "2119",
                Roles = ["3893", "2163", "2155", "3131", "2177"],
                Areas = ["50", "70"],
                Size = 2
            }
        };

        var url = JobKarovUrlBuilder.Build(options);

        Assert.Equal("https://www.jobkarov.com/Search/?speciality=2119&role=3893%2c2163%2c2155%2c3131%2c2177&area=50%2c70&size=2", url);
    }
}
