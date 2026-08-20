using JobWatcher.Sources.AllJobs;

namespace JobWatcher.Tests;

public sealed class AllJobsHtmlParserTests
{
    private static readonly DateTimeOffset CollectedAt = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly AllJobsHtmlParser _parser = new();

    [Fact]
    public void ParsesJobCardsAndPagingMetadata()
    {
        const string html = """
        <html>
          <body>
            <input type="hidden" id="hdnJobsCount" value="349" />
            <input type="hidden" id="hdnTotalPages" value="22" />
            <div class="job-content-top">
              <div class="job-content-top-date">לפני 3 שעות</div>
              <div class="job-content-top-title">
                <div><a title="דרושים | מהנדס /ת פרויקטים" href="/Search/UploadSingle.aspx?JobID=8769958"><h2>מהנדס /ת פרויקטים</h2></a></div>
                <div class="T14"><a href="/Employer/HP/Default.aspx?cid=318711">ריקרוטיקס בע"מ</a></div>
              </div>
              <div class="job-content-top-location"><b>מיקום המשרה:</b> תל אביב יפו (זמן ממוצע : 24 דקות)</div>
              <div class="job-content-top-type"><b>סוג משרה: </b><a>משרה מלאה</a> ו<a>עבודה היברידית</a></div>
              <div class="job-content-top-desc AR RTL">תיאור <br /> משרה</div>
            </div>
          </body>
        </html>
        """;

        var result = _parser.Parse(html, "AllJobs", CollectedAt);

        var vacancy = Assert.Single(result.Vacancies);
        Assert.Empty(result.Warnings);
        Assert.Equal(1, result.JobCardCount);
        Assert.Equal(22, result.TotalPages);
        Assert.Equal(349, result.TotalJobs);
        Assert.Equal("8769958", vacancy.ExternalId);
        Assert.Equal("מהנדס /ת פרויקטים", vacancy.Title);
        Assert.Equal("ריקרוטיקס בע\"מ", vacancy.Company);
        Assert.Equal("תל אביב יפו", vacancy.Location);
        Assert.Equal("https://www.alljobs.co.il/Search/UploadSingle.aspx?JobID=8769958", vacancy.Url);
        Assert.Equal("תיאור משרה", vacancy.Description);
        Assert.Equal(["משרה מלאה", "עבודה היברידית"], vacancy.EmploymentTypes);
    }

    [Fact]
    public void ParsesLtrJobCards()
    {
        const string html = """
        <html>
          <body>
            <input type="hidden" id="hdnJobsCount" value="255" />
            <input type="hidden" id="hdnTotalPages" value="14" />
            <div class="job-content-top">
              <div class="job-content-top-title-ltr closed-job">
                <div class="T22"><a title="Jobs | Staff Backend Developer" href="/Search/UploadSingle.aspx?JobID=8739972"><h2>Staff Backend Developer</h2></a></div>
                <div class="T14"></div>
              </div>
              <div class="job-content-top-location-ltr"><b>Location: </b><a>Tel Aviv-Yafo</a></div>
              <div class="job-content-top-type-ltr"><b>Job Type: </b><a>Full Time</a></div>
              <div class="job-content-top-desc closed-job AL LTR">Backend systems role</div>
            </div>
          </body>
        </html>
        """;

        var result = _parser.Parse(html, "AllJobs", CollectedAt);

        var vacancy = Assert.Single(result.Vacancies);
        Assert.Empty(result.Warnings);
        Assert.Equal("8739972", vacancy.ExternalId);
        Assert.Equal("Staff Backend Developer", vacancy.Title);
        Assert.Equal("Tel Aviv-Yafo", vacancy.Location);
        Assert.Equal(["Full Time"], vacancy.EmploymentTypes);
    }
}
