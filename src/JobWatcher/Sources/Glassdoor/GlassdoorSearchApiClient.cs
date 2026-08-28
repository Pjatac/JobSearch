using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobWatcher.Http;
using Microsoft.Extensions.Logging;

namespace JobWatcher.Sources.Glassdoor;

/// <summary>
/// Calls Glassdoor's search API, the same endpoint the "Show more jobs" button uses.
/// </summary>
/// <remarks>
/// The button is a client-side control with no URL of its own, and Glassdoor ignores every
/// URL-based pagination form tried (the SEO <c>_IP&lt;N&gt;</c> suffix, <c>?p</c> and
/// <c>?pageNumber</c> all return page one). This endpoint is what actually pages. It takes a plain
/// JSON body and needs no CSRF token.
/// </remarks>
public sealed class GlassdoorSearchApiClient(HttpClient client, ILogger logger, string sourceName)
{
    public const string Endpoint = "https://www.glassdoor.com/job-search-next/bff/jobSearchResultsQuery";

    public async Task<HttpResponseMessage> SearchAsync(
        GlassdoorSearchUrl search,
        int pageNumber,
        string? pageCursor,
        int jobsPerPage,
        CancellationToken cancellationToken)
    {
        var request = new GlassdoorSearchApiRequest
        {
            Keyword = search.Keyword,
            LocationId = search.LocationId,
            LocationType = search.LocationType,
            NumJobsToShow = jobsPerPage,
            OriginalPageUrl = search.BuildOriginalPageUrl(),
            PageCursor = pageCursor,
            PageNumber = pageNumber,
            ParameterUrlInput = search.ParameterUrlInput,
            QueryString = search.BuildQueryString(),
            SeoFriendlyUrlInput = search.SeoFriendlyUrlInput
        };

        // Serialised through a source-generated context rather than reflection, so the request
        // body does not depend on reflection-based serialization being enabled.
        var json = JsonSerializer.Serialize(request, GlassdoorApiJsonContext.Default.GlassdoorSearchApiRequest);

        return await HttpRequestRetryPolicy.SendAsync(
            client,
            () =>
            {
                var message = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                // The session's default headers describe a document navigation, which is right for the
                // search page but wrong here: a browser issues this call as a fetch from that page. The
                // fetch-metadata headers, Accept, Origin and Referer are overridden to match, so the
                // request does not claim to be a top-level navigation to a JSON endpoint.
                message.Headers.TryAddWithoutValidation("accept", "*/*");
                message.Headers.TryAddWithoutValidation("sec-fetch-site", "same-origin");
                message.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
                message.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
                message.Headers.TryAddWithoutValidation("origin", "https://www.glassdoor.com");
                message.Headers.TryAddWithoutValidation("referer", search.BuildOriginalPageUrl());
                message.Headers.TryAddWithoutValidation("priority", "u=1, i");
                return message;
            },
            logger,
            sourceName,
            $"POST Glassdoor search API page {pageNumber}",
            cancellationToken);
    }
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(GlassdoorSearchApiRequest))]
internal sealed partial class GlassdoorApiJsonContext : JsonSerializerContext;

/// <summary>
/// The request body, mirroring a captured "Show more jobs" call field for field. Fields that
/// looked inert were kept anyway: replaying the site's own shape is safer than trimming it to what
/// seems necessary.
/// </summary>
public sealed record GlassdoorSearchApiRequest
{
    [JsonPropertyName("excludeJobListingIds")]
    public IReadOnlyList<long> ExcludeJobListingIds { get; init; } = [];

    [JsonPropertyName("filterParams")]
    public IReadOnlyList<object> FilterParams { get; init; } = [];

    [JsonPropertyName("includeIndeedJobAttributes")]
    public bool IncludeIndeedJobAttributes { get; init; } = true;

    [JsonPropertyName("keyword")]
    public required string Keyword { get; init; }

    [JsonPropertyName("locationId")]
    public required long LocationId { get; init; }

    [JsonPropertyName("locationType")]
    public required string LocationType { get; init; }

    [JsonPropertyName("numJobsToShow")]
    public required int NumJobsToShow { get; init; }

    [JsonPropertyName("originalPageUrl")]
    public required string OriginalPageUrl { get; init; }

    [JsonPropertyName("pageCursor")]
    public string? PageCursor { get; init; }

    [JsonPropertyName("pageNumber")]
    public required int PageNumber { get; init; }

    [JsonPropertyName("pageType")]
    public string PageType { get; init; } = "SERP";

    [JsonPropertyName("parameterUrlInput")]
    public required string ParameterUrlInput { get; init; }

    [JsonPropertyName("queryString")]
    public required string QueryString { get; init; }

    [JsonPropertyName("seoFriendlyUrlInput")]
    public required string SeoFriendlyUrlInput { get; init; }

    [JsonPropertyName("seoUrl")]
    public bool SeoUrl { get; init; } = true;
}
