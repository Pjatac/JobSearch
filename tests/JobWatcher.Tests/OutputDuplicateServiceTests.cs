using JobWatcher.Models;
using JobWatcher.Services;

namespace JobWatcher.Tests;

public sealed class OutputDuplicateServiceTests
{
    private static readonly DateTimeOffset GeneratedAt = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private readonly OutputDuplicateService _service = new();

    private const string LongDescription =
        "Design, develop and maintain backend services and APIs using C# on Linux, working closely with product teams.";

    private const string OtherLongDescription =
        "Build and maintain data pipelines for large scale analytics workloads across a distributed cluster of machines.";

    [Fact]
    public void ReportsNothingForDistinctJobs()
    {
        var report = Review(
            Job("Glassdoor", "1", "Backend Developer", "Acme", description: LongDescription),
            Job("Glassdoor", "2", "Frontend Developer", "Acme", description: OtherLongDescription));

        Assert.Equal(2, report.ReviewedJobCount);
        Assert.Equal(0, report.DuplicateGroupCount);
        Assert.Equal(0, report.RedundantJobCount);
        Assert.Empty(report.SharedDescriptionGroups);
    }

    [Fact]
    public void GroupsTheSameTitleAndCompanyWithinOneSource()
    {
        // The existing cross-site candidate report skips same-site pairs; a single site can still
        // list one job twice.
        var report = Review(
            Job("Glassdoor", "1", "Backend Developer", "Acme"),
            Job("Glassdoor", "2", "backend  developer", "ACME"));

        var group = Assert.Single(report.DuplicateGroups);
        Assert.Equal(2, group.Count);
        Assert.Equal(1, report.RedundantJobCount);
        Assert.Contains("same-title-and-company", group.Reasons);
    }

    [Fact]
    public void GroupsTheSameTitleAndDescriptionAcrossDifferentCompanies()
    {
        // An agency relisting: company differs, but title and description match exactly.
        var report = Review(
            Job("Glassdoor", "1", "Senior Mobile Developer (Flutter)", "SAR Technologies", description: LongDescription),
            Job("Glassdoor", "2", "Senior Mobile Developer (Flutter)", "Finonex", description: LongDescription));

        var group = Assert.Single(report.DuplicateGroups);
        Assert.Contains("same-title-and-description", group.Reasons);
    }

    [Fact]
    public void GroupsListingsSharingAUrlIgnoringClickTrackingParameters()
    {
        // Only unambiguous click trackers are stripped. Parameters like "ref" are left alone,
        // because on some sites they select content rather than attribute a click.
        var report = Review(
            Job("AllJobs", "1", "Backend Developer", "Acme", url: "https://site.test/jobs/17?utm_source=a"),
            Job("Drushim", "2", "Something Else Entirely", "Other", url: "https://site.test/jobs/17?gclid=b"));

        var group = Assert.Single(report.DuplicateGroups);
        Assert.Contains("same-url", group.Reasons);
    }

    [Fact]
    public void KeepsListingsApartWhenTheQueryStringCarriesTheListingId()
    {
        // AllJobs addresses differ only by "?JobID=", so a normalisation that dropped the query
        // collapsed every listing on the site into one group.
        var report = Review(
            Job("AllJobs", "1", "Backend Developer", "Acme", url: "https://www.alljobs.co.il/Search/UploadSingle.aspx?JobID=7186099"),
            Job("AllJobs", "2", "R&D Team Leader", "Other", url: "https://www.alljobs.co.il/Search/UploadSingle.aspx?JobID=7786368"));

        Assert.Empty(report.DuplicateGroups);
    }

    [Fact]
    public void TreatsQueryParameterOrderAsInsignificant()
    {
        var report = Review(
            Job("AllJobs", "1", "Backend Developer", "Acme", url: "https://site.test/job?a=1&b=2"),
            Job("Drushim", "2", "Backend Developer", "Acme", url: "https://site.test/job?b=2&a=1"));

        var group = Assert.Single(report.DuplicateGroups);
        Assert.Contains("same-url", group.Reasons);
    }

    [Fact]
    public void MergesChainedMatchesIntoOneGroup()
    {
        var report = Review(
            Job("Glassdoor", "1", "Backend Developer", "Acme"),
            Job("Glassdoor", "2", "Backend Developer", "Acme"),
            Job("AllJobs", "3", "Backend Developer", "Acme"));

        var group = Assert.Single(report.DuplicateGroups);
        Assert.Equal(3, group.Count);
        Assert.Equal(2, report.RedundantJobCount);
    }

    [Fact]
    public void DoesNotTreatASharedDescriptionAloneAsADuplicate()
    {
        // Eternix lists three different roles behind one Glassdoor search teaser; SentinelOne opens
        // every posting with the same paragraph. Neither is the same job twice.
        var report = Review(
            Job("Glassdoor", "1", "Backend Developer", "Eternix", description: LongDescription),
            Job("Glassdoor", "2", "Desktop Application Developer", "Eternix", description: LongDescription),
            Job("Glassdoor", "3", "Frontend Developer", "Eternix", description: LongDescription));

        Assert.Empty(report.DuplicateGroups);

        var shared = Assert.Single(report.SharedDescriptionGroups);
        Assert.Equal(3, shared.Count);
        Assert.Contains("identical-description-different-titles", shared.Reasons);
    }

    [Fact]
    public void IgnoresDescriptionsTooShortToBeEvidence()
    {
        var report = Review(
            Job("Glassdoor", "1", "Backend Developer", "Acme", description: "Backend developer wanted."),
            Job("Glassdoor", "2", "Frontend Developer", "Other", description: "Backend developer wanted."));

        Assert.Empty(report.DuplicateGroups);
        Assert.Empty(report.SharedDescriptionGroups);
    }

    [Fact]
    public void DoesNotGroupTheSameTitleAtDifferentCompaniesWithoutFurtherEvidence()
    {
        // Two companies genuinely hiring for the same common role.
        var report = Review(
            Job("Glassdoor", "1", "Backend Developer", "Acme", description: LongDescription),
            Job("Glassdoor", "2", "Backend Developer", "Other", description: OtherLongDescription));

        Assert.Empty(report.DuplicateGroups);
    }

    private OutputDuplicatesOutput Review(params JobVacancy[] jobs)
    {
        var sourceOutput = new SourceOutput
        {
            Source = "Test",
            Status = "success",
            NewJobs = jobs
        };

        return _service.Review(GeneratedAt, [sourceOutput]);
    }

    private static JobVacancy Job(
        string source,
        string id,
        string title,
        string? company,
        string? description = null,
        string? url = null)
    {
        return new JobVacancy
        {
            Source = source,
            ExternalId = id,
            Title = title,
            Company = company,
            Description = description,
            Url = url ?? $"https://{source.ToLowerInvariant()}.test/jobs/{id}",
            CollectedAtUtc = GeneratedAt
        };
    }
}
