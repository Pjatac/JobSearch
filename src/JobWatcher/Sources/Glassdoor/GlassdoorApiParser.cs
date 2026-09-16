using System.Net;
using System.Globalization;
using System.Text;
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

    public JobVacancy? ParseDetail(string json, JobVacancy listing)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            var description = CleanDetailDescription(GetString(root, "jobDescription"))
                ?? (TryGetObject(root, "jobOverview", out var overview)
                    ? CleanDetailDescription(GetString(overview, "description"))
                    : null);

            if (string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            return listing with
            {
                Description = description,
                Title = CleanText(GetString(root, "jobTitle"))
                    ?? (TryGetObject(root, "jobListingDetails", out var listingDetails) ? CleanText(GetString(listingDetails, "jobTitleText")) : null)
                    ?? listing.Title,
                Company = CleanText(GetString(root, "employerName"))
                    ?? (TryGetObject(root, "jobListingDetails", out var details) &&
                        TryGetObject(details, "employer", out var employer)
                            ? CleanText(GetString(employer, "name"))
                            : null)
                    ?? listing.Company,
                CompanyRating = ExtractCompanyRating(root) ?? listing.CompanyRating,
                Location = CleanText(GetString(root, "locationName")) ?? listing.Location
            };
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
            CompanyRating = ExtractCompanyRating(listing) ?? ExtractCompanyRating(jobview) ?? ExtractCompanyRating(header) ?? ExtractCompanyRating(job),
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

    private static double? ExtractCompanyRating(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetDouble(element, "employerRating")
            ?? GetDouble(element, "overallRating")
            ?? GetDouble(element, "rating")
            ?? GetDouble(element, "ratingValue")
            ?? GetNestedDouble(element, "companyRatings", "overallRating")
            ?? GetNestedDouble(element, "ratings", "overallRating")
            ?? GetNestedDouble(element, "employerOverview", "overallRating")
            ?? GetNestedDouble(element, "employerOverview", "ratings", "overallRating")
            ?? GetNestedDouble(element, "employer", "rating")
            ?? GetNestedDouble(element, "employer", "overallRating");
    }

    private static double? GetNestedDouble(JsonElement element, string first, string second)
    {
        return TryGetObject(element, first, out var child) ? GetDouble(child, second) : null;
    }

    private static double? GetNestedDouble(JsonElement element, string first, string second, string third)
    {
        return TryGetObject(element, first, out var child) ? GetNestedDouble(child, second, third) : null;
    }

    private static double? GetDouble(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var number) => number,
            JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
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

    private static string? CleanDetailDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var document = new HtmlDocument();
        document.LoadHtml(WebUtility.HtmlDecode(value));
        RemoveNodes(document, "//script|//style|//noscript");

        var builder = new StringBuilder();
        AppendFormattedText(document.DocumentNode, builder);

        var withoutCss = RemoveCssNoise(builder.ToString());
        var lines = withoutCss
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => WhitespaceRegex().Replace(line, " ").Trim())
            .ToList();

        var compact = new List<string>();
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                if (compact.Count > 0 && compact[^1].Length > 0)
                {
                    compact.Add(string.Empty);
                }

                continue;
            }

            compact.Add(line);
        }

        var formatted = string.Join('\n', compact).Trim();
        return formatted.Length == 0 ? null : formatted;
    }

    private static void RemoveNodes(HtmlDocument document, string xpath)
    {
        var nodes = document.DocumentNode.SelectNodes(xpath);
        if (nodes is null)
        {
            return;
        }

        foreach (var node in nodes)
        {
            node.Remove();
        }
    }

    private static void AppendFormattedText(HtmlNode node, StringBuilder builder)
    {
        switch (node.NodeType)
        {
            case HtmlNodeType.Text:
                AppendInlineText(builder, ((HtmlTextNode)node).Text);
                return;
            case HtmlNodeType.Comment:
                return;
        }

        var name = node.Name.ToLowerInvariant();
        switch (name)
        {
            case "br":
                EnsureLineBreak(builder);
                return;
            case "li":
                EnsureLineBreak(builder);
                builder.Append("- ");
                AppendChildren(node, builder);
                EnsureLineBreak(builder);
                return;
            case "p":
            case "div":
            case "section":
            case "article":
                EnsureParagraphBreak(builder);
                AppendChildren(node, builder);
                EnsureParagraphBreak(builder);
                return;
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                EnsureParagraphBreak(builder);
                AppendChildren(node, builder);
                EnsureParagraphBreak(builder);
                return;
            case "ul":
            case "ol":
                EnsureParagraphBreak(builder);
                AppendChildren(node, builder);
                EnsureParagraphBreak(builder);
                return;
            default:
                AppendChildren(node, builder);
                return;
        }
    }

    private static void AppendChildren(HtmlNode node, StringBuilder builder)
    {
        foreach (var child in node.ChildNodes)
        {
            AppendFormattedText(child, builder);
        }
    }

    private static void AppendInlineText(StringBuilder builder, string value)
    {
        var text = WebUtility.HtmlDecode(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        text = WhitespaceRegex().Replace(text, " ");
        if (builder.Length > 0 &&
            !char.IsWhiteSpace(builder[^1]) &&
            !IsLeadingPunctuation(text[0]))
        {
            builder.Append(' ');
        }

        builder.Append(text);
    }

    private static void EnsureLineBreak(StringBuilder builder)
    {
        if (builder.Length == 0 || builder[^1] == '\n')
        {
            return;
        }

        builder.Append('\n');
    }

    private static void EnsureParagraphBreak(StringBuilder builder)
    {
        if (builder.Length == 0)
        {
            return;
        }

        if (builder[^1] != '\n')
        {
            builder.Append('\n');
        }

        if (builder.Length < 2 || builder[^2] != '\n')
        {
            builder.Append('\n');
        }
    }

    private static bool IsLeadingPunctuation(char value)
    {
        return value is '.' or ',' or ':' or ';' or ')' or ']' or '}';
    }

    private static string RemoveCssNoise(string value)
    {
        var withoutRules = CssRuleRegex().Replace(value, " ");
        var withoutProperties = CssPropertyRunRegex().Replace(withoutRules, " ");
        return EmptyCssRuleRegex().Replace(withoutProperties, " ");
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

    [GeneratedRegex(@"(?s)(?:^|\s)(?:@[\w-]+\s+[^{]+|\.[A-Za-z_-][\w-]*(?:\s*[:.#>][^{]*)?)\{.*?\}", RegexOptions.CultureInvariant)]
    private static partial Regex CssRuleRegex();

    [GeneratedRegex(@"(?s)(?:-[A-Za-z][\w-]*|[A-Za-z][\w-]*)\s*:\s*[^;{}]{1,160};(?:\s*(?:-[A-Za-z][\w-]*|[A-Za-z][\w-]*)\s*:\s*[^;{}]{1,160};){4,}", RegexOptions.CultureInvariant)]
    private static partial Regex CssPropertyRunRegex();

    [GeneratedRegex(@"\.[A-Za-z0-9_-]+\s*\{\s*\}", RegexOptions.CultureInvariant)]
    private static partial Regex EmptyCssRuleRegex();
}

public sealed record GlassdoorApiParseResult(
    IReadOnlyList<JobVacancy> Vacancies,
    IReadOnlyDictionary<int, string> Cursors,
    int? TotalJobs,
    IReadOnlyList<string> Warnings);
