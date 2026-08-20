using JobWatcher.Models;
using JobWatcher.Services;

namespace JobWatcher.Tests;

public sealed class JobComparisonServiceTests
{
    private readonly JobComparisonService _service = new();

    [Fact]
    public void FirstRunTreatsAllCurrentVacanciesAsNew()
    {
        var current = Snapshot("JobKarov", Vacancy("1"), Vacancy("2"));

        var diff = _service.Compare(null, current);

        Assert.True(diff.IsInitialRun);
        Assert.Equal(2, diff.NewVacancies.Count);
        Assert.Equal(0, diff.PreviousCount);
    }

    [Fact]
    public void NoChangesReturnsUnchangedOnly()
    {
        var previous = Snapshot("JobKarov", Vacancy("1"));
        var current = Snapshot("JobKarov", Vacancy("1"));

        var diff = _service.Compare(previous, current);

        Assert.Empty(diff.NewVacancies);
        Assert.Single(diff.UnchangedVacancies);
        Assert.Empty(diff.RemovedVacancies);
    }

    [Fact]
    public void FindsNewAndRemovedVacanciesByIdentity()
    {
        var previous = Snapshot("JobKarov", Vacancy("1"), Vacancy("2"));
        var current = Snapshot("JobKarov", Vacancy("2"), Vacancy("3"));

        var diff = _service.Compare(previous, current);

        Assert.Equal("3", Assert.Single(diff.NewVacancies).ExternalId);
        Assert.Equal("1", Assert.Single(diff.RemovedVacancies).ExternalId);
    }

    [Fact]
    public void SameTitleWithDifferentIdsIsNew()
    {
        var previous = Snapshot("JobKarov", Vacancy("1", "Same"));
        var current = Snapshot("JobKarov", Vacancy("2", "Same"));

        var diff = _service.Compare(previous, current);

        Assert.Equal("2", Assert.Single(diff.NewVacancies).ExternalId);
        Assert.Equal("1", Assert.Single(diff.RemovedVacancies).ExternalId);
    }

    [Fact]
    public void DuplicateCurrentVacanciesAreDeduplicated()
    {
        var current = Snapshot("JobKarov", Vacancy("1"), Vacancy("1"));

        var diff = _service.Compare(null, current);

        Assert.Single(diff.NewVacancies);
        Assert.Equal(1, diff.CurrentCount);
    }

    [Fact]
    public void LargeCountDropCreatesWarning()
    {
        var previous = Snapshot("JobKarov", Enumerable.Range(1, 10).Select(i => Vacancy(i.ToString())).ToArray());
        var current = Snapshot("JobKarov", Vacancy("1"), Vacancy("2"), Vacancy("3"), Vacancy("4"));

        var diff = _service.Compare(previous, current);

        Assert.Single(diff.Warnings);
    }

    private static SourceSnapshot Snapshot(string source, params JobVacancy[] vacancies)
    {
        return new SourceSnapshot { Source = source, CollectedAtUtc = DateTimeOffset.UtcNow, Vacancies = vacancies };
    }

    private static JobVacancy Vacancy(string id, string title = "Title")
    {
        return new JobVacancy
        {
            Source = "JobKarov",
            ExternalId = id,
            Title = title,
            Url = $"https://www.jobkarov.com/Search/Site/{id}",
            CollectedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
