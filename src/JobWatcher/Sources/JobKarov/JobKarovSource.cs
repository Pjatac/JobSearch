using JobWatcher.Configuration;
using JobWatcher.Http;
using JobWatcher.Models;
using JobWatcher.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace JobWatcher.Sources.JobKarov;

public sealed class JobKarovSource(
    IHttpClientFactory httpClientFactory,
    JobKarovJsonLdParser parser,
    IOptions<JobWatcherOptions> watcherOptions,
    ILogger<JobKarovSource> logger) : IJobSource
{
    public const string HttpClientName = "JobKarov";
    public string Name => "JobKarov";

    public async Task<SourceRunResult> FetchAsync(JobSourceOptions options, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, watcherOptions.Value.RequestTimeoutSeconds));
            var specialities = string.IsNullOrWhiteSpace(options.Url)
                ? JobKarovUrlBuilder.GetSpecialities(options)
                : new[] { string.Empty };
            if (specialities.Count == 0)
            {
                specialities = !string.IsNullOrWhiteSpace(options.JobKarovFilter?.Query)
                    ? [string.Empty]
                    : [];
            }

            if (specialities.Count == 0)
            {
                return Failed(options.Name, warnings, "No JobKarov categories or search query are selected.");
            }

            var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
            string? latestHtml = null;

            foreach (var speciality in specialities)
            {
                var url = JobKarovUrlBuilder.Build(options, speciality);
                logger.LogInformation("Fetching JobKarov source {Source}, speciality {Speciality} from {Url}", options.Name, speciality, url);
                using var response = await HttpRequestRetryPolicy.GetAsync(client, url, logger, options.Name, cancellationToken);
                var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var html = Encoding.UTF8.GetString(responseBytes);
                latestHtml = html;
                logger.LogInformation("JobKarov source {Source}, speciality {Speciality}: HTTP {StatusCode}, response size {ResponseSize}", options.Name, speciality, (int)response.StatusCode, responseBytes.Length);

                if (!response.IsSuccessStatusCode)
                {
                    await SaveDiagnosticHtmlAsync(html, options.Name, collectedAtUtc, cancellationToken);
                    return Failed(options.Name, warnings, $"JobKarov speciality {speciality}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                var parseResult = parser.Parse(html, options.Name, collectedAtUtc);
                warnings.AddRange(parseResult.Warnings);
                logger.LogInformation(
                    "JobKarov source {Source}, speciality {Speciality}: JSON-LD blocks {JsonLdBlocks}, JobPosting objects {JobPostings}, parsed vacancies {VacancyCount}, requirements merged {RequirementsMerged}",
                    options.Name,
                    speciality,
                    parseResult.JsonLdBlockCount,
                    parseResult.JobPostingObjectCount,
                    parseResult.Vacancies.Count,
                    parseResult.RequirementsMergedCount);
                foreach (var vacancy in parseResult.Vacancies)
                {
                    vacancies.TryAdd(vacancy.ExternalId, vacancy);
                }
            }

            if (vacancies.Count < options.MinimumExpectedVacancies)
            {
                if (latestHtml is not null)
                {
                    await SaveDiagnosticHtmlAsync(latestHtml, options.Name, collectedAtUtc, cancellationToken);
                }

                return Failed(options.Name, warnings, $"Parsed {vacancies.Count} vacancies, below minimum {options.MinimumExpectedVacancies}.");
            }

            return new SourceRunResult
            {
                Source = options.Name,
                Success = true,
                Snapshot = new SourceSnapshot
                {
                    Source = options.Name,
                    CollectedAtUtc = collectedAtUtc,
                    Vacancies = vacancies.Values.OrderBy(vacancy => vacancy.ExternalId, StringComparer.OrdinalIgnoreCase).ToList()
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
