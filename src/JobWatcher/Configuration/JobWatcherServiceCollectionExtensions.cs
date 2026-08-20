using System.Net;
using System.Net.Http.Headers;
using JobWatcher.Http;
using JobWatcher.Persistence;
using JobWatcher.Services;
using JobWatcher.Sources;
using JobWatcher.Sources.AllJobs;
using JobWatcher.Sources.Drushim;
using JobWatcher.Sources.Glassdoor;
using JobWatcher.Sources.JobKarov;
using JobWatcher.Sources.JobSwipeCo;
using JobWatcher.Sources.SecretTelAviv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobWatcher.Configuration;

public static class JobWatcherServiceCollectionExtensions
{
    public static IServiceCollection AddJobWatcherCollector(this IServiceCollection services)
    {
        services.AddSingleton<JobKarovJsonLdParser>();
        services.AddSingleton<AllJobsHtmlParser>();
        services.AddSingleton<DrushimHtmlParser>();
        services.AddSingleton<DrushimApiParser>();
        services.AddSingleton<JobSwipeCoJsonLdParser>();
        services.AddSingleton<GlassdoorHtmlParser>();
        services.AddSingleton<GlassdoorApiParser>();
        services.AddSingleton<SecretTelAvivHtmlParser>();
        services.AddSingleton<IJobSource, JobKarovSource>();
        services.AddSingleton<IJobSource, AllJobsSource>();
        services.AddSingleton<IJobSource, DrushimSource>();
        services.AddSingleton<IJobSource, JobSwipeCoSource>();
        services.AddSingleton<IJobSource, GlassdoorSource>();
        services.AddSingleton<IJobSource, SecretTelAvivSource>();
        services.AddSingleton<ISnapshotStore, JsonSnapshotStore>();
        services.AddSingleton<JobComparisonService>();
        services.AddSingleton<JobClassificationService>();
        services.AddSingleton<DuplicateCandidateService>();
        services.AddSingleton<OutputDuplicateService>();
        services.AddSingleton<JobWatcherRunner>();

        AddHtmlClient(services, JobKarovSource.HttpClientName, "JobWatcher/1.0 (+https://localhost/job-watcher; contact=local)", false, false);
        AddHtmlClient(services, AllJobsSource.HttpClientName, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36 JobWatcher/1.0", true, true);
        AddHtmlClient(services, DrushimSource.HttpClientName, "JobWatcher/1.0 (+https://localhost/job-watcher; contact=local)", false, false);
        AddHtmlClient(services, JobSwipeCoSource.HttpClientName, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36 JobWatcher/1.0", false, true);

        services.AddHttpClient(GlassdoorSource.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(provider => CreateGlassdoorHandler(provider))
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
        services.AddHttpClient(SecretTelAvivSource.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new TlsClientMessageHandler(BrowserSessionOptions.Chrome133Navigation()))
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
        return services;
    }

    private static void AddHtmlClient(IServiceCollection services, string name, string userAgent, bool useCookies, bool addHebrewLanguage)
    {
        services.AddHttpClient(name, client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
            if (addHebrewLanguage) client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("he-IL,he;q=0.9,en-US;q=0.8,en;q=0.7");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            UseCookies = useCookies,
            CookieContainer = new CookieContainer()
        });
    }

    private static TlsClientMessageHandler CreateGlassdoorHandler(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<JobWatcherOptions>>().Value;
        var logger = provider.GetRequiredService<ILogger<TlsClientMessageHandler>>();
        var path = BrowserSessionFile.GetDefaultPath(options.DataDirectory);
        var session = BrowserSessionFile.Load(path);
        var handler = new TlsClientMessageHandler(BrowserSessionOptions.Chrome133Navigation(session?.UserAgent, session?.AcceptLanguage));
        if (session is null) return handler;
        var cookieNames = session.SeedCookies(handler.Cookies, ".glassdoor.com", out var skippedNames);
        logger.LogInformation("Loaded {CookieCount} cookies from {Path} for Glassdoor: {CookieNames}", cookieNames.Count, path, string.Join(", ", cookieNames));
        if (skippedNames.Count > 0) logger.LogWarning("Skipped {SkippedCount} unsupported cookies: {SkippedNames}", skippedNames.Count, string.Join(", ", skippedNames));
        return handler;
    }
}
