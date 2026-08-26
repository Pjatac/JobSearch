using JobWatcher.Sources.DevJobs;

namespace JobWatcher.Tests;

public sealed class DevJobsHtmlParserTests
{
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 26, 14, 0, 0, TimeSpan.Zero);
    private readonly DevJobsHtmlParser parser = new();

    [Fact]
    public void ParsesSearchCardsAndNextPage()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "devjobs-search.html"));

        var result = parser.ParseSearch(html, "DevJobs-Backend", CollectedAt);

        Assert.Equal(2, result.JobCardCount);
        Assert.True(result.HasNextPage);
        Assert.Empty(result.Warnings);
        var vacancy = Assert.Single(result.Vacancies, item => item.ExternalId == "4435561066");
        Assert.Equal("Backend Developer", vacancy.Title);
        Assert.Equal("HOTELMIZE", vacancy.Company);
        Assert.Equal("Tel Aviv-Yafo", vacancy.Location);
        Assert.Equal(new DateOnly(2026, 7, 1), vacancy.DatePosted);
        Assert.Equal("https://devjobs.co.il/job-details/4435561066", vacancy.Url);
    }

    [Fact]
    public void ParsesDetailPage()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "devjobs-detail.html"));

        var result = parser.ParseDetail(html, "DevJobs-Backend", "https://devjobs.co.il/job-details/4435561066", CollectedAt);

        Assert.Empty(result.Warnings);
        var vacancy = Assert.IsType<JobWatcher.Models.JobVacancy>(result.Vacancy);
        Assert.Equal("4435561066", vacancy.ExternalId);
        Assert.Equal("Backend Developer", vacancy.Title);
        Assert.Equal("HOTELMIZE", vacancy.Company);
        Assert.Equal("Tel Aviv-Yafo", vacancy.Location);
        Assert.Equal(new DateOnly(2026, 7, 1), vacancy.DatePosted);
        Assert.Equal(["Hybrid"], vacancy.EmploymentTypes);
        Assert.Equal("Mize is a dynamic Fintech-travel startup.\nOur R&D team is seeking a Backend Developer.\nRequirements:\n- 3+ years of professional C# development experience.\n- Solid understanding of .NET.\n\nSkills: C# - 3y, .NET, Couchbase", vacancy.Description);
    }
}
