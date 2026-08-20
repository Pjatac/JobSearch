using JobWatcher.Sources.SecretTelAviv;

namespace JobWatcher.Tests;

public sealed class SecretTelAvivHtmlParserTests
{
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 14, 14, 36, 56, TimeSpan.Zero);
    private readonly SecretTelAvivHtmlParser parser = new();

    [Fact]
    public void ParsesSearchGridCards()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "secrettelaviv-search.html"));

        var result = parser.Parse(html, "SecretTelAviv-BackEnd", CollectedAt);

        Assert.Equal(2, result.JobCardCount);
        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Vacancies.Count);

        var vacancy = Assert.Single(result.Vacancies, item => item.ExternalId == "backend-net-developer");
        Assert.Equal("Backend .NET Developer", vacancy.Title);
        Assert.Equal("Landa Corporation", vacancy.Company);
        Assert.Equal("Rehovot", vacancy.Location);
        Assert.Equal("https://jobs.secrettelaviv.com/job/backend-net-developer/", vacancy.Url);
        Assert.Equal(["1 Full-time"], vacancy.EmploymentTypes);
        Assert.Null(vacancy.Description);
        Assert.Equal(CollectedAt, vacancy.CollectedAtUtc);

        var relativeUrlVacancy = Assert.Single(result.Vacancies, item => item.ExternalId == "back-end-tech-lead-5");
        Assert.Equal("https://jobs.secrettelaviv.com/job/back-end-tech-lead-5/", relativeUrlVacancy.Url);
    }

    [Fact]
    public void ParsesJobPostingDetails()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "secrettelaviv-detail.html"));

        var result = parser.ParseDetail(html);

        Assert.Empty(result.Warnings);
        var details = Assert.IsType<SecretTelAvivJobDetails>(result.Details);
        Assert.Equal("Backend .NET Developer", details.Title);
        Assert.Equal("Landa Corporation", details.Company);
        Assert.Equal("Rehovot", details.Location);
        Assert.Equal("We are looking for a Backend .NET Developer.\nBuild real-time systems with C# and .NET.\n- Code reviews\n- SQL databases", details.Description);
        Assert.Equal(new DateOnly(2026, 8, 3), details.DatePosted);
        Assert.Equal(new DateOnly(9999, 12, 31), details.ValidThrough);
        Assert.Equal(["FULL_TIME"], details.EmploymentTypes);
    }
}
