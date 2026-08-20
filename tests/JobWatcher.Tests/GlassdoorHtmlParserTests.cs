using JobWatcher.Sources.Glassdoor;

namespace JobWatcher.Tests;

public sealed class GlassdoorHtmlParserTests
{
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
    private readonly GlassdoorHtmlParser _parser = new();

    [Fact]
    public void ParsesSearchCards()
    {
        const string html = """
        <html>
          <head><title>247 backend developer Jobs in Kfar Saba, August 2026 | Glassdoor</title></head>
          <body>
            <h1 data-test="search-title">247 Backend developer jobs in Kfar Saba</h1>
            <div class="jobCard JobCard_jobCardContent__JQ5Rq">
              <div class="EmployerProfile_profileContainer__63w3R" id="job-employer-1010144713302">
                <span class="EmployerProfile_compactEmployerName__9MGcV">Centrical</span>
              </div>
              <a class="JobCard_jobTitle__GLyJ1" data-test="job-title" href="https://www.glassdoor.com/job-listing/backend-developer-centrical-JV_KO0,17_KE18,27.htm?jl=1010144713302" id="job-title-1010144713302">Backend Developer</a>
              <div class="JobCard_location__Ds1fM" data-test="emp-location" id="job-location-1010144713302">Israel</div>
              <div class="JobCard_jobDescriptionSnippet__l1tnl" data-test="descSnippet"><div>Server-side developer with 3+ Years of experience.&amp;hellip;</div></div>
            </div>
          </body>
        </html>
        """;

        var result = _parser.Parse(html, "Glassdoor", CollectedAt);

        var vacancy = Assert.Single(result.Vacancies);
        Assert.Empty(result.Warnings);
        Assert.Equal(1, result.JobCardCount);
        Assert.Equal(247, result.TotalJobs);
        Assert.Equal("1010144713302", vacancy.ExternalId);
        Assert.Equal("Backend Developer", vacancy.Title);
        Assert.Equal("Centrical", vacancy.Company);
        Assert.Equal("Israel", vacancy.Location);
        Assert.Equal("https://www.glassdoor.com/job-listing/backend-developer-centrical-JV_KO0,17_KE18,27.htm?jl=1010144713302", vacancy.Url);
        Assert.Equal("Server-side developer with 3+ Years of experience....", vacancy.Description);
    }

    [Fact]
    public void ReadsListingAgeFromOutsideTheInnerCardContainer()
    {
        // Real Glassdoor markup puts job-age as a sibling of JobCard_jobCardContent, not inside
        // it, so anchoring on the nearest "jobCard" class ancestor loses the date.
        const string html = """
        <li data-test="jobListing">
          <div data-test="job-card-wrapper">
            <div class="JobCard_jobCardContent__JQ5Rq">
              <div class="EmployerProfile_profileContainer__63w3R">
                <span class="EmployerProfile_compactEmployerName__9MGcV">Centrical</span>
              </div>
              <a data-test="job-title" href="https://www.glassdoor.com/job-listing/x.htm?jl=1010144713302" id="job-title-1010144713302">Backend Developer</a>
              <div class="JobCard_location__Ds1fM" data-test="emp-location">Israel</div>
              <div class="JobCard_jobDescriptionSnippet__l1tnl" data-test="descSnippet"><div>Server-side developer.</div></div>
            </div>
            <div class="JobCard_listingAge__jJsuc" data-test="job-age">10d</div>
          </div>
        </li>
        """;

        var vacancy = Assert.Single(_parser.Parse(html, "Glassdoor", CollectedAt).Vacancies);

        Assert.Equal(new DateOnly(2026, 7, 27), vacancy.DatePosted);
        Assert.Equal("Centrical", vacancy.Company);
        Assert.Equal("Israel", vacancy.Location);
        Assert.Equal("Server-side developer.", vacancy.Description);
    }

    [Theory]
    [InlineData("3d", 2026, 8, 3)]
    [InlineData("22d", 2026, 7, 15)]
    [InlineData("30d+", 2026, 7, 7)]
    [InlineData("24h", 2026, 8, 6)]
    [InlineData("Today", 2026, 8, 6)]
    public void DerivesDatePostedFromRelativeCardAge(string age, int year, int month, int day)
    {
        var html = $"""
        <div class="jobCard">
          <a data-test="job-title" href="https://www.glassdoor.com/job-listing/x.htm?jl=123" id="job-title-123">Backend Developer</a>
          <div data-test="job-age">{age}</div>
        </div>
        """;

        var vacancy = Assert.Single(_parser.Parse(html, "Glassdoor", CollectedAt).Vacancies);

        Assert.Equal(new DateOnly(year, month, day), vacancy.DatePosted);
    }

    [Fact]
    public void LeavesDatePostedNullWhenCardHasNoAge()
    {
        const string html = """
        <div class="jobCard">
          <a data-test="job-title" href="https://www.glassdoor.com/job-listing/x.htm?jl=123" id="job-title-123">Backend Developer</a>
        </div>
        """;

        var vacancy = Assert.Single(_parser.Parse(html, "Glassdoor", CollectedAt).Vacancies);

        Assert.Null(vacancy.DatePosted);
    }

    [Fact]
    public void FallsBackToJsonLdItemList()
    {
        const string html = """
        <html><head>
          <script type="application/ld+json">
          {
            "@context": "https://schema.org",
            "@type": "ItemList",
            "numberOfItems": 1,
            "itemListElement": [
              {
                "@type": "ListItem",
                "position": 1,
                "name": "(16934) C# Backend Developer",
                "url": "https://www.glassdoor.com/job-listing/16934-c-backend-developer-yael-group-JV_KO0,33_KE34,44.htm?jl=1010215401137"
              }
            ]
          }
          </script>
        </head></html>
        """;

        var result = _parser.Parse(html, "Glassdoor", CollectedAt);

        var vacancy = Assert.Single(result.Vacancies);
        Assert.Equal(0, result.JobCardCount);
        Assert.Equal(1, result.JsonLdBlockCount);
        Assert.Equal(1, result.ItemListCount);
        Assert.Equal("1010215401137", vacancy.ExternalId);
        Assert.Equal("(16934) C# Backend Developer", vacancy.Title);
    }

    [Fact]
    public void ParsesJobDetailJsonCapturedFromClick()
    {
        const string json = """
        {
          "employerName": "Centrical",
          "jobDescription": "<div><p>Excellent C# .NET work experience</p></div>",
          "jobListingDetails": {
            "jobTitleText": "Backend Developer",
            "locationName": "Israel",
            "seoJobLink": "https://www.glassdoor.com/job-listing/backend-developer-centrical-JV_KO0,17_KE18,27.htm?jl=1010144713302"
          },
          "jobOverview": {
            "discoverDate": "2026-07-25T00:00:00",
            "listingId": 1010144713302
          }
        }
        """;

        var vacancy = _parser.ParseJobDetailJson(json, "Glassdoor", CollectedAt);

        Assert.NotNull(vacancy);
        Assert.Equal("1010144713302", vacancy.ExternalId);
        Assert.Equal("Backend Developer", vacancy.Title);
        Assert.Equal("Centrical", vacancy.Company);
        Assert.Equal("Israel", vacancy.Location);
        Assert.Equal("Excellent C# .NET work experience", vacancy.Description);
        Assert.Equal(new DateOnly(2026, 7, 25), vacancy.DatePosted);
    }
}
