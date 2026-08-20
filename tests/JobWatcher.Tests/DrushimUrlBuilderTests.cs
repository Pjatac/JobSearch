using JobWatcher.Configuration;
using JobWatcher.Sources.Drushim;

namespace JobWatcher.Tests;

public sealed class DrushimUrlBuilderTests
{
    [Fact]
    public void UsesExplicitUrlWhenConfigured()
    {
        var options = new JobSourceOptions
        {
            Name = "Drushim",
            Url = "https://www.drushim.co.il/jobs/cat6/?ssaen=3"
        };

        var url = DrushimUrlBuilder.Build(options);

        Assert.Equal("https://www.drushim.co.il/jobs/cat6/?ssaen=3", url);
    }

    [Fact]
    public void BuildsCategoryUrlWithExperience()
    {
        var options = new JobSourceOptions
        {
            Name = "Drushim-Software",
            DrushimFilter = new DrushimFilterOptions
            {
                CategoryId = 6,
                Experience = 3,
                IncludeAreaAround = false,
                Range = null
            }
        };

        var url = DrushimUrlBuilder.Build(options);

        Assert.Equal("https://www.drushim.co.il/jobs/cat6/?ssaen=3", url);
    }

    [Fact]
    public void BuildsSubcategoryAreaUrl()
    {
        var options = new JobSourceOptions
        {
            Name = "Drushim-Backend",
            DrushimFilter = new DrushimFilterOptions
            {
                CategoryId = 6,
                SubcategoryId = 616,
                AreaIds = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14],
                GeoLexId = 539071,
                IncludeAreaAround = true,
                Experience = 3,
                Range = 3
            }
        };

        var url = DrushimUrlBuilder.Build(options);

        Assert.Equal("https://www.drushim.co.il/jobs/subcat/616/area/1-2-3-4-5-6-7-8-9-10-11-12-13-14/?catdir=6&geolexid=539071&isaa=true&ssaen=3&range=3", url);
    }

    [Fact]
    public void BuildsCombinedSubcategoryUrl()
    {
        var options = new JobSourceOptions
        {
            Name = "Drushim-SoftwareRoles",
            DrushimFilter = new DrushimFilterOptions
            {
                CategoryId = 6,
                SubcategoryIds = [69, 183, 372, 380, 616],
                AreaIds = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14],
                ExperienceRange = "2-3-4",
                Scopes = [1],
                GeoLexId = 539071,
                IncludeAreaAround = true,
                Experience = 3,
                Range = 3
            }
        };

        var url = DrushimUrlBuilder.Build(options);

        Assert.Equal("https://www.drushim.co.il/jobs/subcat/69-183-372-380-616/area/1-2-3-4-5-6-7-8-9-10-11-12-13-14/?catdir=6&scope=1&experience=2-3-4&geolexid=539071&isaa=true&ssaen=3&range=3", url);
    }

    [Fact]
    public void BuildsApiSearchUrl()
    {
        var options = new JobSourceOptions
        {
            Name = "Drushim-SoftwareRoles",
            DrushimFilter = new DrushimFilterOptions
            {
                CategoryId = 6,
                SubcategoryIds = [69, 183, 372, 380, 616],
                AreaIds = [1, 2, 3],
                ExperienceRange = "2-3-4",
                Scopes = [1],
                GeoLexId = 539071,
                IncludeAreaAround = true,
                Experience = 3,
                Range = 3
            }
        };

        var url = DrushimUrlBuilder.BuildApiSearch(options, page: 2);

        Assert.Equal("https://www.drushim.co.il/api/jobs/search?catdir=6&subcat=69-183-372-380-616&area=1-2-3&scope=1&experience=2-3-4&geolexid=539071&isaa=true&ssaen=3&range=3&isAA=true&page=2", url);
    }
}
