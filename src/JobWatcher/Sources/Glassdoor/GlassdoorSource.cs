using JobWatcher.Configuration;
using JobWatcher.Http;
using JobWatcher.Models;
using JobWatcher.Services;
using JobWatcher.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobWatcher.Sources.Glassdoor;

/// <summary>
/// Glassdoor blocks ordinary .NET TLS fingerprints, so the named client behind
/// <see cref="HttpClientName"/> is wired to a browser-fingerprinted primary handler in
/// <c>Program.cs</c>. This adapter is unaware of that: it just resolves its named client.
/// </summary>
/// <remarks>
/// Results come from the search API, which pages through cursors and carries an exact listing age.
/// The server-rendered HTML is kept as a fallback for when the API call fails: it still yields the
/// first 30 results, which is better than nothing.
/// </remarks>
public sealed class GlassdoorSource(
    IHttpClientFactory httpClientFactory,
    GlassdoorHtmlParser parser,
    GlassdoorApiParser apiParser,
    IOptions<JobWatcherOptions> watcherOptions,
    IRunPauseController pauseController,
    ILogger<GlassdoorSource> logger) : IJobSource
{
    public const string HttpClientName = "Glassdoor";
    private const double DefaultRequestDelaySeconds = 1;

    public string Name => "Glassdoor";

    public async Task<SourceRunResult> FetchAsync(JobSourceOptions options, DateTimeOffset collectedAtUtc, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        try
        {
            var urls = options.GlassdoorFilter?.SearchUrls.Count > 0
                ? options.GlassdoorFilter.SearchUrls
                : !string.IsNullOrWhiteSpace(options.Url)
                    ? [options.Url]
                    : [];

            if (urls.Count == 0)
            {
                return Failed(options.Name, warnings, $"Source '{options.Name}' must define glassdoorFilter.searchUrls or url.");
            }

            using var client = httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, watcherOptions.Value.RequestTimeoutSeconds));

            var apiClient = new GlassdoorSearchApiClient(client, logger, options.Name);
            var requestDelay = TimeSpan.FromSeconds(Math.Max(0, options.GlassdoorFilter?.RequestDelaySeconds ?? DefaultRequestDelaySeconds));
            var maxPages = Math.Max(1, options.GlassdoorFilter?.MaxPages ?? 7);
            var jobsPerPage = Math.Max(1, options.GlassdoorFilter?.JobsPerPage ?? 30);
            var maxDetails = Math.Max(0, options.GlassdoorFilter?.MaxDetailsPerSearch ?? 30);

            var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
            var isFirstRequest = true;

            foreach (var url in urls)
            {
                await pauseController.WaitIfPausedAsync(cancellationToken);

                var search = GlassdoorSearchUrl.TryParse(url);
                if (search is null)
                {
                    warnings.Add($"Could not decode Glassdoor search parameters from '{url}'; falling back to the first HTML page only.");
                    var htmlOnly = await FetchHtmlPageAsync(client, url, options, collectedAtUtc, warnings, Pace, cancellationToken);
                    if (htmlOnly.Error is not null)
                    {
                        return Failed(options.Name, warnings, htmlOnly.Error);
                    }

                    Add(htmlOnly.Vacancies);
                    continue;
                }

                var pagesFetched = 0;
                var cursors = new Dictionary<int, string>();
                var pageNumber = 1;
                string? cursor = null;

                while (pagesFetched < maxPages)
                {
                    await pauseController.WaitIfPausedAsync(cancellationToken);
                    await Pace(cancellationToken);

                    logger.LogInformation(
                        "Fetching source {Source} page {PageNumber} for '{Keyword}' via the search API",
                        options.Name,
                        pageNumber,
                        search.Keyword);

                    using var response = await apiClient.SearchAsync(search, pageNumber, cursor, jobsPerPage, cancellationToken);
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);

                    var challenge = GlassdoorChallengeDetector.Detect(response.StatusCode, body);
                    if (challenge is not null)
                    {
                        await SaveDiagnosticAsync(body, options.Name, collectedAtUtc, "html", cancellationToken);
                        return Failed(options.Name, warnings, challenge);
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        // Page one failing leaves nothing at all, so the HTML page is worth trying.
                        // A later page failing just ends the walk with what has been collected.
                        if (pageNumber > 1)
                        {
                            warnings.Add($"Glassdoor search API returned HTTP {(int)response.StatusCode} for page {pageNumber}; kept {vacancies.Count} vacancies from earlier pages.");
                            break;
                        }

                        warnings.Add($"Glassdoor search API returned HTTP {(int)response.StatusCode}; falling back to the server-rendered page.");
                        var fallback = await FetchHtmlPageAsync(client, url, options, collectedAtUtc, warnings, Pace, cancellationToken);
                        if (fallback.Error is not null)
                        {
                            return Failed(options.Name, warnings, fallback.Error);
                        }

                        Add(fallback.Vacancies);
                        break;
                    }

                    var result = apiParser.Parse(body, options.Name, collectedAtUtc);
                    warnings.AddRange(result.Warnings);
                    pagesFetched++;

                    logger.LogInformation(
                        "Source {Source} page {PageNumber}: vacancies {VacancyCount}, total jobs {TotalJobs}, cursors for pages {CursorPages}",
                        options.Name,
                        pageNumber,
                        result.Vacancies.Count,
                        result.TotalJobs,
                        string.Join(", ", result.Cursors.Keys.Order()));

                    if (result.Vacancies.Count == 0)
                    {
                        await SaveDiagnosticAsync(body, options.Name, collectedAtUtc, "json", cancellationToken);
                        if (pageNumber == 1)
                        {
                            warnings.Add("Glassdoor search API returned no vacancies on the first page.");
                        }

                        break;
                    }

                    Add(result.Vacancies);

                    foreach (var (page, value) in result.Cursors)
                    {
                        cursors[page] = value;
                    }

                    // Cursors arrive for the other pages, never for the one just fetched, so the
                    // walk stops naturally when Glassdoor stops offering a next page.
                    if (!cursors.TryGetValue(pageNumber + 1, out var nextCursor))
                    {
                        break;
                    }

                    pageNumber++;
                    cursor = nextCursor;
                }
            }

            var enriched = await EnrichDetailsAsync(client, vacancies.Values.ToList(), options, collectedAtUtc, warnings, Pace, maxDetails, cancellationToken);
            var orderedVacancies = enriched.OrderBy(v => v.ExternalId, StringComparer.OrdinalIgnoreCase).ToList();
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

            void Add(IEnumerable<JobVacancy> parsed)
            {
                foreach (var vacancy in parsed)
                {
                    vacancies.TryAdd(vacancy.ExternalId, vacancy);
                }
            }

            async Task Pace(CancellationToken token)
            {
                if (!isFirstRequest && requestDelay > TimeSpan.Zero)
                {
                    logger.LogInformation("Waiting {DelaySeconds:0.##}s before the next {Source} request", requestDelay.TotalSeconds, options.Name);
                    await Task.Delay(requestDelay, token);
                }

                isFirstRequest = false;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failed(options.Name, warnings, ex.Message);
        }
    }

    private async Task<IReadOnlyList<JobVacancy>> EnrichDetailsAsync(
        HttpClient client,
        IReadOnlyList<JobVacancy> vacancies,
        JobSourceOptions options,
        DateTimeOffset collectedAtUtc,
        List<string> warnings,
        Func<CancellationToken, Task> pace,
        int maxDetails,
        CancellationToken cancellationToken)
    {
        if (maxDetails <= 0 || vacancies.Count == 0)
        {
            return vacancies;
        }

        var enriched = new List<JobVacancy>(vacancies.Count);
        var detailCount = 0;
        var detailFailures = 0;
        var detailParseMisses = 0;

        logger.LogInformation(
            "Enriching source {Source} details for up to {MaxDetails} of {VacancyCount} Glassdoor vacancies",
            options.Name,
            maxDetails,
            vacancies.Count);

        foreach (var vacancy in vacancies)
        {
            await pauseController.WaitIfPausedAsync(cancellationToken);

            if (detailCount >= maxDetails)
            {
                enriched.Add(vacancy);
                continue;
            }

            detailCount++;
            await pace(cancellationToken);
            logger.LogInformation("Fetching source {Source} details for listing {ListingId}", options.Name, vacancy.ExternalId);

            using var response = await HttpRequestRetryPolicy.SendAsync(
                client,
                () =>
                {
                    var message = new HttpRequestMessage(HttpMethod.Get, BuildDetailApiUrl(vacancy));
                    message.Headers.TryAddWithoutValidation("accept", "*/*");
                    message.Headers.TryAddWithoutValidation("sec-fetch-site", "same-origin");
                    message.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
                    message.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
                    message.Headers.TryAddWithoutValidation("referer", "https://www.glassdoor.com/");
                    message.Headers.TryAddWithoutValidation("priority", "u=1, i");
                    return message;
                },
                logger,
                options.Name,
                $"GET Glassdoor job details {vacancy.ExternalId}",
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            var challenge = GlassdoorChallengeDetector.Detect(response.StatusCode, body);
            if (challenge is not null)
            {
                await SaveDiagnosticAsync(body, options.Name, collectedAtUtc, "json", cancellationToken);
                warnings.Add($"Stopped Glassdoor detail enrichment after {detailCount - 1} listings: {challenge}");
                enriched.Add(vacancy);
                enriched.AddRange(vacancies.Skip(detailCount));
                detailFailures++;
                break;
            }

            if (!response.IsSuccessStatusCode)
            {
                warnings.Add($"Glassdoor detail API returned HTTP {(int)response.StatusCode} for listing {vacancy.ExternalId}; kept listing without detail description.");
                enriched.Add(vacancy);
                detailFailures++;
                continue;
            }

            var parsed = apiParser.ParseDetail(body, vacancy);
            if (parsed is null)
            {
                detailParseMisses++;
                logger.LogInformation(
                    "Glassdoor detail API response for listing {ListingId} did not include a usable description",
                    vacancy.ExternalId);
                enriched.Add(vacancy);
                continue;
            }

            enriched.Add(parsed);
        }

        if (vacancies.Count > maxDetails)
        {
            warnings.Add($"Glassdoor detail enrichment limited to {maxDetails} of {vacancies.Count} vacancies.");
        }

        if (detailFailures > 0)
        {
            warnings.Add($"Glassdoor detail enrichment failed for {detailFailures} of {detailCount} attempted vacancies.");
        }

        if (detailParseMisses > 0)
        {
            warnings.Add($"Glassdoor detail enrichment received no usable description for {detailParseMisses} of {detailCount} successful detail responses.");
        }

        return enriched;
    }

    private static string BuildDetailApiUrl(JobVacancy vacancy)
    {
        var query = Uri.EscapeDataString(vacancy.Url);
        return $"https://www.glassdoor.com/job-listing/api/job-details?jobListingId={Uri.EscapeDataString(vacancy.ExternalId)}&pageTypeEnum=SERP&queryString={query}&countryId=1";
    }

    private async Task<(IReadOnlyList<JobVacancy> Vacancies, string? Error)> FetchHtmlPageAsync(
        HttpClient client,
        string url,
        JobSourceOptions options,
        DateTimeOffset collectedAtUtc,
        List<string> warnings,
        Func<CancellationToken, Task> pace,
        CancellationToken cancellationToken)
    {
        await pace(cancellationToken);
        await pauseController.WaitIfPausedAsync(cancellationToken);
        logger.LogInformation("Fetching source {Source} search page from {Url}", options.Name, url);

        using var response = await HttpRequestRetryPolicy.GetAsync(client, url, logger, options.Name, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        var challenge = GlassdoorChallengeDetector.Detect(response.StatusCode, html);
        if (challenge is not null)
        {
            await SaveDiagnosticAsync(html, options.Name, collectedAtUtc, "html", cancellationToken);
            return ([], challenge);
        }

        if (!response.IsSuccessStatusCode)
        {
            await SaveDiagnosticAsync(html, options.Name, collectedAtUtc, "html", cancellationToken);
            return ([], $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var result = parser.Parse(html, options.Name, collectedAtUtc);
        warnings.AddRange(result.Warnings);
        if (result.TotalJobs is not null && result.TotalJobs > result.Vacancies.Count)
        {
            warnings.Add($"Glassdoor search page exposed {result.Vacancies.Count} vacancies out of advertised total {result.TotalJobs}.");
        }

        if (result.Vacancies.Count == 0)
        {
            await SaveDiagnosticAsync(html, options.Name, collectedAtUtc, "html", cancellationToken);
        }

        return (result.Vacancies, null);
    }

    private async Task SaveDiagnosticAsync(string content, string sourceName, DateTimeOffset collectedAtUtc, string extension, CancellationToken cancellationToken)
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
