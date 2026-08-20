using JobWatcher.Sources.Glassdoor;

namespace JobWatcher.Tests;

public sealed class GlassdoorApiParserTests
{
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private readonly GlassdoorApiParser _parser = new();

    // Trimmed from a real jobSearchResultsQuery response.
    private const string Response = """
    {
      "data": {
        "jobListings": {
          "jobListings": [
            {
              "jobview": {
                "header": {
                  "ageInDays": 10,
                  "employer": { "id": 0, "name": null },
                  "employerNameFromSearch": "Insert Technologhy",
                  "jobTitleText": "Backend Developer",
                  "locationName": "Israel",
                  "seoJobLink": "https://www.glassdoor.com/job-listing/backend-developer-insert-technologhy-JV_KO0,17_KE18,36.htm?jl=1009200608505"
                },
                "job": {
                  "descriptionFragmentsText": [
                    "This is a key role at the heart of our technological infrastructure…"
                  ],
                  "jobTitleText": "Backend Developer",
                  "listingId": 1009200608505
                }
              }
            },
            {
              "jobview": {
                "header": {
                  "ageInDays": 154,
                  "employer": { "id": 2365676, "name": "Comet" },
                  "employerNameFromSearch": "Comet ML",
                  "jobTitleText": "Full Stack Engineer",
                  "locationName": "Israel",
                  "seoJobLink": "https://www.glassdoor.com/job-listing/full-stack-engineer-comet-ny-JV_KO0,19_KE20,28.htm?jl=1010024608643"
                },
                "job": {
                  "descriptionFragmentsText": ["Build robust APIs and backend services."],
                  "listingId": 1010024608643
                }
              }
            }
          ],
          "paginationCursors": [
            { "cursor": "AB4AAIEAAAA", "pageNumber": 1 },
            { "cursor": "AB4AAoEAPAA", "pageNumber": 3 }
          ],
          "totalJobsCount": 251
        }
      }
    }
    """;

    [Fact]
    public void ParsesListingsFromTheSearchApi()
    {
        var result = _parser.Parse(Response, "Glassdoor", CollectedAt);

        Assert.Empty(result.Warnings);
        Assert.Equal(251, result.TotalJobs);
        Assert.Equal(2, result.Vacancies.Count);

        var vacancy = result.Vacancies.Single(v => v.ExternalId == "1009200608505");
        Assert.Equal("Backend Developer", vacancy.Title);
        Assert.Equal("Insert Technologhy", vacancy.Company);
        Assert.Equal("Israel", vacancy.Location);
        Assert.Equal("https://www.glassdoor.com/job-listing/backend-developer-insert-technologhy-JV_KO0,17_KE18,36.htm?jl=1009200608505", vacancy.Url);
        Assert.Equal("This is a key role at the heart of our technological infrastructure...", vacancy.Description);
    }

    [Fact]
    public void DerivesAnExactPostingDateFromAgeInDays()
    {
        // The rendered card only says "30d+"; the API gives the real number.
        var result = _parser.Parse(Response, "Glassdoor", CollectedAt);

        Assert.Equal(new DateOnly(2026, 7, 29), result.Vacancies.Single(v => v.ExternalId == "1009200608505").DatePosted);
        Assert.Equal(new DateOnly(2026, 3, 7), result.Vacancies.Single(v => v.ExternalId == "1010024608643").DatePosted);
    }

    [Fact]
    public void PrefersTheSearchEmployerNameOverTheProfileName()
    {
        // employerNameFromSearch is what the card shows; employer.name is the Glassdoor profile.
        var result = _parser.Parse(Response, "Glassdoor", CollectedAt);

        Assert.Equal("Comet ML", result.Vacancies.Single(v => v.ExternalId == "1010024608643").Company);
    }

    [Fact]
    public void ReadsPaginationCursors()
    {
        var result = _parser.Parse(Response, "Glassdoor", CollectedAt);

        Assert.Equal(2, result.Cursors.Count);
        Assert.Equal("AB4AAIEAAAA", result.Cursors[1]);
        Assert.Equal("AB4AAoEAPAA", result.Cursors[3]);
    }

    [Fact]
    public void ReportsMalformedJsonAsAWarningInsteadOfThrowing()
    {
        var result = _parser.Parse("{ not json", "Glassdoor", CollectedAt);

        Assert.Empty(result.Vacancies);
        Assert.Contains(result.Warnings, w => w.Contains("Malformed", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsAResponseWithoutJobListings()
    {
        var result = _parser.Parse("""{ "data": {} }""", "Glassdoor", CollectedAt);

        Assert.Empty(result.Vacancies);
        Assert.Contains(result.Warnings, w => w.Contains("no data.jobListings", StringComparison.Ordinal));
    }

    [Fact]
    public void SkipsListingsMissingIdentityAndWarns()
    {
        const string json = """
        {
          "data": { "jobListings": { "jobListings": [
            { "jobview": { "header": { "jobTitleText": "No link" }, "job": { "listingId": 1 } } }
          ] } }
        }
        """;

        var result = _parser.Parse(json, "Glassdoor", CollectedAt);

        Assert.Empty(result.Vacancies);
        Assert.Contains(result.Warnings, w => w.Contains("Skipped 1", StringComparison.Ordinal));
    }
}
