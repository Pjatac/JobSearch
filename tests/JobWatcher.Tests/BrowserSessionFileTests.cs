using System.Net;
using JobWatcher.Http;

namespace JobWatcher.Tests;

public sealed class BrowserSessionFileTests
{
    private const string RawHeaderBlock = """
    :authority: www.glassdoor.com
    :method: GET
    :path: /Job/kfar-saba-backend-developer-jobs-SRCH_IL.0,9_IC4507116_KO10,27.htm
    :scheme: https
    Accept: text/html,application/xhtml+xml
    Accept-Encoding: gzip, deflate, br, zstd
    Accept-Language: uk,en-US;q=0.9,en;q=0.8,ru;q=0.7
    Cache-Control: no-cache
    Cookie: gdId=abc-123; cf_clearance=xyz.789-1786143331-1.2.1.1; __cf_bm=def456
    User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/140.0.0.0 Safari/537.36
    """;

    [Fact]
    public void ParsesARawRequestHeaderBlock()
    {
        var session = BrowserSessionFile.Parse(RawHeaderBlock);

        Assert.NotNull(session);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/140.0.0.0 Safari/537.36", session.UserAgent);
        Assert.Equal("uk,en-US;q=0.9,en;q=0.8,ru;q=0.7", session.AcceptLanguage);
        Assert.Equal("gdId=abc-123; cf_clearance=xyz.789-1786143331-1.2.1.1; __cf_bm=def456", session.Cookie);
    }

    [Fact]
    public void ParsesTheNameAndValueRowsShownByDevTools()
    {
        const string headers = """
        :authority
        www.glassdoor.com
        :method
        GET
        accept-language
        uk,en-US;q=0.9
        cookie
        gdId=abc-123; cf_clearance=xyz789
        user-agent
        Mozilla/5.0 Chrome/149.0.0.0
        """;

        var session = BrowserSessionFile.Parse(headers);

        Assert.NotNull(session);
        Assert.Equal("gdId=abc-123; cf_clearance=xyz789", session.Cookie);
        Assert.Equal("uk,en-US;q=0.9", session.AcceptLanguage);
        Assert.Equal("Mozilla/5.0 Chrome/149.0.0.0", session.UserAgent);
    }

    [Fact]
    public void ParsesTheTabSeparatedRowsCopiedFromDevTools()
    {
        const string headers = """
        :authority	www.glassdoor.com
        accept-language	uk,en-US;q=0.9
        cookie	gdId=abc-123; cf_clearance=xyz789
        user-agent	Mozilla/5.0 Chrome/149.0.0.0
        """;

        var session = BrowserSessionFile.Parse(headers);

        Assert.NotNull(session);
        Assert.Equal("gdId=abc-123; cf_clearance=xyz789", session.Cookie);
        Assert.Equal("uk,en-US;q=0.9", session.AcceptLanguage);
        Assert.Equal("Mozilla/5.0 Chrome/149.0.0.0", session.UserAgent);
    }

    [Fact]
    public void ParsesTheSpaceSeparatedRowsCopiedFromDevTools()
    {
        const string headers = """
        cookie    gdId=abc-123; cf_clearance=xyz789
        accept-language    uk,en-US;q=0.9
        user-agent    Mozilla/5.0 Chrome/149.0.0.0
        """;

        var session = BrowserSessionFile.Parse(headers);

        Assert.NotNull(session);
        Assert.Equal("gdId=abc-123; cf_clearance=xyz789", session.Cookie);
        Assert.Equal("uk,en-US;q=0.9", session.AcceptLanguage);
        Assert.Equal("Mozilla/5.0 Chrome/149.0.0.0", session.UserAgent);
    }

    [Fact]
    public void SkipsHttp2PseudoHeaders()
    {
        // ":path" would otherwise parse as an empty name with a value, and ":scheme: https" could
        // be mistaken for a real header.
        var session = BrowserSessionFile.Parse(RawHeaderBlock);

        Assert.NotNull(session);
        Assert.DoesNotContain("glassdoor.com", session.Cookie);
    }

    [Fact]
    public void ParsesACopiedCurlCommand()
    {
        // Current Chrome has no "view source" toggle for request headers, so "Copy as cURL" is
        // often what an operator can actually export.
        const string curl = """
        curl 'https://www.glassdoor.com/Job/kfar-saba-backend-developer-jobs-SRCH_IL.0,9_IC4507116_KO10,27.htm' \
          -H 'accept: text/html,application/xhtml+xml' \
          -H 'accept-language: uk,en-US;q=0.9' \
          -b 'gdId=abc-123; cf_clearance=xyz789' \
          -H 'user-agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/149.0.0.0 Safari/537.36'
        """;

        var session = BrowserSessionFile.Parse(curl);

        Assert.NotNull(session);
        Assert.Equal("gdId=abc-123; cf_clearance=xyz789", session.Cookie);
        Assert.Equal("uk,en-US;q=0.9", session.AcceptLanguage);
        Assert.Equal("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/149.0.0.0 Safari/537.36", session.UserAgent);
    }

    [Fact]
    public void ParsesACurlCommandCopiedForCmd()
    {
        const string curl = """
        curl "https://www.glassdoor.com/" ^
          -H ^"cookie: gdId=abc-123; cf_clearance=xyz789^" ^
          -H ^"user-agent: Chrome/149^"
        """;

        var session = BrowserSessionFile.Parse(curl);

        Assert.NotNull(session);
        Assert.Equal("gdId=abc-123; cf_clearance=xyz789", session.Cookie);
        Assert.Equal("Chrome/149", session.UserAgent);
    }

    [Fact]
    public void ReturnsNullWhenTheBlockHasNoCookies()
    {
        Assert.Null(BrowserSessionFile.Parse("Accept: text/html\nUser-Agent: Chrome/140"));
    }

    [Fact]
    public void SeedsCookiesFromTheParsedHeader()
    {
        var session = BrowserSessionFile.Parse(RawHeaderBlock);
        var container = new CookieContainer();

        var names = session!.SeedCookies(container, ".glassdoor.com", out _);

        Assert.Equal(["gdId", "cf_clearance", "__cf_bm"], names);

        var cookies = container.GetCookies(new Uri("https://www.glassdoor.com/Job/jobs.htm"));
        Assert.Equal(3, cookies.Count);
        Assert.Equal("abc-123", cookies["gdId"]!.Value);
        Assert.Equal("xyz.789-1786143331-1.2.1.1", cookies["cf_clearance"]!.Value);
    }

    [Fact]
    public void KeepsCookieValuesContainingEqualsSigns()
    {
        var session = BrowserSessionFile.Parse("Cookie: token=aGVsbG8=; other=1");
        var container = new CookieContainer();

        session!.SeedCookies(container, ".glassdoor.com", out _);

        Assert.Equal("aGVsbG8=", container.GetCookies(new Uri("https://www.glassdoor.com/"))["token"]!.Value);
    }

    [Fact]
    public void IgnoresMalformedCookieEntries()
    {
        var session = BrowserSessionFile.Parse("Cookie: good=1; ; nonsense; =novalue; also_good=2");

        Assert.Equal(["good", "also_good"], session!.SeedCookies(new CookieContainer(), ".glassdoor.com", out _));
    }

    [Fact]
    public void SkipsCookiesTheContainerRejectsInsteadOfThrowing()
    {
        // g_state holds raw JSON; CookieContainer rejects the quotes and commas in it.
        var session = BrowserSessionFile.Parse("""Cookie: before=1; g_state={"i_l":0,"i_b":"x"}; after=2""");

        var names = session!.SeedCookies(new CookieContainer(), ".glassdoor.com", out var skipped);

        Assert.Equal(["before", "after"], names);
        Assert.Equal(["g_state"], skipped);
    }

    [Fact]
    public void LoadReturnsNullWhenTheFileIsMissing()
    {
        Assert.Null(BrowserSessionFile.Load(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.txt")));
    }

    [Fact]
    public void LoadReadsTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"session-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, RawHeaderBlock);

        try
        {
            var session = BrowserSessionFile.Load(path);

            Assert.NotNull(session);
            Assert.Contains("cf_clearance", session.Cookie);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
