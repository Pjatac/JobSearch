using JobWatcher.Sources.Drushim;

namespace JobWatcher.Tests;

public sealed class DrushimHtmlParserTests
{
    private readonly DrushimHtmlParser _parser = new();
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 5, 8, 15, 0, TimeSpan.Zero);

    [Fact]
    public void ParsesRenderedJobCards()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "drushim-job-cards.html"));

        var result = _parser.Parse(html, "Drushim-Backend", CollectedAt);

        Assert.Equal(2, result.JobCardCount);
        Assert.Equal(2, result.Vacancies.Count);

        var vacancy = result.Vacancies[0];
        Assert.Equal("37816406", vacancy.ExternalId);
        Assert.Equal("Backend developer", vacancy.Title);
        Assert.Equal("Mertens - Malam Team", vacancy.Company);
        Assert.Equal("רעננה", vacancy.Location);
        Assert.Equal("https://www.drushim.co.il/job/37816406/1abb4f7b/", vacancy.Url);
        Assert.Contains("מפתח.ת Backend", vacancy.Description);
        Assert.Equal(new DateOnly(2026, 8, 5), vacancy.DatePosted);
        Assert.Contains("משרה מלאה", vacancy.EmploymentTypes);
    }
}
