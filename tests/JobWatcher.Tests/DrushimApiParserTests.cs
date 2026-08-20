using JobWatcher.Sources.Drushim;

namespace JobWatcher.Tests;

public sealed class DrushimApiParserTests
{
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly DrushimApiParser _parser = new();

    [Fact]
    public void ParsesApiResultList()
    {
        var json = """
        {
          "TotalPagesNumber": 11,
          "NextPageNumber": 2,
          "TotalSearchResultCount": 118,
          "ResultList": [
            {
              "Code": 37900861,
              "Company": { "NameInHebrew": "Comm-IT" },
              "JobContent": {
                "Name": "AI Engineer",
                "Description": "Build <b>services</b>",
                "Requirements": "3+ years",
                "Scopes": [{ "Code": 1, "NameInHebrew": "משרה מלאה" }],
                "Addresses": [{ "City": "פתח תקווה" }]
              },
              "JobInfo": {
                "Link": "/job/37900861/85345d63/",
                "Date": "2026-07-27T11:38:50.313"
              }
            }
          ]
        }
        """;

        var result = _parser.Parse(json, "Drushim-SoftwareRoles", CollectedAt);

        var vacancy = Assert.Single(result.Vacancies);
        Assert.Empty(result.Warnings);
        Assert.Equal(11, result.TotalPages);
        Assert.Equal(2, result.NextPage);
        Assert.Equal(118, result.TotalSearchResultCount);
        Assert.Equal("37900861", vacancy.ExternalId);
        Assert.Equal("AI Engineer", vacancy.Title);
        Assert.Equal("Comm-IT", vacancy.Company);
        Assert.Equal("פתח תקווה", vacancy.Location);
        Assert.Equal("https://www.drushim.co.il/job/37900861/85345d63/", vacancy.Url);
        Assert.Equal(new DateOnly(2026, 7, 27), vacancy.DatePosted);
        Assert.Equal("משרה מלאה", Assert.Single(vacancy.EmploymentTypes));
        Assert.Contains("Build services", vacancy.Description);
        Assert.Contains("Requirements: 3+ years", vacancy.Description);
    }
}
