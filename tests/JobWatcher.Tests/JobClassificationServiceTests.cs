using JobWatcher.Configuration;
using JobWatcher.Models;
using JobWatcher.Services;

namespace JobWatcher.Tests;

public sealed class JobClassificationServiceTests
{
    [Fact]
    public void ClassifiesSeniorDotNetBackendAsRelevant()
    {
        var result = Classify("Senior C#/.NET Backend Engineer", "5+ years building backend APIs.");

        Assert.Equal("relevant", result.Classification);
        Assert.Contains("include-signal:C#", result.Reasons);
    }

    [Fact]
    public void ClassifiesJavaOnlyAsExcludedOtherLanguage()
    {
        var result = Classify("Java Developer", "Build Spring services.");

        Assert.Equal("excluded", result.Classification);
        Assert.Contains("other-language:Java", result.Reasons);
    }

    [Fact]
    public void ClassifiesJuniorDotNetAsExcludedUnlessSeniorOverrideExists()
    {
        var junior = Classify("Junior .NET Developer", "1-2 years of experience.");
        var overrideResult = Classify("Senior .NET Developer", "1-2 years is acceptable, but 5+ years preferred.");

        Assert.Equal("excluded", junior.Classification);
        Assert.Contains(junior.Reasons, reason => reason.StartsWith("junior:", StringComparison.Ordinal));
        Assert.Equal("relevant", overrideResult.Classification);
    }

    [Fact]
    public void ClassifiesFullStackAsExcludedRoleMismatch()
    {
        var result = Classify("Full Stack C# Developer", "ASP.NET and React.");

        Assert.Equal("excluded", result.Classification);
        Assert.Contains("role-mismatch:Full Stack", result.Reasons);
    }

    [Fact]
    public void ClassifiesGlassdoorWithoutDescriptionAsReviewAndSetsSoftFlags()
    {
        var result = JobClassificationService.ClassifyJob(new JobVacancy
        {
            Source = "Glassdoor-BackendKfarSaba",
            ExternalId = "1",
            Title = "Software Engineer",
            Company = "Cyber Corp",
            Location = "Jerusalem",
            Url = "https://example.test/1",
            CollectedAtUtc = DateTimeOffset.UtcNow
        }, Config());

        Assert.Equal("review", result.Classification);
        Assert.Contains("glassdoor-short-description", result.Reasons);
        Assert.True(result.Flags.FarCommute);
        Assert.True(result.Flags.Cyber);
    }

    private static JobClassification Classify(string title, string description) =>
        JobClassificationService.ClassifyJob(new JobVacancy
        {
            Source = "JobKarov-Software",
            ExternalId = "1",
            Title = title,
            Description = description,
            Url = "https://example.test/1",
            CollectedAtUtc = DateTimeOffset.UtcNow
        }, Config());

    private static JobClassificationOptions Config() => new()
    {
        DescriptionScanLength = 400,
        IncludeSignals = ["C#", ".NET", "DotNet", "ASP.NET", "Backend", "Back-End", "Back End", "צד שרת", "בק אנד"],
        OtherPrimaryLanguages = ["Java", "Python", "Golang", "Go", "PHP", "Node.js", "NodeJS", "C++", "Rust", "Kotlin", "Scala", "Ruby", "Elixir", "Magic"],
        RoleMismatchSignals = ["Frontend", "Front-End", "Full Stack", "Fullstack", "פרונט", "Mobile", "Android", "iOS", "QA", "Automation", "בודק", "Manual Tester", "DevOps", "SRE", "Site Reliability", "Platform Engineer", "Infrastructure Engineer", "Terraform", "Ansible", "ETL", "Airflow", "Spark", "DBA", "Data Engineer", "Data Analyst", "אנליסט", "מהנדס דאטה", "BI", "Embedded", "FPGA", "DSP", "Hardware", "חומרה", "מוטס", "Mechanical", "מכני", "Product Manager", "מנהל מוצר", "Machine Learning", "ML Engineer", "Algorithm", "אלגוריתם", "Student", "סטודנט", "Architect", "ארכיטקט", "Team Lead", "ראש צוות"],
        JuniorSignals = ["Junior", "ג'וניור", "שנה ניסיון", "כשנה", "שנתיים", "1 year", "Up to 3 years"],
        JuniorExperiencePatterns = [@"(?<![\p{L}\p{N}])1\s*-\s*2\s*y(?:ears?)?(?![\p{L}\p{N}])"],
        SeniorOverrideSignals = ["Senior", "בכיר", "4+", "5+", "6+"],
        FarCommuteLocations = ["Jerusalem"],
        CyberSignals = ["Cyber", "Security", "סייבר", "אבטח"]
    };
}
