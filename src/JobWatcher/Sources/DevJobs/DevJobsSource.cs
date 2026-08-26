using JobWatcher.Configuration;
using JobWatcher.Models;
using JobWatcher.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobWatcher.Sources.DevJobs;

public sealed class DevJobsSource(
    IHttpClientFactory httpClientFactory,
    DevJobsHtmlParser parser,
    IOptions<JobWatcherOptions> watcherOptions,
    ILogger<DevJobsSource> logger) : IJobSource
{
    public const string HttpClientName = "DevJobs";
    public string Name => "DevJobs";

    public async Task<SourceRunResult> FetchAsync(JobSourceOptions options, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Url) && options.DevJobsFilter is null)
        {
            return Failed(options.Name, warnings, $"Source '{options.Name}' must define devJobsFilter.searchUrl.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, watcherOptions.Value.RequestTimeoutSeconds));
            var filter = options.DevJobsFilter ?? new DevJobsFilterOptions();
            var maxPages = string.IsNullOrWhiteSpace(options.Url) ? Math.Max(1, filter.MaxPages) : 1;
            var maxDetails = Math.Max(0, filter.MaxDetailsPerPage);
            var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
            var jobCardCount = 0;
            string? latestSearchHtml = null;

            for (var page = 1; page <= maxPages; page++)
            {
                var searchUrl = !string.IsNullOrWhiteSpace(options.Url) ? options.Url : DevJobsUrlBuilder.Build(filter, page);
                logger.LogInformation("Fetching DevJobs source {Source}, page {Page} from {Url}", options.Name, page, searchUrl);
                using var response = await client.GetAsync(searchUrl, cancellationToken);
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                latestSearchHtml = html;
                if (!response.IsSuccessStatusCode)
                {
                    await SaveDiagnosticHtmlAsync(html, options.Name, collectedAtUtc, cancellationToken);
                    return Failed(options.Name, warnings, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                var search = parser.ParseSearch(html, options.Name, collectedAtUtc);
                warnings.AddRange(search.Warnings);
                jobCardCount += search.JobCardCount;
                var pageVacancies = new List<JobVacancy>();
                foreach (var listedVacancy in search.Vacancies)
                {
                    if (vacancies.TryAdd(listedVacancy.ExternalId, listedVacancy))
                    {
                        pageVacancies.Add(listedVacancy);
                    }
                }

                foreach (var listedVacancy in pageVacancies.Take(maxDetails))
                {
                    var vacancy = await LoadDetailsAsync(client, listedVacancy, options.Name, collectedAtUtc, warnings, cancellationToken);
                    vacancies[vacancy.ExternalId] = vacancy;
                }

                if (!search.HasNextPage)
                {
                    break;
                }
            }

            var orderedVacancies = vacancies.Values.OrderBy(vacancy => vacancy.ExternalId, StringComparer.OrdinalIgnoreCase).ToList();
            logger.LogInformation("DevJobs source {Source}: parsed {VacancyCount} vacancies from {JobCardCount} cards", options.Name, orderedVacancies.Count, jobCardCount);
            if (orderedVacancies.Count < options.MinimumExpectedVacancies)
            {
                if (latestSearchHtml is not null)
                {
                    await SaveDiagnosticHtmlAsync(latestSearchHtml, options.Name, collectedAtUtc, cancellationToken);
                }

                return Failed(options.Name, warnings, $"Parsed {orderedVacancies.Count} vacancies, below minimum {options.MinimumExpectedVacancies}.");
            }

            return new SourceRunResult
            {
                Source = options.Name,
                Success = true,
                Snapshot = new SourceSnapshot { Source = options.Name, CollectedAtUtc = collectedAtUtc, Vacancies = orderedVacancies },
                Warnings = warnings
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed(options.Name, warnings, ex.Message);
        }
    }

    private async Task<JobVacancy> LoadDetailsAsync(HttpClient client, JobVacancy listedVacancy, string sourceName, DateTimeOffset collectedAtUtc, List<string> warnings, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(listedVacancy.Url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            warnings.Add($"Skipped DevJobs detail {listedVacancy.Url}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
            return listedVacancy;
        }

        var detail = parser.ParseDetail(await response.Content.ReadAsStringAsync(cancellationToken), sourceName, listedVacancy.Url, collectedAtUtc);
        warnings.AddRange(detail.Warnings);
        return detail.Vacancy is null ? listedVacancy : listedVacancy with
        {
            Title = detail.Vacancy.Title,
            Company = detail.Vacancy.Company ?? listedVacancy.Company,
            Location = detail.Vacancy.Location ?? listedVacancy.Location,
            Description = detail.Vacancy.Description,
            DatePosted = detail.Vacancy.DatePosted ?? listedVacancy.DatePosted,
            EmploymentTypes = detail.Vacancy.EmploymentTypes
        };
    }

    private async Task SaveDiagnosticHtmlAsync(string html, string sourceName, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
    {
        var path = await DiagnosticFileWriter.WriteLatestAsync(watcherOptions.Value.DataDirectory, sourceName, collectedAtUtc, "html", html, cancellationToken);
        logger.LogWarning("Wrote diagnostic HTML for source {Source} to {Path}", sourceName, path);
    }

    private static SourceRunResult Failed(string sourceName, IReadOnlyList<string> warnings, string error) => new()
    {
        Source = sourceName,
        Success = false,
        Error = error,
        Warnings = warnings
    };
}
