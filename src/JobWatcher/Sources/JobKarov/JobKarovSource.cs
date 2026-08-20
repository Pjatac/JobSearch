using JobWatcher.Configuration;
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
            var url = JobKarovUrlBuilder.Build(options);
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
                "Source {Source}: JSON-LD blocks {JsonLdBlocks}, JobPosting objects {JobPostings}, deduplicated vacancies {VacancyCount}, requirements merged {RequirementsMerged}",
                options.Name,
                parseResult.JsonLdBlockCount,
                parseResult.JobPostingObjectCount,
                parseResult.Vacancies.Count,
                parseResult.RequirementsMergedCount);

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

    private async Task SaveDiagnosticHtmlAsync(string html, string sourceName, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
    {
        var diagnosticsDirectory = Path.Combine(watcherOptions.Value.DataDirectory, "diagnostics");
        Directory.CreateDirectory(diagnosticsDirectory);
        var path = Path.Combine(diagnosticsDirectory, $"{FileNames.ToSafeName(sourceName)}-{collectedAtUtc:yyyyMMddTHHmmssZ}.html");
        await File.WriteAllTextAsync(path, html, cancellationToken);
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
