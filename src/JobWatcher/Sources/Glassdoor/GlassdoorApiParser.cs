using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using JobWatcher.Models;

namespace JobWatcher.Sources.Glassdoor;

/// <summary>
/// Reads a response from Glassdoor's <c>jobSearchResultsQuery</c> search API.
/// </summary>
/// <remarks>
/// This carries more than the rendered cards do. Most usefully <c>ageInDays</c> is an exact
/// number, where the HTML only shows a bucketed label such as "30d+", so the posting date is real
/// rather than a lower bound. The response also lists pagination cursors for the other pages,
/// which is what makes paging past the first 30 results possible at all.
/// </remarks>
public sealed partial class GlassdoorApiParser
{
    public GlassdoorApiParseResult Parse(string json, string sourceName, DateTimeOffset collectedAtUtc)
    {
        var warnings = new List<string>();

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return new GlassdoorApiParseResult([], new Dictionary<int, string>(), null, [$"Malformed Glassdoor API response: {ex.Message}"]);
        }

        using (document)
        {
            if (!TryGetObject(document.RootElement, "data", out var data) ||
                !TryGetObject(data, "jobListings", out var jobListings))
            {
                return new GlassdoorApiParseResult([], new Dictionary<int, string>(), null, ["Glassdoor API response has no data.jobListings object."]);
            }

            var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
            var skipped = 0;

            if (jobListings.TryGetProperty("jobListings", out var listings) && listings.ValueKind == JsonValueKind.Array)
            {
                foreach (var listing in listings.EnumerateArray())
                {
                    var vacancy = ParseListing(listing, sourceName, collectedAtUtc);
                    if (vacancy is null)
                    {
                        skipped++;
                        continue;
                    }

                    vacancies.TryAdd(vacancy.ExternalId, vacancy);
                }
            }

            if (skipped > 0)
            {
                warnings.Add($"Skipped {skipped} Glassdoor API listings without title, URL, or listing id.");
            }

            return new GlassdoorApiParseResult(
                vacancies.Values.OrderBy(v => v.ExternalId, StringComparer.OrdinalIgnoreCase).ToList(),
                ParseCursors(jobListings),
                jobListings.TryGetProperty("totalJobsCount", out var total) && total.TryGetInt32(out var totalJobs) ? totalJobs : null,
                warnings);
        }
    }

    private static JobVacancy? ParseListing(JsonElement listing, string sourceName, DateTimeOffset collectedAtUtc)
    {
        if (!TryGetObject(listing, "jobview", out var jobview))
        {
            return null;
        }

        TryGetObject(jobview, "header", out var header);
        TryGetObject(jobview, "job", out var job);

        var id = GetString(job, "listingId") ?? ExtractListingIdFromUrl(GetString(header, "seoJobLink"));
        var title = CleanText(GetString(header, "jobTitleText") ?? GetString(job, "jobTitleText"));
        var url = CleanText(GetString(header, "seoJobLink"));

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return new JobVacancy
        {
            Source = sourceName,
            ExternalId = id,
            Title = title,
            Company = CleanText(GetString(header, "employerNameFromSearch")
                ?? (TryGetObject(header, "employer", out var employer) ? GetString(employer, "name") : null)),
            Location = CleanText(GetString(header, "locationName")),
            Url = url,
            Description = ParseDescription(job),
            DatePosted = ParseAgeInDays(header, collectedAtUtc),
            CollectedAtUtc = collectedAtUtc
        };
    }

    private static IReadOnlyDictionary<int, string> ParseCursors(JsonElement jobListings)
    {
        var cursors = new Dictionary<int, string>();
        if (!jobListings.TryGetProperty("paginationCursors", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return cursors;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("pageNumber", out var pageNumber) &&
                pageNumber.TryGetInt32(out var page) &&
                item.TryGetProperty("cursor", out var cursor) &&
                cursor.ValueKind == JsonValueKind.String &&
                cursor.GetString() is { Length: > 0 } value)
            {
                cursors[page] = value;
            }
        }

        return cursors;
    }

    private static DateOnly? ParseAgeInDays(JsonElement header, DateTimeOffset collectedAtUtc)
    {
        return header.ValueKind == JsonValueKind.Object &&
            header.TryGetProperty("ageInDays", out var age) &&
            age.TryGetInt32(out var days) &&
            days >= 0
            ? DateOnly.FromDateTime(collectedAtUtc.UtcDateTime.Date).AddDays(-days)
            : null;
    }

    private static string? ParseDescription(JsonElement job)
    {
        if (job.ValueKind != JsonValueKind.Object ||
            !job.TryGetProperty("descriptionFragmentsText", out var fragments) ||
            fragments.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var text = string.Join(' ', fragments.EnumerateArray()
            .Where(fragment => fragment.ValueKind == JsonValueKind.String)
            .Select(fragment => fragment.GetString())
            .Where(fragment => !string.IsNullOrWhiteSpace(fragment)));

        return CleanDescription(text);
    }

    private static string? ExtractListingIdFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var match = ListingIdRegex().Match(url);
        return match.Success ? match.Groups["id"].Value : null;
    }

    private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var child) &&
            child.ValueKind == JsonValueKind.Object)
        {
            value = child;
            return true;
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    private static string? CleanDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var document = new HtmlDocument();
        document.LoadHtml(WebUtility.HtmlDecode(value));
        return CleanText(document.DocumentNode.InnerText);
    }

    private static string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var decoded = WebUtility.HtmlDecode(value).Replace("…", "...", StringComparison.Ordinal);
        return WhitespaceRegex().Replace(decoded, " ").Trim();
    }

    [GeneratedRegex(@"[?&]jl=(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ListingIdRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

public sealed record GlassdoorApiParseResult(
    IReadOnlyList<JobVacancy> Vacancies,
    IReadOnlyDictionary<int, string> Cursors,
    int? TotalJobs,
    IReadOnlyList<string> Warnings);
