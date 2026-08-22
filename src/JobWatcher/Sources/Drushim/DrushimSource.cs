using System.Text;
using JobWatcher.Configuration;
using JobWatcher.Models;
using JobWatcher.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobWatcher.Sources.Drushim;

public sealed class DrushimSource(
    IHttpClientFactory httpClientFactory,
    DrushimHtmlParser parser,
    DrushimApiParser apiParser,
    IOptions<JobWatcherOptions> watcherOptions,
    ILogger<DrushimSource> logger) : IJobSource
{
    public const string HttpClientName = "Drushim";
    public string Name => "Drushim";

    public async Task<SourceRunResult> FetchAsync(JobSourceOptions options, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        try
        {
            if (string.IsNullOrWhiteSpace(options.Url) && options.DrushimFilter is not null)
            {
                return await FetchApiAsync(options, collectedAtUtc, warnings, cancellationToken);
            }

            var url = DrushimUrlBuilder.Build(options);
            logger.LogInformation("Fetching source {Source} from {Url}", options.Name, url);
            using var client = httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, watcherOptions.Value.RequestTimeoutSeconds));

            using var response = await client.GetAsync(url, cancellationToken);
            var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var html = Encoding.UTF8.GetString(responseBytes);
            logger.LogInformation("Source {Source} HTTP {StatusCode}, response size {ResponseSize}", options.Name, (int)response.StatusCode, responseBytes.Length);

            if (!response.IsSuccessStatusCode)
            {
                await SaveDiagnosticHtmlAsync(html, options.Name, collectedAtUtc, cancellationToken);
                return Failed(options.Name, warnings, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var parseResult = parser.Parse(html, options.Name, collectedAtUtc);
            warnings.AddRange(parseResult.Warnings);
            logger.LogInformation(
                "Source {Source}: job cards {JobCards}, deduplicated vacancies {VacancyCount}",
                options.Name,
                parseResult.JobCardCount,
                parseResult.Vacancies.Count);

            if (parseResult.Vacancies.Count == 0 || parseResult.Vacancies.Count < options.MinimumExpectedVacancies)
            {
                await SaveDiagnosticHtmlAsync(html, options.Name, collectedAtUtc, cancellationToken);
                return Failed(options.Name, warnings, $"Parsed {parseResult.Vacancies.Count} vacancies, below minimum {options.MinimumExpectedVacancies}.");
            }

            return new SourceRunResult
            {
                Source = options.Name,
                Success = true,
                Snapshot = new SourceSnapshot
                {
                    Source = options.Name,
                    CollectedAtUtc = collectedAtUtc,
                    Vacancies = parseResult.Vacancies
                },
                Warnings = warnings
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed(options.Name, warnings, ex.Message);
        }
    }

    private async Task<SourceRunResult> FetchApiAsync(
        JobSourceOptions options,
        DateTimeOffset collectedAtUtc,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, watcherOptions.Value.RequestTimeoutSeconds));

        var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
        var page = 1;
        var totalPages = 1;
        int? totalSearchResultCount = null;

        while (page <= totalPages)
        {
            var url = DrushimUrlBuilder.BuildApiSearch(options, page);
            logger.LogInformation("Fetching source {Source} page {Page} from {Url}", options.Name, page, url);

            using var response = await client.GetAsync(url, cancellationToken);
            var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var json = Encoding.UTF8.GetString(responseBytes);
            logger.LogInformation(
                "Source {Source} page {Page} HTTP {StatusCode}, response size {ResponseSize}",
                options.Name,
                page,
                (int)response.StatusCode,
                responseBytes.Length);

            if (!response.IsSuccessStatusCode)
            {
                await SaveDiagnosticTextAsync(json, options.Name, collectedAtUtc, "json", cancellationToken);
                return Failed(options.Name, warnings, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var parseResult = apiParser.Parse(json, options.Name, collectedAtUtc);
            warnings.AddRange(parseResult.Warnings);
            totalPages = Math.Max(1, parseResult.TotalPages);
            totalSearchResultCount ??= parseResult.TotalSearchResultCount;

            foreach (var vacancy in parseResult.Vacancies)
            {
                vacancies.TryAdd(vacancy.ExternalId, vacancy);
            }

            logger.LogInformation(
                "Source {Source} page {Page}: API items {ApiItems}, page vacancies {PageVacancies}, total pages {TotalPages}, deduplicated vacancies {VacancyCount}",
                options.Name,
                page,
                parseResult.ResultItemCount,
                parseResult.Vacancies.Count,
                totalPages,
                vacancies.Count);

            if (parseResult.ResultItemCount == 0 || parseResult.NextPage is null || parseResult.NextPage <= page)
            {
                break;
            }

            page = parseResult.NextPage.Value;
        }

        if (totalSearchResultCount is not null && vacancies.Count < totalSearchResultCount)
        {
            warnings.Add($"Drushim API returned {vacancies.Count} unique vacancies, below advertised total {totalSearchResultCount}.");
        }

        var orderedVacancies = vacancies.Values.OrderBy(v => v.ExternalId, StringComparer.OrdinalIgnoreCase).ToList();
        if (orderedVacancies.Count == 0 || orderedVacancies.Count < options.MinimumExpectedVacancies)
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

    private async Task SaveDiagnosticHtmlAsync(string html, string sourceName, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
    {
        var path = await DiagnosticFileWriter.WriteLatestAsync(watcherOptions.Value.DataDirectory, sourceName, collectedAtUtc, "html", html, cancellationToken);
        logger.LogWarning("Wrote diagnostic HTML for source {Source} to {Path}", sourceName, path);
    }

    private async Task SaveDiagnosticTextAsync(string content, string sourceName, DateTimeOffset collectedAtUtc, string extension, CancellationToken cancellationToken)
    {
        var path = await DiagnosticFileWriter.WriteLatestAsync(watcherOptions.Value.DataDirectory, sourceName, collectedAtUtc, extension, content, cancellationToken);
        logger.LogWarning("Wrote diagnostic response for source {Source} to {Path}", sourceName, path);
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
