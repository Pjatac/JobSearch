using JobWatcher.Sources.JobKarov;

namespace JobWatcher.Tests;

public sealed class JobKarovRequirementsTests
{
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private readonly JobKarovJsonLdParser _parser = new();

    private static string Html(string baseSites) => $$"""
        <html><head>
        <script type="application/ld+json">
        {
          "@type": "ItemList",
          "itemListElement": [{
            "@type": "ListItem",
            "item": {
              "@type": "JobPosting",
              "title": "Backend Developer",
              "description": "Join our team and build services.",
              "url": "/Search/Site/2725138"
            }
          }]
        }
        </script>
        <script>
            window.__BASE_SITES__ = {{baseSites}};
        </script>
        </head></html>
        """;

    [Fact]
    public void AppendsTheRequirementsFieldJsonLdOmits()
    {
        // JobKarov publishes only "description" as JSON-LD and keeps the experience level and the
        // technology stack in a separate "require" field.
        var result = _parser.Parse(
            Html("""[{"id":2725138,"require":"4+ years with C# and .NET. Experience with Kubernetes."}]"""),
            "JobKarov",
            CollectedAt);

        var vacancy = Assert.Single(result.Vacancies);
        Assert.Equal(1, result.RequirementsMergedCount);
        Assert.Equal(
            "Join our team and build services.\n\nRequirements: 4+ years with C# and .NET. Experience with Kubernetes.",
            vacancy.Description);
    }

    [Fact]
    public void LeavesVacanciesWithoutAMatchingEntryUntouched()
    {
        var result = _parser.Parse(
            Html("""[{"id":9999999,"require":"Belongs to another vacancy."}]"""),
            "JobKarov",
            CollectedAt);

        var vacancy = Assert.Single(result.Vacancies);
        Assert.Equal(0, result.RequirementsMergedCount);
        Assert.Equal("Join our team and build services.", vacancy.Description);
    }

    [Fact]
    public void IgnoresEntriesWithoutRequirements()
    {
        var result = _parser.Parse(Html("""[{"id":2725138,"require":""}]"""), "JobKarov", CollectedAt);

        Assert.Equal(0, result.RequirementsMergedCount);
        Assert.Equal("Join our team and build services.", Assert.Single(result.Vacancies).Description);
    }

    [Fact]
    public void DoesNotAppendRequirementsThatAreAlreadyInTheDescription()
    {
        var result = _parser.Parse(
            Html("""[{"id":2725138,"require":"build services"}]"""),
            "JobKarov",
            CollectedAt);

        Assert.Equal(0, result.RequirementsMergedCount);
        Assert.Equal("Join our team and build services.", Assert.Single(result.Vacancies).Description);
    }

    [Fact]
    public void StripsMarkupFromTheRequirementsText()
    {
        var result = _parser.Parse(
            Html("""[{"id":2725138,"require":"<p>4+ years with C#</p><br/><b>Kubernetes</b>"}]"""),
            "JobKarov",
            CollectedAt);

        Assert.Contains("Requirements: 4+ years with C# Kubernetes", Assert.Single(result.Vacancies).Description);
    }

    [Fact]
    public void ParsesNormallyWhenTheBaseSitesBlockIsAbsent()
    {
        const string html = """
            <html><head><script type="application/ld+json">
            { "@type": "JobPosting", "title": "Backend Developer", "description": "Only this.", "url": "/Search/Site/1" }
            </script></head></html>
            """;

        var result = _parser.Parse(html, "JobKarov", CollectedAt);

        Assert.Equal(0, result.RequirementsMergedCount);
        Assert.Empty(result.Warnings);
        Assert.Equal("Only this.", Assert.Single(result.Vacancies).Description);
    }

    [Fact]
    public void WarnsInsteadOfThrowingOnAMalformedBaseSitesBlock()
    {
        var result = _parser.Parse(Html("""[{"id":2725138,"require":}]"""), "JobKarov", CollectedAt);

        Assert.Single(result.Vacancies);
        Assert.Contains(result.Warnings, w => w.Contains("__BASE_SITES__", StringComparison.Ordinal));
    }
}
