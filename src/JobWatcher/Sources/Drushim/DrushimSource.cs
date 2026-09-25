using System.Text;
using JobWatcher.Configuration;
using JobWatcher.Http;
using JobWatcher.Models;
using JobWatcher.Services;
using JobWatcher.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobWatcher.Sources.Drushim;

public sealed class DrushimSource(
    IHttpClientFactory httpClientFactory,
    DrushimHtmlParser parser,
    IOptions<JobWatcherOptions> watcherOptions,
    IRunPauseController pauseController,
    ILogger<DrushimSource> logger) : IJobSource
{
    public const string HttpClientName = "Drushim";
    public string Name => "Drushim";

    public async Task<SourceRunResult> FetchAsync(JobSourceOptions options, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, watcherOptions.Value.RequestTimeoutSeconds));

            var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
            string? lastHtml = null;
            foreach (var request in BuildRequests(options))
            {
                await pauseController.WaitIfPausedAsync(cancellationToken);

                if (request.CategoryId is null)
                {
                    logger.LogInformation("Fetching source {Source} from {Url}", options.Name, request.Url);
                }
                else
                {
                    logger.LogInformation("Fetching source {Source}, category {CategoryId} from {Url}", options.Name, request.CategoryId, request.Url);
                }

                using var response = await HttpRequestRetryPolicy.GetAsync(client, request.Url, logger, options.Name, cancellationToken);
                var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var html = Encoding.UTF8.GetString(responseBytes);
                lastHtml = html;
                logger.LogInformation(
                    "Source {Source}{CategorySuffix} HTTP {StatusCode}, response size {ResponseSize}",
                    options.Name,
                    request.CategoryId is null ? string.Empty : $", category {request.CategoryId}",
                    (int)response.StatusCode,
                    responseBytes.Length);

                if (!response.IsSuccessStatusCode)
                {
                    await SaveDiagnosticHtmlAsync(html, options.Name, collectedAtUtc, cancellationToken);
                    return Failed(options.Name, warnings, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                var parseResult = parser.Parse(html, options.Name, collectedAtUtc);
                warnings.AddRange(parseResult.Warnings);
                foreach (var vacancy in parseResult.Vacancies)
                {
                    vacancies.TryAdd(vacancy.ExternalId, vacancy);
                }

                logger.LogInformation(
                    "Source {Source}{CategorySuffix}: job cards {JobCards}, page vacancies {PageVacancies}, deduplicated vacancies {VacancyCount}",
                    options.Name,
                    request.CategoryId is null ? string.Empty : $", category {request.CategoryId}",
                    parseResult.JobCardCount,
                    parseResult.Vacancies.Count,
                    vacancies.Count);
            }

            var maximumDetails = Math.Max(0, options.DrushimFilter?.MaxDetailsPerSearch ?? 0);
            var listedVacancies = vacancies.Values.OrderBy(v => v.ExternalId, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var listedVacancy in listedVacancies.Take(maximumDetails))
            {
                await pauseController.WaitIfPausedAsync(cancellationToken);

                var detail = await LoadDetailsAsync(client, listedVacancy, options.Name, collectedAtUtc, warnings, cancellationToken);
                if (detail is not null)
                {
                    vacancies[listedVacancy.ExternalId] = listedVacancy with
                    {
                        Title = detail.Title,
                        Company = detail.Company ?? listedVacancy.Company,
                        Location = detail.Location ?? listedVacancy.Location,
                        Description = PreferLonger(detail.Description, listedVacancy.Description),
                        DatePosted = detail.DatePosted ?? listedVacancy.DatePosted,
                        EmploymentTypes = detail.EmploymentTypes.Count > 0 ? detail.EmploymentTypes : listedVacancy.EmploymentTypes
                    };
                }
            }

            var orderedVacancies = vacancies.Values.OrderBy(v => v.ExternalId, StringComparer.OrdinalIgnoreCase).ToList();
            logger.LogInformation(
                "Source {Source}: parsed {VacancyCount} vacancies and loaded {DetailCount} details",
                options.Name,
                orderedVacancies.Count,
                Math.Min(listedVacancies.Count, maximumDetails));

            if (orderedVacancies.Count == 0 || orderedVacancies.Count < options.MinimumExpectedVacancies)
            {
                if (lastHtml is not null)
                {
                    await SaveDiagnosticHtmlAsync(lastHtml, options.Name, collectedAtUtc, cancellationToken);
                }

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

    private async Task<JobVacancy?> LoadDetailsAsync(
        HttpClient client,
        JobVacancy listedVacancy,
        string sourceName,
        DateTimeOffset collectedAtUtc,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Fetching Drushim detail for source {Source} from {Url}", sourceName, listedVacancy.Url);
            using var response = await HttpRequestRetryPolicy.GetAsync(client, listedVacancy.Url, logger, sourceName, cancellationToken);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                warnings.Add($"Skipped Drushim detail {listedVacancy.Url}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
                return null;
            }

            return parser.ParseDetail(html, sourceName, listedVacancy.Url, collectedAtUtc);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add($"Skipped Drushim detail {listedVacancy.Url}: {ex.Message}");
            return null;
        }
    }

    private async Task SaveDiagnosticHtmlAsync(string html, string sourceName, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
    {
        var path = await DiagnosticFileWriter.WriteLatestAsync(watcherOptions.Value.DataDirectory, sourceName, collectedAtUtc, "html", html, cancellationToken);
        logger.LogWarning("Wrote diagnostic HTML for source {Source} to {Path}", sourceName, path);
    }

    private static IReadOnlyList<DrushimRequest> BuildRequests(JobSourceOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Url) || options.DrushimFilter is null)
        {
            return [new DrushimRequest(null, DrushimUrlBuilder.Build(options))];
        }

        return DrushimUrlBuilder.GetCategoryIds(options.DrushimFilter)
            .Select(categoryId => new DrushimRequest(categoryId, DrushimUrlBuilder.Build(options, categoryId)))
            .ToList();
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

    private static string? PreferLonger(string? detailDescription, string? listedDescription)
    {
        if (string.IsNullOrWhiteSpace(detailDescription))
        {
            return listedDescription;
        }

        if (string.IsNullOrWhiteSpace(listedDescription))
        {
            return detailDescription;
        }

        return detailDescription.Length >= listedDescription.Length ? detailDescription : listedDescription;
    }

    private sealed record DrushimRequest(int? CategoryId, string Url);
}
