using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using JobWatcher.Models;
using JobWatcher.Utilities;

namespace JobWatcher.Sources.Glassdoor;

public sealed partial class GlassdoorHtmlParser
{
    private static readonly Uri BaseUri = new("https://www.glassdoor.com");

    public GlassdoorParseResult Parse(string html, string sourceName, DateTimeOffset collectedAtUtc)
    {
        var warnings = new List<string>();
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
        var cards = document.DocumentNode
            .SelectNodes("//a[@data-test='job-title' and contains(@href, '/job-listing/')]")
            ?.ToList() ?? [];

        var skippedCards = 0;
        foreach (var titleLink in cards)
        {
            var vacancy = ParseCard(titleLink, sourceName, collectedAtUtc);
            if (vacancy is not null)
            {
                vacancies.TryAdd(vacancy.ExternalId, vacancy);
                continue;
            }

            skippedCards++;
        }

        if (skippedCards > 0)
        {
            warnings.Add($"Skipped {skippedCards} Glassdoor job cards without title, URL, or listing id.");
        }

        var jsonLdResult = ParseJsonLdItemLists(document, sourceName, collectedAtUtc);
        warnings.AddRange(jsonLdResult.Warnings);

        if (vacancies.Count == 0)
        {
            foreach (var vacancy in jsonLdResult.Vacancies)
            {
                vacancies.TryAdd(vacancy.ExternalId, vacancy);
            }
        }

        return new GlassdoorParseResult(
            vacancies.Values.OrderBy(v => v.ExternalId, StringComparer.OrdinalIgnoreCase).ToList(),
            warnings,
            cards.Count,
            jsonLdResult.JsonLdBlockCount,
            jsonLdResult.ItemListCount,
            ExtractTotalJobs(document));
    }

    public JobVacancy? ParseJobDetailJson(string json, string sourceName, DateTimeOffset collectedAtUtc)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var title = CleanText(GetString(root, "jobTitle")
            ?? GetNestedString(root, "jobListingDetails", "jobTitleText")
            ?? GetNestedString(root, "jobOverview", "jobTitleText"));
        var url = CleanText(GetString(root, "seoJobLink")
            ?? GetNestedString(root, "jobListingDetails", "seoJobLink"));
        var id = GetString(root, "listingId")
            ?? GetNestedString(root, "jobListingDetails", "jobListingId")
            ?? GetNestedString(root, "jobOverview", "listingId")
            ?? ExtractExternalId(url);

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return new JobVacancy
        {
            Source = sourceName,
            ExternalId = id,
            Title = title,
            Company = CleanText(GetString(root, "employerName")
                ?? GetNestedString(root, "jobListingDetails", "employerNameFromSearch")
                ?? GetNestedString(root, "employerOverview", "shortName")),
            Location = CleanText(GetString(root, "locationName")
                ?? GetNestedString(root, "jobListingDetails", "locationName")),
            Url = NormalizeUrl(url),
            Description = CleanDescription(GetString(root, "jobDescription")
                ?? GetNestedString(root, "jobOverview", "description")),
            DatePosted = ParseDiscoverDate(GetNestedString(root, "jobOverview", "discoverDate")),
            CollectedAtUtc = collectedAtUtc
        };
    }

    private static JobVacancy? ParseCard(HtmlNode titleLink, string sourceName, DateTimeOffset collectedAtUtc)
    {
        var href = titleLink.GetAttributeValue("href", string.Empty);
        var title = CleanText(titleLink.InnerText);
        var id = ExtractExternalId(href) ?? ExtractExternalId(titleLink.GetAttributeValue("id", string.Empty));
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var card = FindCard(titleLink);
        return new JobVacancy
        {
            Source = sourceName,
            ExternalId = id,
            Title = title,
            Company = ExtractCompany(card),
            Location = CleanText(card?.SelectSingleNode(".//*[@data-test='emp-location']")?.InnerText),
            Url = NormalizeUrl(href),
            Description = CleanText(card?.SelectSingleNode(".//*[@data-test='descSnippet']")?.InnerText),
            DatePosted = ParseJobAge(CleanText(card?.SelectSingleNode(".//*[@data-test='job-age']")?.InnerText), collectedAtUtc),
            CollectedAtUtc = collectedAtUtc
        };
    }

    /// <summary>
    /// Glassdoor cards expose a relative listing age ("3d", "24h", "30d+") instead of a posting
    /// date, so the date is derived from the collection time. "30d+" means "at least 30 days old",
    /// and the derived date is that lower bound: it is the newest the listing can be, which keeps
    /// <c>MaximumVacancyAgeDays</c> filtering on the safe side rather than optimistic.
    /// </summary>
    private static DateOnly? ParseJobAge(string? rawAge, DateTimeOffset collectedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(rawAge))
        {
            return null;
        }

        var collectedDate = DateOnly.FromDateTime(collectedAtUtc.UtcDateTime.Date);
        if (rawAge.Contains("today", StringComparison.OrdinalIgnoreCase) ||
            rawAge.Contains("just posted", StringComparison.OrdinalIgnoreCase))
        {
            return collectedDate;
        }

        var match = JobAgeRegex().Match(rawAge);
        if (!match.Success || !int.TryParse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        // Hour- and minute-granularity ages are all "posted today" at date resolution.
        var days = match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "d" => value,
            _ => 0
        };

        return collectedDate.AddDays(-days);
    }

    private static JsonLdParseResult ParseJsonLdItemLists(HtmlDocument document, string sourceName, DateTimeOffset collectedAtUtc)
    {
        var warnings = new List<string>();
        var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
        var scripts = document.DocumentNode
            .SelectNodes("//script[translate(@type, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='application/ld+json']")
            ?.Select(script => script.InnerText)
            .ToList() ?? [];
        var itemListCount = 0;

        for (var i = 0; i < scripts.Count; i++)
        {
            var json = WebUtility.HtmlDecode(scripts[i]).Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                using var jsonDocument = JsonDocument.Parse(json);
                foreach (var element in Traverse(jsonDocument.RootElement))
                {
                    if (!HasType(element, "ItemList"))
                    {
                        continue;
                    }

                    itemListCount++;
                    foreach (var vacancy in ExtractItemListVacancies(element, sourceName, collectedAtUtc))
                    {
                        vacancies.TryAdd(vacancy.ExternalId, vacancy);
                    }
                }
            }
            catch (JsonException ex)
            {
                warnings.Add($"Malformed Glassdoor JSON-LD block #{i + 1}: {ex.Message}");
            }
        }

        return new JsonLdParseResult(
            vacancies.Values.OrderBy(v => v.ExternalId, StringComparer.OrdinalIgnoreCase).ToList(),
            warnings,
            scripts.Count,
            itemListCount);
    }

    private static IEnumerable<JobVacancy> ExtractItemListVacancies(JsonElement element, string sourceName, DateTimeOffset collectedAtUtc)
    {
        if (!element.TryGetProperty("itemListElement", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in items.EnumerateArray())
        {
            var name = CleanText(GetString(item, "name"));
            var url = CleanText(GetString(item, "url"));
            var id = ExtractExternalId(url);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            yield return new JobVacancy
            {
                Source = sourceName,
                ExternalId = id,
                Title = name,
                Url = NormalizeUrl(url),
                CollectedAtUtc = collectedAtUtc
            };
        }
    }

    private static string? ExtractCompany(HtmlNode? card)
    {
        if (card is null)
        {
            return null;
        }

        return CleanText(card.SelectSingleNode(".//*[contains(@class, 'EmployerProfile_compactEmployerName')]")?.InnerText
            ?? card.SelectSingleNode(".//*[@data-test='employer-name']")?.InnerText);
    }

    /// <summary>
    /// Finds the element wrapping one whole result card.
    /// </summary>
    /// <remarks>
    /// The class-based lookup alone is not enough: the nearest ancestor whose class contains
    /// "jobCard" is <c>JobCard_jobCardContent</c>, an inner container that holds the title,
    /// location and snippet but not the listing age, which is its sibling. The <c>data-test</c>
    /// wrappers are the real card boundary, so they are preferred and the class match is only a
    /// fallback for markup that lacks them.
    /// </remarks>
    private static HtmlNode? FindCard(HtmlNode titleLink)
    {
        return titleLink.SelectSingleNode("ancestor::*[@data-test='jobListing']")
            ?? titleLink.SelectSingleNode("ancestor::*[@data-test='job-card-wrapper']")
            ?? FindAncestorWithClass(titleLink, "jobCard");
    }

    private static HtmlNode? FindAncestorWithClass(HtmlNode node, string classFragment)
    {
        for (var current = node.ParentNode; current is not null; current = current.ParentNode)
        {
            var classValue = current.GetAttributeValue("class", string.Empty);
            if (classValue.Contains(classFragment, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }
        }

        return null;
    }

    private static int? ExtractTotalJobs(HtmlDocument document)
    {
        var searchTitle = CleanText(document.DocumentNode.SelectSingleNode("//*[@data-test='search-title']")?.InnerText)
            ?? CleanText(document.DocumentNode.SelectSingleNode("//title")?.InnerText);
        if (searchTitle is null)
        {
            return null;
        }

        var match = TotalJobsRegex().Match(searchTitle);
        return match.Success && int.TryParse(match.Groups["count"].Value.Replace(",", string.Empty, StringComparison.Ordinal), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? count
            : null;
    }

    private static IEnumerable<JsonElement> Traverse(JsonElement element)
    {
        yield return element;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                foreach (var child in Traverse(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var childElement in element.EnumerateArray())
            {
                foreach (var child in Traverse(childElement))
                {
                    yield return child;
                }
            }
        }
    }

    private static bool HasType(JsonElement element, string expectedType)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("@type", out var type)
            && GetStringOrArray(type).Any(value => string.Equals(value, expectedType, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static string? GetNestedString(JsonElement element, string firstProperty, string secondProperty)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(firstProperty, out var child)
            && child.ValueKind == JsonValueKind.Object
            ? GetString(child, secondProperty)
            : null;
    }

    private static IReadOnlyList<string> GetStringOrArray(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => [value.GetString() ?? string.Empty],
            JsonValueKind.Array => value.EnumerateArray()
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => v.GetString() ?? string.Empty)
                .ToList(),
            _ => []
        };
    }

    private static string NormalizeUrl(string rawUrl)
    {
        return Uri.TryCreate(WebUtility.HtmlDecode(rawUrl), UriKind.Absolute, out var absolute)
            ? absolute.ToString()
            : new Uri(BaseUri, WebUtility.HtmlDecode(rawUrl)).ToString();
    }

    private static string? ExtractExternalId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = ListingIdRegex().Match(WebUtility.HtmlDecode(value));
        return match.Success ? match.Groups["id"].Value : null;
    }

    private static DateOnly? ParseDiscoverDate(string? raw)
    {
        return DateTimeOffset.TryParse(CleanText(raw), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? DateOnly.FromDateTime(date.UtcDateTime)
            : null;
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

        var decoded = value;
        for (var i = 0; i < 3; i++)
        {
            var next = WebUtility.HtmlDecode(decoded);
            if (string.Equals(next, decoded, StringComparison.Ordinal))
            {
                break;
            }

            decoded = next;
        }

        decoded = decoded.Replace("\u2026", "...", StringComparison.Ordinal);
        return WhitespaceRegex().Replace(decoded, " ").Trim();
    }

    private sealed record JsonLdParseResult(
        IReadOnlyList<JobVacancy> Vacancies,
        IReadOnlyList<string> Warnings,
        int JsonLdBlockCount,
        int ItemListCount);

    [GeneratedRegex(@"(?:[?&]jl=|jobListingId=|job-title-)(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ListingIdRegex();

    [GeneratedRegex(@"^(?<count>[\d,]+)\s+", RegexOptions.CultureInvariant)]
    private static partial Regex TotalJobsRegex();

    [GeneratedRegex(@"(?<value>\d+)\s*(?<unit>[dhm])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JobAgeRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
