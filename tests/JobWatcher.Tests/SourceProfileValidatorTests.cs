using JobWatcher.Configuration;

namespace JobWatcher.Tests;

public sealed class SourceProfileValidatorTests
{
    private readonly SourceProfileValidator validator = new();

    [Fact]
    public void JobKarovStructuredProfileRequiresSpecialityId()
    {
        var result = validator.Validate(new JobSourceOptions
        {
            Name = "JobKarov",
            Adapter = "JobKarov",
            JobKarovFilter = new JobKarovFilterOptions { Speciality = "" }
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("speciality", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DrushimStructuredProfileRequiresPositiveCategoryId()
    {
        var result = validator.Validate(new JobSourceOptions
        {
            Name = "Drushim",
            Adapter = "Drushim",
            DrushimFilter = new DrushimFilterOptions { CategoryId = 0 }
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("category", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AllJobsWithoutPositionIdIsAWarningNotAnError()
    {
        var result = validator.Validate(new JobSourceOptions
        {
            Name = "AllJobs",
            Adapter = "AllJobs",
            AllJobsFilter = new AllJobsFilterOptions()
        });

        Assert.True(result.IsValid);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void JobSwipeRequiresAtLeastOneSearchUrl()
    {
        var result = validator.Validate(new JobSourceOptions
        {
            Name = "JobSwipe",
            Adapter = "JobSwipeCo",
            JobSwipeCoFilter = new JobSwipeCoFilterOptions()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("search URL", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GlassdoorAllowsDirectUrlWhenSearchListIsEmpty()
    {
        var result = validator.Validate(new JobSourceOptions
        {
            Name = "Glassdoor",
            Adapter = "Glassdoor",
            Url = "https://www.glassdoor.com/Job/example.htm"
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SecretTelAvivRequiresItsOwnSearchUrl()
    {
        var invalid = validator.Validate(new JobSourceOptions
        {
            Name = "Secret Tel Aviv",
            Adapter = "SecretTelAviv",
            SecretTelAvivFilter = new SecretTelAvivFilterOptions { SearchUrl = "https://example.test/jobs" }
        });
        var valid = validator.Validate(new JobSourceOptions
        {
            Name = "Secret Tel Aviv",
            Adapter = "SecretTelAviv",
            SecretTelAvivFilter = new SecretTelAvivFilterOptions { SearchUrl = "https://jobs.secrettelaviv.com/list/find/?query=Back+End" }
        });

        Assert.False(invalid.IsValid);
        Assert.True(valid.IsValid);
    }
}
