using System.Net;
using JobWatcher.Sources.Glassdoor;

namespace JobWatcher.Tests;

public sealed class GlassdoorChallengeDetectorTests
{
    [Fact]
    public void DetectsChallengePageServedWithSuccessStatus()
    {
        const string html = """
        <!doctype html><html lang="en"><head>
          <title>Security | Glassdoor</title>
          <meta name="robots" content="noindex, nofollow" />
        </head><body></body></html>
        """;

        var challenge = GlassdoorChallengeDetector.Detect(HttpStatusCode.OK, html);

        Assert.NotNull(challenge);
        Assert.Contains("anti-bot challenge", challenge);
        Assert.Contains("Security | Glassdoor", challenge);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void DetectsBlockingStatusCodes(HttpStatusCode statusCode)
    {
        var challenge = GlassdoorChallengeDetector.Detect(statusCode, "<html><head><title>Glassdoor</title></head></html>");

        Assert.NotNull(challenge);
        Assert.Contains(((int)statusCode).ToString(), challenge);
    }

    [Fact]
    public void AcceptsNormalSearchPage()
    {
        const string html = """
        <html><head><title>251 backend developer Jobs in Kfar Saba, August 2026 | Glassdoor</title></head><body></body></html>
        """;

        Assert.Null(GlassdoorChallengeDetector.Detect(HttpStatusCode.OK, html));
    }

    [Fact]
    public void DoesNotFlagAPageWithoutATitle()
    {
        Assert.Null(GlassdoorChallengeDetector.Detect(HttpStatusCode.OK, "<html><body>jobs</body></html>"));
    }
}
