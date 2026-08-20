using JobWatcher.Sources.JobSwipeCo;

namespace JobWatcher.Tests;

public sealed class JobSwipeCoJsonLdParserTests
{
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly JobSwipeCoJsonLdParser _parser = new();

    [Fact]
    public void ParsesSearchItemList()
    {
        const string html = """
        <html><head>
        <script type="application/ld+json">
        {
          "@context": "http://schema.org",
          "@type": "ItemList",
          "numberOfItems": 2,
          "itemListElement": [
            {"@type":"ListItem","position":1,"url":"https://jobswipe.co/jobs/backend-developer-6a23fc5828561c70347b1be1"},
            {"@type":"ListItem","position":2,"url":"https://jobswipe.co/jobs/backend-developer-6a23fc5828561c70347b1be1"}
          ]
        }
        </script>
        </head></html>
        """;

        var result = _parser.ParseSearch(html);

        var url = Assert.Single(result.JobUrls);
        Assert.Equal("https://jobswipe.co/jobs/backend-developer-6a23fc5828561c70347b1be1", url);
        Assert.Empty(result.Warnings);
        Assert.Equal(1, result.JsonLdBlockCount);
        Assert.Equal(1, result.ItemListCount);
    }

    [Fact]
    public void ParsesJobPostingDetail()
    {
        const string html = """
        <html><head>
        <script type="application/ld+json">
        {
          "@context": "http://schema.org",
          "@type": "JobPosting",
          "title": "Backend Developer",
          "url": "https://jobswipe.co/jobs/backend-developer-6a23fc5828561c70347b1be1",
          "datePosted": "2026-06-06",
          "validThrough": "2026-08-05",
          "employmentType": "FULL_TIME",
          "hiringOrganization": {"@type":"Organization","name":"Acme"},
          "jobLocation": {
            "@type": "Place",
            "address": {
              "@type": "PostalAddress",
              "addressLocality": "Tel Aviv",
              "addressRegion": "Israel"
            }
          },
          "description": "<p>Build services</p>"
        }
        </script>
        </head></html>
        """;

        var result = _parser.ParseJob(html, "JobSwipeCo", CollectedAt);

        Assert.NotNull(result.Vacancy);
        var vacancy = result.Vacancy;
        Assert.Empty(result.Warnings);
        Assert.Equal("6a23fc5828561c70347b1be1", vacancy.ExternalId);
        Assert.Equal("Backend Developer", vacancy.Title);
        Assert.Equal("Acme", vacancy.Company);
        Assert.Equal("Tel Aviv, Israel", vacancy.Location);
        Assert.Equal("Build services", vacancy.Description);
        Assert.Equal(new DateOnly(2026, 6, 6), vacancy.DatePosted);
        Assert.Equal(new DateOnly(2026, 8, 5), vacancy.ValidThrough);
        Assert.Equal(["FULL_TIME"], vacancy.EmploymentTypes);
    }

    [Fact]
    public void PreservesParagraphAndListBoundariesInDescription()
    {
        const string html = """
        <script type="application/ld+json">
        {
          "@type": "JobPosting",
          "title": "Backend Developer",
          "url": "https://jobswipe.co/jobs/backend-developer-6a23fc5828561c70347b1be1",
          "description": "<p>Build services.</p><p>Work with <strong>.NET</strong>.</p><ul><li>Docker</li><li>Kubernetes</li></ul>"
        }
        </script>
        """;

        var vacancy = _parser.ParseJob(html, "JobSwipeCo", CollectedAt).Vacancy;

        Assert.NotNull(vacancy);
        Assert.Equal("Build services.\nWork with .NET.\n- Docker\n- Kubernetes", vacancy.Description);
    }
}
