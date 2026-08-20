using JobWatcher.Sources.JobKarov;

namespace JobWatcher.Tests;

public sealed class JobKarovJsonLdParserTests
{
    private readonly JobKarovJsonLdParser _parser = new();
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 5, 8, 15, 0, TimeSpan.Zero);

    [Fact]
    public void ParsesItemListFixture()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "jobkarov-itemlist.html"));

        var result = _parser.Parse(html, "JobKarov", CollectedAt);

        Assert.Equal(2, result.Vacancies.Count);
        Assert.Equal(2, result.JobPostingObjectCount);
        Assert.Equal("2712464", result.Vacancies[0].ExternalId);
        Assert.Equal("https://www.jobkarov.com/Search/Site/2712464", result.Vacancies[0].Url);
        Assert.Contains("מפתח", result.Vacancies[0].Title);
        Assert.Contains("Build services", result.Vacancies[0].Description);
        Assert.DoesNotContain("<p>", result.Vacancies[0].Description);
    }

    [Fact]
    public void ParsesDirectRootJobPostingAndArrayType()
    {
        var html = Html("""
        {
          "@type": ["Thing", "JobPosting"],
          "title": "QA Engineer",
          "url": "/Search/Site/111",
          "employmentType": "FULL_TIME"
        }
        """);

        var result = _parser.Parse(html, "JobKarov", CollectedAt);

        var vacancy = Assert.Single(result.Vacancies);
        Assert.Equal("111", vacancy.ExternalId);
        Assert.Equal(["FULL_TIME"], vacancy.EmploymentTypes);
    }

    [Fact]
    public void ParsesMultipleScriptsAndIgnoresMalformedUnrelatedBlock()
    {
        var html = """
        <html><head>
        <script type="application/ld+json">{ this is not json }</script>
        <script type="application/ld+json">{"@type":"JobPosting","title":"DevOps","url":"/Search/Site/222"}</script>
        </head></html>
        """;

        var result = _parser.Parse(html, "JobKarov", CollectedAt);

        Assert.Single(result.Vacancies);
        Assert.Single(result.Warnings);
        Assert.Equal(2, result.JsonLdBlockCount);
    }

    [Fact]
    public void DeduplicatesDuplicateJobPostings()
    {
        var html = Html("""
        [
          {"@type":"JobPosting","title":"First","url":"/Search/Site/333"},
          {"@type":"JobPosting","title":"Second","url":"/Search/Site/333"}
        ]
        """);

        var result = _parser.Parse(html, "JobKarov", CollectedAt);

        Assert.Single(result.Vacancies);
        Assert.Equal(2, result.JobPostingObjectCount);
    }

    [Fact]
    public void AllowsMissingCompanyAndLocation()
    {
        var html = Html("""{"@type":"JobPosting","title":"Support","url":"/Search/Site/444"}""");

        var vacancy = Assert.Single(_parser.Parse(html, "JobKarov", CollectedAt).Vacancies);

        Assert.Null(vacancy.Company);
        Assert.Null(vacancy.Location);
    }

    [Fact]
    public void ExtractsLocationFromCommonAddressShape()
    {
        var html = Html("""
        {
          "@type":"JobPosting",
          "title":"Analyst",
          "url":"/Search/Site/555",
          "hiringOrganization":{"name":"Acme"},
          "jobLocation":{"address":{"addressLocality":"תל אביב","addressRegion":"מרכז"}}
        }
        """);

        var vacancy = Assert.Single(_parser.Parse(html, "JobKarov", CollectedAt).Vacancies);

        Assert.Equal("Acme", vacancy.Company);
        Assert.Equal("תל אביב, מרכז", vacancy.Location);
    }

    private static string Html(string json)
    {
        return $"""<html><head><script type="application/ld+json">{json}</script></head></html>""";
    }
}
