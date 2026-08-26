using System.Net;
using System.Net.Http.Headers;
using JobWatcher.Configuration;
using JobWatcher.Http;
using JobWatcher.Persistence;
using JobWatcher.Services;
using JobWatcher.Sources;
using JobWatcher.Sources.AllJobs;
using JobWatcher.Sources.Drushim;
using JobWatcher.Sources.DevJobs;
using JobWatcher.Sources.Glassdoor;
using JobWatcher.Sources.JobKarov;
using JobWatcher.Sources.JobSwipeCo;
using JobWatcher.Sources.SecretTelAviv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false, reloadOnChange: false);
builder.Services.Configure<JobWatcherOptions>(builder.Configuration.GetSection("JobWatcher"));

builder.Services.AddSingleton<JobKarovJsonLdParser>();
builder.Services.AddSingleton<AllJobsHtmlParser>();
builder.Services.AddSingleton<DrushimHtmlParser>();
builder.Services.AddSingleton<DrushimApiParser>();
builder.Services.AddSingleton<JobSwipeCoJsonLdParser>();
builder.Services.AddSingleton<GlassdoorHtmlParser>();
builder.Services.AddSingleton<GlassdoorApiParser>();
builder.Services.AddSingleton<SecretTelAvivHtmlParser>();
builder.Services.AddSingleton<DevJobsHtmlParser>();
builder.Services.AddSingleton<IJobSource, JobKarovSource>();
builder.Services.AddSingleton<IJobSource, AllJobsSource>();
builder.Services.AddSingleton<IJobSource, DrushimSource>();
builder.Services.AddSingleton<IJobSource, JobSwipeCoSource>();
builder.Services.AddSingleton<IJobSource, GlassdoorSource>();
builder.Services.AddSingleton<IJobSource, SecretTelAvivSource>();
builder.Services.AddSingleton<IJobSource, DevJobsSource>();
builder.Services.AddSingleton<ISnapshotStore, JsonSnapshotStore>();
builder.Services.AddSingleton<JobComparisonService>();
builder.Services.AddSingleton<JobClassificationService>();
builder.Services.AddSingleton<DuplicateCandidateService>();
builder.Services.AddSingleton<OutputDuplicateService>();
builder.Services.AddSingleton<JobWatcherRunner>();

builder.Services
    .AddHttpClient(JobKarovSource.HttpClientName, client =>
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("JobWatcher/1.0 (+https://localhost/job-watcher; contact=local)");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
    });

builder.Services
    .AddHttpClient(AllJobsSource.HttpClientName, client =>
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36 JobWatcher/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("he-IL,he;q=0.9,en-US;q=0.8,en;q=0.7");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        UseCookies = true,
        CookieContainer = new CookieContainer()
    });

builder.Services
    .AddHttpClient(DrushimSource.HttpClientName, client =>
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("JobWatcher/1.0 (+https://localhost/job-watcher; contact=local)");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
    });

builder.Services
    .AddHttpClient(JobSwipeCoSource.HttpClientName, client =>
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36 JobWatcher/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("he-IL,he;q=0.9,en-US;q=0.8,en;q=0.7");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
    });

builder.Services
    .AddHttpClient(SecretTelAvivSource.HttpClientName)
    .ConfigurePrimaryHttpMessageHandler(() => new TlsClientMessageHandler(BrowserSessionOptions.Chrome133Navigation()))
    .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

builder.Services.AddBrowserTlsClient(DevJobsSource.HttpClientName);

// Glassdoor rejects the default .NET TLS fingerprint with 403 plus an anti-bot challenge page,
// so this is the one source that goes through TlsClient's browser-fingerprinted handler. No
// default headers are set here on purpose: the Chrome preset supplies a matching header set, and
// overriding parts of it is what makes a request look synthetic again.
// The handler owns the TLS session, and with it the cookie jar, the TLS 1.3 tickets and the
// pooled HTTP/2 connection. HttpClientFactory rotates primary handlers every two minutes by
// default, which would silently discard all three mid-run and make every rotation look like a
// brand-new visitor. This process is short-lived, so the handler lives for its whole run.
builder.Services
    .AddHttpClient(GlassdoorSource.HttpClientName)
    .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
    {
        var watcherOptions = serviceProvider.GetRequiredService<IOptions<JobWatcherOptions>>().Value;
        var logger = serviceProvider.GetRequiredService<ILogger<TlsClientMessageHandler>>();

        var sessionPath = BrowserSessionFile.GetDefaultPath(watcherOptions.DataDirectory);
        var browserSession = BrowserSessionFile.Load(sessionPath);

        var handler = new TlsClientMessageHandler(
            BrowserSessionOptions.Chrome133Navigation(browserSession?.UserAgent, browserSession?.AcceptLanguage));
        if (browserSession is null)
        {
            logger.LogInformation("No exported browser session at {Path}; Glassdoor requests are anonymous", sessionPath);
            return handler;
        }

        // Names only. The values are a live browser session and are never logged.
        var cookieNames = browserSession.SeedCookies(handler.Cookies, ".glassdoor.com", out var skippedNames);
        logger.LogInformation(
            "Loaded {CookieCount} cookies from {Path} for Glassdoor: {CookieNames}",
            cookieNames.Count,
            sessionPath,
            string.Join(", ", cookieNames));

        if (skippedNames.Count > 0)
        {
            logger.LogWarning("Skipped {SkippedCount} unsupported cookies: {SkippedNames}", skippedNames.Count, string.Join(", ", skippedNames));
        }

        if (string.IsNullOrWhiteSpace(browserSession.UserAgent))
        {
            logger.LogWarning(
                "Exported session at {Path} has no user-agent line; cf_clearance is bound to the exporting browser's User-Agent and is likely to be rejected",
                sessionPath);
        }

        return handler;
    })
    .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

using var host = builder.Build();
var runner = host.Services.GetRequiredService<JobWatcherRunner>();
return await runner.RunAsync(CancellationToken.None);
