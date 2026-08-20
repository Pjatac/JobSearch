using JobWatcher.Sources.Glassdoor;

namespace JobWatcher.Tests;

public sealed class GlassdoorSearchUrlTests
{
    private const string ConfiguredUrl =
        "https://www.glassdoor.com/Job/kfar-saba-backend-developer-jobs-SRCH_IL.0,9_IC4507116_KO10,27.htm";

    [Fact]
    public void DecodesTheSearchParametersEncodedInTheSeoRoute()
    {
        var search = GlassdoorSearchUrl.TryParse(ConfiguredUrl);

        Assert.NotNull(search);
        Assert.Equal("kfar-saba-backend-developer-jobs", search.SeoFriendlyUrlInput);
        Assert.Equal("IL.0,9_IC4507116_KO10,27", search.ParameterUrlInput);
        Assert.Equal(4507116, search.LocationId);
        Assert.Equal("CITY", search.LocationType);
        Assert.Equal("C", search.LocationTypeCode);
    }

    [Fact]
    public void ReadsTheKeywordFromTheCharacterRangeInTheSlug()
    {
        // KO10,27 addresses "backend-developer" inside "kfar-saba-backend-developer-jobs".
        var search = GlassdoorSearchUrl.TryParse(ConfiguredUrl);

        Assert.Equal("backend developer", search!.Keyword);
    }

    [Fact]
    public void ReproducesTheQueryStringGlassdoorSends()
    {
        // Glassdoor double-encodes its own keyword, producing %2520. Replayed as the site sends it.
        var search = GlassdoorSearchUrl.TryParse(ConfiguredUrl);

        Assert.Equal("locId=4507116&locT=C&sc.keyword=backend%2520developer", search!.BuildQueryString());
        Assert.Equal(
            $"{ConfiguredUrl}?locId=4507116&locT=C&sc.keyword=backend%20developer",
            search.BuildOriginalPageUrl());
    }

    [Theory]
    [InlineData("https://www.glassdoor.com/Job/jobs.htm?sc.occupationParam=.Net")]
    [InlineData("https://www.glassdoor.com/Job/kfar-saba-jobs-SRCH_IL.0,9.htm")]
    [InlineData("not a url")]
    public void ReturnsNullWhenTheRouteCarriesNoSearchParameters(string url)
    {
        Assert.Null(GlassdoorSearchUrl.TryParse(url));
    }
}
