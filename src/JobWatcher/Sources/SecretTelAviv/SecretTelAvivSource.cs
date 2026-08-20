using JobWatcher.Configuration;
using JobWatcher.Models;
using JobWatcher.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobWatcher.Sources.SecretTelAviv;

public sealed class SecretTelAvivSource(
    IHttpClientFactory httpClientFactory,
    SecretTelAvivHtmlParser parser,
    IOptions<JobWatcherOptions> watcherOptions,
    ILogger<SecretTelAvivSource> logger) : IJobSource
{
    public const string HttpClientName = "SecretTelAviv";
    public string Name => "SecretTelAviv";

    public async Task<SourceRunResult> FetchAsync(JobSourceOptions options, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var url = !string.IsNullOrWhiteSpace(options.Url) ? options.Url : options.SecretTelAvivFilter?.SearchUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return Failed(options.Name, warnings, $"Source '{options.Name}' must define secretTelAvivFilter.searchUrl.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, watcherOptions.Value.RequestTimeoutSeconds));
            logger.LogInformation("Fetching source {Source} from {Url}", options.Name, url);
            using var response = await client.GetAsync(url, cancellationToken);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await SaveDiagnosticHtmlAsync(html, options.Name, collectedAtUtc, cancellationToken);
                return Failed(options.Name, warnings, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var parseResult = parser.Parse(html, options.Name, collectedAtUtc);
            warnings.AddRange(parseResult.Warnings);
            var vacancies = parseResult.Vacancies.ToDictionary(vacancy => vacancy.ExternalId, StringComparer.OrdinalIgnoreCase);
            var maximumDetails = Math.Max(0, options.SecretTelAvivFilter?.MaxDetailsPerSearch ?? 0);
            foreach (var listedVacancy in parseResult.Vacancies.Take(maximumDetails))
            {
                using var detailResponse = await client.GetAsync(listedVacancy.Url, cancellationToken);
                if (!detailResponse.IsSuccessStatusCode)
                {
                    warnings.Add($"Skipped Secret Tel Aviv detail {listedVacancy.Url}: HTTP {(int)detailResponse.StatusCode} {detailResponse.ReasonPhrase}.");
                    continue;
                }

                var detailResult = parser.ParseDetail(await detailResponse.Content.ReadAsStringAsync(cancellationToken));
                warnings.AddRange(detailResult.Warnings);
                if (detailResult.Details is { } details)
                {
                    vacancies[listedVacancy.ExternalId] = listedVacancy with
                    {
                        Title = details.Title ?? listedVacancy.Title,
                        Company = details.Company ?? listedVacancy.Company,
                        Location = details.Location ?? listedVacancy.Location,
                        Description = details.Description,
                        DatePosted = details.DatePosted,
                        ValidThrough = details.ValidThrough,
                        EmploymentTypes = details.EmploymentTypes.Count > 0 ? details.EmploymentTypes : listedVacancy.EmploymentTypes
                    };
                }
                else
                {
                    warnings.Add($"Skipped Secret Tel Aviv detail {listedVacancy.Url}: no JobPosting JSON-LD.");
                }
            }

            var orderedVacancies = vacancies.Values.OrderBy(vacancy => vacancy.ExternalId, StringComparer.OrdinalIgnoreCase).ToList();
            logger.LogInformation(
                "Source {Source}: parsed {VacancyCount} vacancies from {JobCardCount} cards and loaded {DetailCount} details",
                options.Name,
                orderedVacancies.Count,
                parseResult.JobCardCount,
                Math.Min(parseResult.Vacancies.Count, maximumDetails));

            if (orderedVacancies.Count < options.MinimumExpectedVacancies)
            {
                await SaveDiagnosticHtmlAsync(html, options.Name, collectedAtUtc, cancellationToken);
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
        var diagnosticsDirectory = Path.Combine(watcherOptions.Value.DataDirectory, "diagnostics");
        Directory.CreateDirectory(diagnosticsDirectory);
        var path = Path.Combine(diagnosticsDirectory, $"{FileNames.ToSafeName(sourceName)}-{collectedAtUtc:yyyyMMddTHHmmssZ}.html");
        await File.WriteAllTextAsync(path, html, cancellationToken);
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
