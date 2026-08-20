using JobWatcher.Models;
using JobWatcher.Services;

namespace JobWatcher.Tests;

public sealed class DuplicateCandidateServiceTests
{
    private readonly DuplicateCandidateService _service = new();

    [Fact]
    public void FindsLikelyDuplicatesAcrossDifferentSites()
    {
        var snapshots = new[]
        {
            Snapshot("JobKarov-Software", Vacancy("JobKarov-Software", "1", "Senior Backend Engineer - C# .NET", "Acme")),
            Snapshot("Drushim-SoftwareRoles", Vacancy("Drushim-SoftwareRoles", "2", "Back-End Developer .NET Core C#", "ACME"))
        };

        var output = _service.FindCandidates(DateTimeOffset.UtcNow, snapshots);

        var candidate = Assert.Single(output.Candidates);
        Assert.Equal(1, output.CandidateCount);
        Assert.True(candidate.Score >= 0.78);
        Assert.Contains(candidate.Reasons, reason => reason.StartsWith("title:", StringComparison.Ordinal));
        Assert.Contains(candidate.Reasons, reason => reason.StartsWith("company:", StringComparison.Ordinal));
    }

    [Fact]
    public void IgnoresDifferentConfiguredSourcesFromSameSiteFamily()
    {
        var snapshots = new[]
        {
            Snapshot("JobKarov-Software", Vacancy("JobKarov-Software", "1", "Backend Engineer", "Acme")),
            Snapshot("JobKarov-Cyber", Vacancy("JobKarov-Cyber", "2", "Backend Engineer", "Acme"))
        };

        var output = _service.FindCandidates(DateTimeOffset.UtcNow, snapshots);

        Assert.Empty(output.Candidates);
        Assert.Equal(0, output.CandidateCount);
    }

    [Fact]
    public void CollapsesRepeatedSameSiteVacancyBeforeCrossSiteMatching()
    {
        var snapshots = new[]
        {
            Snapshot("JobKarov-Software", Vacancy("JobKarov-Software", "1", "Senior Backend Engineer", "Acme")),
            Snapshot("JobKarov-Cyber", Vacancy("JobKarov-Cyber", "1", "Senior Backend Engineer", "Acme")),
            Snapshot("Drushim-SoftwareRoles", Vacancy("Drushim-SoftwareRoles", "2", "Backend Developer", "Acme"))
        };

        var output = _service.FindCandidates(DateTimeOffset.UtcNow, snapshots);

        Assert.Single(output.Candidates);
        Assert.Equal(1, output.CandidateCount);
    }

    [Fact]
    public void DoesNotMatchWeakTitleSimilarity()
    {
        var snapshots = new[]
        {
            Snapshot("JobKarov-Software", Vacancy("JobKarov-Software", "1", "Backend Engineer", "Acme")),
            Snapshot("Drushim-SoftwareRoles", Vacancy("Drushim-SoftwareRoles", "2", "QA Automation Engineer", "Acme"))
        };

        var output = _service.FindCandidates(DateTimeOffset.UtcNow, snapshots);

        Assert.Empty(output.Candidates);
    }

    private static SourceSnapshot Snapshot(string source, params JobVacancy[] vacancies)
    {
        return new SourceSnapshot
        {
            Source = source,
            CollectedAtUtc = DateTimeOffset.UtcNow,
            Vacancies = vacancies
        };
    }

    private static JobVacancy Vacancy(string source, string id, string title, string? company)
    {
        return new JobVacancy
        {
            Source = source,
            ExternalId = id,
            Title = title,
            Company = company,
            Location = "Tel Aviv",
            Url = $"https://example.test/{source}/{id}",
            CollectedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
