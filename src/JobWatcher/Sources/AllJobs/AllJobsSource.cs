using System.Text;
using JobWatcher.Configuration;
using JobWatcher.Http;
using JobWatcher.Models;
using JobWatcher.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobWatcher.Sources.AllJobs;

public sealed class AllJobsSource(
    IHttpClientFactory httpClientFactory,
    AllJobsHtmlParser parser,
    IOptions<JobWatcherOptions> watcherOptions,
    ILogger<AllJobsSource> logger) : IJobSource
{
    public const string HttpClientName = "AllJobs";
    public string Name => "AllJobs";

    public async Task<SourceRunResult> FetchAsync(JobSourceOptions options, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, watcherOptions.Value.RequestTimeoutSeconds));

            var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
            var maxPages = Math.Max(1, options.AllJobsFilter?.MaxPages ?? 25);
            var filter = options.AllJobsFilter ?? new AllJobsFilterOptions();
            var positionIds = AllJobsUrlBuilder.GetPositionIds(filter);
            List<int?> positionSearches = positionIds.Count > 0 ? positionIds.Select<int, int?>(positionId => positionId).ToList() : [null];
            int? totalJobs = null;

            foreach (var positionId in positionSearches)
            {
                int? totalPages = null;
                int? positionTotalJobs = null;

                for (var page = 1; page <= Math.Min(totalPages ?? maxPages, maxPages); page++)
                {
                    var url = AllJobsUrlBuilder.Build(options, page, positionId);
                    logger.LogInformation("Fetching source {Source}, position {PositionId}, page {Page} from {Url}", options.Name, positionId, page, url);
                    using var response = await HttpRequestRetryPolicy.GetAsync(client, url, logger, options.Name, cancellationToken);
                    var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    var html = Encoding.UTF8.GetString(responseBytes);
                    logger.LogInformation(
                        "Source {Source}, position {PositionId}, page {Page} HTTP {StatusCode}, response size {ResponseSize}",
                        options.Name,
                        positionId,
                        page,
                        (int)response.StatusCode,
                        responseBytes.Length);

                    if (!response.IsSuccessStatusCode)
                    {
                        await SaveDiagnosticHtmlAsync(html, options.Name, collectedAtUtc, cancellationToken);
                        return Failed(options.Name, warnings, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                    }

                    var parseResult = parser.Parse(html, options.Name, collectedAtUtc);
                    warnings.AddRange(parseResult.Warnings);
                    totalPages ??= parseResult.TotalPages;
                    positionTotalJobs ??= parseResult.TotalJobs;

                    logger.LogInformation(
                        "Source {Source}, position {PositionId}, page {Page}: job cards {JobCards}, page vacancies {PageVacancies}, total pages {TotalPages}, deduplicated vacancies {VacancyCount}",
                        options.Name,
                        positionId,
                        page,
                        parseResult.JobCardCount,
                        parseResult.Vacancies.Count,
                        totalPages,
                        vacancies.Count + parseResult.Vacancies.Count);

                    if (parseResult.Vacancies.Count == 0)
                    {
                        if (html.Contains("Radware", StringComparison.OrdinalIgnoreCase))
                        {
                            warnings.Add($"AllJobs position {positionId?.ToString() ?? "all"} page {page} looked like a Radware interstitial.");
                        }

                        break;
                    }

                    foreach (var vacancy in parseResult.Vacancies)
                    {
                        vacancies.TryAdd(vacancy.ExternalId, vacancy);
                    }
                }

                if (totalPages is not null && totalPages > maxPages)
                {
                    warnings.Add($"AllJobs position {positionId?.ToString() ?? "all"} total pages {totalPages} exceeds configured maxPages {maxPages}.");
                }

                totalJobs = (totalJobs ?? 0) + (positionTotalJobs ?? 0);
            }

            var orderedVacancies = vacancies.Values.OrderBy(v => v.ExternalId, StringComparer.OrdinalIgnoreCase).ToList();
            if (orderedVacancies.Count == 0 || orderedVacancies.Count < options.MinimumExpectedVacancies)
            {
                return Failed(options.Name, warnings, $"Parsed {orderedVacancies.Count} vacancies, below minimum {options.MinimumExpectedVacancies}.");
            }

            if (totalJobs is not null && orderedVacancies.Count < totalJobs)
            {
                warnings.Add($"AllJobs parsed {orderedVacancies.Count} unique vacancies, below advertised total {totalJobs}.");
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
