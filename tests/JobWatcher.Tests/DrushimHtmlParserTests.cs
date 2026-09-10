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

    [Fact]
    public void ParsesCurrentSearchJobCards()
    {
        const string html = """
        <html>
          <body>
            <article data-nagish="job-card-item" class="job-card-module-scss-module__nFMPJW__card">
              <h3 data-nagish="job-card-title" class="job-card-module-scss-module__nFMPJW__title">Backend Developer</h3>
              <span class="job-card-module-scss-module__nFMPJW__companyName">Acme</span>
              <div class="job-card-meta-module-scss-module__DkSa0a__row">
                <span class="job-card-meta-module-scss-module__DkSa0a__text">Tel Aviv</span>
              </div>
              <div class="job-card-meta-module-scss-module__DkSa0a__row">
                <span class="job-card-meta-module-scss-module__DkSa0a__text">3-4 years</span>
                <span class="job-card-meta-module-scss-module__DkSa0a__text">משרה מלאה</span>
                <span class="job-card-meta-module-scss-module__DkSa0a__text">היברידי</span>
              </div>
              <p class="job-card-module-scss-module__nFMPJW__description">Build backend services.</p>
              <a data-nagish="job-card-details-link" href="/job/38142978/7cb2ca63/">Details</a>
            </article>
          </body>
        </html>
        """;

        var result = _parser.Parse(html, "Drushim-Backend", CollectedAt);

        var vacancy = Assert.Single(result.Vacancies);
        Assert.Equal(1, result.JobCardCount);
        Assert.Equal("38142978", vacancy.ExternalId);
        Assert.Equal("Backend Developer", vacancy.Title);
        Assert.Equal("Acme", vacancy.Company);
        Assert.Equal("Tel Aviv", vacancy.Location);
        Assert.Equal("https://www.drushim.co.il/job/38142978/7cb2ca63/", vacancy.Url);
        Assert.Equal("Build backend services.", vacancy.Description);
        Assert.Contains("משרה מלאה", vacancy.EmploymentTypes);
        Assert.Contains("היברידי", vacancy.EmploymentTypes);
    }

    [Fact]
    public void ParsesNextDataJobsBeforeRenderedCards()
    {
        const string html = """
        <html>
          <body>
            <article data-nagish="job-card-item">
              <h3 data-nagish="job-card-title">Short card title</h3>
              <p class="job-card-module-scss-module__nFMPJW__description">Short teaser only.</p>
              <a data-nagish="job-card-details-link" href="/job/38054096/0b621faa/">Details</a>
            </article>
            <script id="__NEXT_DATA__" type="application/json">
            {
              "props": {
                "pageProps": {
                  "dehydratedState": {
                    "queries": [
                      {
                        "state": {
                          "data": {
                            "pages": [
                              {
                                "jobs": [
                                  {
                                    "id": "38054096",
                                    "title": "Full Stack Developer",
                                    "companyName": "Acme",
                                    "city": "Tel Aviv",
                                    "cities": ["Tel Aviv", "Ramat Gan"],
                                    "scope": "Full Time",
                                    "scopes": ["Full Time", "Hybrid"],
                                    "publishedAtIso": "2026-09-08T06:00:00",
                                    "description": "Role context. Requirements: 1. C# .NET. 2. REST API.",
                                    "jobUrl": "/job/38054096/0b621faa/"
                                  }
                                ]
                              }
                            ]
                          }
                        }
                      }
                    ]
                  }
                }
              }
            }
            </script>
          </body>
        </html>
        """;

        var result = _parser.Parse(html, "Drushim-Backend", CollectedAt);

        var vacancy = Assert.Single(result.Vacancies);
        Assert.Equal("38054096", vacancy.ExternalId);
        Assert.Equal("Full Stack Developer", vacancy.Title);
        Assert.Equal("Acme", vacancy.Company);
        Assert.Equal("Tel Aviv, Ramat Gan", vacancy.Location);
        Assert.Equal("https://www.drushim.co.il/job/38054096/0b621faa/", vacancy.Url);
        Assert.Equal("Role context. Requirements: 1. C# .NET. 2. REST API.", vacancy.Description);
        Assert.Equal(new DateOnly(2026, 9, 8), vacancy.DatePosted);
        Assert.Equal(["Full Time", "Hybrid"], vacancy.EmploymentTypes);
    }

    [Fact]
    public void ParsesVisibleDetailDescriptionWithRequirements()
    {
        const string html = """
        <html>
          <body>
            <main>
              <h1>מתכנת/ת</h1>
              <a href="/company/123">מדנס סוכנות לביטוח</a>
              <span>תל אביב</span>
              <span>משרה מלאה</span>
              <h2>תיאור משרה</h2>
              <p>למדנס סוכנות לביטוח דרוש/ה מתכנת/ת</p>
              <p>תחומי אחריות:</p>
              <p>פיתוח ותחזוקה של מערכות פנים ארגוניות בשפת C# .NET.</p>
              <h2>דרישות התפקיד</h2>
              <p>1. 1-3 שנות ניסיון בפיתוח צד שרת ב-C# .NET / .NET Core (חובה)</p>
              <p>2. ניסיון בפיתוח וצריכת שירותי REST API ו-Web API (חובה)</p>
              <p>* משרה זו פונה לנשים וגברים כאחד.</p>
              <h2>הדרך החכמה להתקדם לתפקיד הבא</h2>
            </main>
          </body>
        </html>
        """;

        var vacancy = _parser.ParseDetail(html, "Drushim-Backend", "https://www.drushim.co.il/job/38054096/0b621faa/", CollectedAt);

        Assert.NotNull(vacancy);
        Assert.Equal("38054096", vacancy.ExternalId);
        Assert.Equal("מתכנת/ת", vacancy.Title);
        Assert.Equal("מדנס סוכנות לביטוח", vacancy.Company);
        Assert.Equal("https://www.drushim.co.il/job/38054096/0b621faa/", vacancy.Url);
        Assert.Equal(
            "למדנס סוכנות לביטוח דרוש/ה מתכנת/ת\nתחומי אחריות:\nפיתוח ותחזוקה של מערכות פנים ארגוניות בשפת C# .NET.\nדרישות התפקיד\n1. 1-3 שנות ניסיון בפיתוח צד שרת ב-C# .NET / .NET Core (חובה)\n2. ניסיון בפיתוח וצריכת שירותי REST API ו-Web API (חובה)\n* משרה זו פונה לנשים וגברים כאחד.",
            vacancy.Description);
        Assert.Contains("משרה מלאה", vacancy.EmploymentTypes);
    }
}
