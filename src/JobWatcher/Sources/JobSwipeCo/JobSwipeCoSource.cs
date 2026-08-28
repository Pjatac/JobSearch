using JobWatcher.Configuration;
using JobWatcher.Http;
using JobWatcher.Models;
using JobWatcher.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobWatcher.Sources.JobSwipeCo;

public sealed class JobSwipeCoSource(
    IHttpClientFactory httpClientFactory,
    JobSwipeCoJsonLdParser parser,
    IOptions<JobWatcherOptions> watcherOptions,
    ILogger<JobSwipeCoSource> logger) : IJobSource
{
    public const string HttpClientName = "JobSwipeCo";
    public string Name => "JobSwipeCo";

    public async Task<SourceRunResult> FetchAsync(JobSourceOptions options, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        try
        {
            if (options.JobSwipeCoFilter is null || options.JobSwipeCoFilter.SearchUrls.Count == 0)
            {
                return Failed(options.Name, warnings, $"Source '{options.Name}' must define jobSwipeCoFilter.searchUrls.");
            }

            using var client = httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, watcherOptions.Value.RequestTimeoutSeconds));

            var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
            var jobUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var skippedDetailsWithoutJobPosting = 0;
            var maxDetailsPerSearch = Math.Max(1, options.JobSwipeCoFilter.MaxDetailsPerSearch);

            foreach (var searchUrl in options.JobSwipeCoFilter.SearchUrls)
            {
                logger.LogInformation("Fetching source {Source} search page from {Url}", options.Name, searchUrl);
                using var response = await HttpRequestRetryPolicy.GetAsync(client, searchUrl, logger, options.Name, cancellationToken);
                var html = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    await SaveDiagnosticHtmlAsync(html, options.Name, collectedAtUtc, cancellationToken);
                    return Failed(options.Name, warnings, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                var searchResult = parser.ParseSearch(html);
                warnings.AddRange(searchResult.Warnings);
                logger.LogInformation(
                    "Source {Source}: search page JSON-LD blocks {JsonLdBlockCount}, item lists {ItemListCount}, job URLs {JobUrlCount}",
                    options.Name,
                    searchResult.JsonLdBlockCount,
                    searchResult.ItemListCount,
                    searchResult.JobUrls.Count);

                foreach (var jobUrl in searchResult.JobUrls.Take(maxDetailsPerSearch))
                {
                    jobUrls.Add(jobUrl);
                }
            }

            foreach (var jobUrl in jobUrls)
            {
                logger.LogInformation("Fetching source {Source} job detail from {Url}", options.Name, jobUrl);
                using var response = await HttpRequestRetryPolicy.GetAsync(client, jobUrl, logger, options.Name, cancellationToken);
                var html = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    warnings.Add($"Skipped JobSwipe.co detail {jobUrl}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
                    continue;
                }

                var jobResult = parser.ParseJob(html, options.Name, collectedAtUtc);
                warnings.AddRange(jobResult.Warnings);
                if (jobResult.Vacancy is not null)
                {
                    vacancies.TryAdd(jobResult.Vacancy.ExternalId, jobResult.Vacancy);
                }
                else
                {
                    skippedDetailsWithoutJobPosting++;
                }
            }

            if (skippedDetailsWithoutJobPosting > 0)
            {
                warnings.Add($"Skipped {skippedDetailsWithoutJobPosting} JobSwipe.co details without JobPosting JSON-LD.");
            }

            var orderedVacancies = vacancies.Values.OrderBy(v => v.ExternalId, StringComparer.OrdinalIgnoreCase).ToList();
            if (orderedVacancies.Count < options.MinimumExpectedVacancies)
            {
                return Failed(options.Name, warnings, $"Parsed {orderedVacancies.Count} vacancies, below minimum {options.MinimumExpectedVacancies}.");
            }

            return new SourceRunResult
            {
                Source = options.Name,
                Success = true,
                Snapshot = new SourceSnapshot
                {
                    Source = options.Name,
                    CollectedAtUtc = collectedAtUtc,
                    Vacancies = orderedVacancies
                },
                Warnings = warnings
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed(options.Name, warnings, ex.Message);
        }
    }

    private async Task SaveDiagnosticHtmlAsync(string html, string sourceName, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
    {
        var path = await DiagnosticFileWriter.WriteLatestAsync(watcherOptions.Value.DataDirectory, sourceName, collectedAtUtc, "html", html, cancellationToken);
        logger.LogWarning("Wrote diagnostic HTML for source {Source} to {Path}", sourceName, path);
    }

    private static SourceRunResult Failed(string sourceName, IReadOnlyList<string> warnings, string error)
    {
        return new SourceRunResult
        {
            Source = sourceName,
            Success = false,
            Error = error,
            Warnings = warnings
        };
    }
}
