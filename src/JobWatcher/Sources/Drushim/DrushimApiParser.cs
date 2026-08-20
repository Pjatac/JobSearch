using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobWatcher.Models;

namespace JobWatcher.Sources.Drushim;

public sealed partial class DrushimApiParser
{
    private static readonly Uri BaseUri = new("https://www.drushim.co.il");

    public DrushimApiParseResult Parse(string json, string sourceName, DateTimeOffset collectedAtUtc)
    {
        var warnings = new List<string>();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var totalPages = GetInt(root, "TotalPagesNumber") ?? 1;
        var nextPage = GetInt(root, "NextPageNumber");
        var totalSearchResultCount = GetInt(root, "TotalSearchResultCount") ?? GetInt(root, "Count");
        var resultList = root.TryGetProperty("ResultList", out var resultListElement) && resultListElement.ValueKind == JsonValueKind.Array
            ? resultListElement
            : default;

        var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
        if (resultList.ValueKind != JsonValueKind.Array)
        {
            warnings.Add("Drushim API response did not contain a ResultList array.");
            return new DrushimApiParseResult([], warnings, 0, totalPages, nextPage, totalSearchResultCount);
        }

        foreach (var item in resultList.EnumerateArray())
        {
            var vacancy = ParseItem(item, sourceName, collectedAtUtc, warnings);
            if (vacancy is not null)
            {
                vacancies.TryAdd(vacancy.ExternalId, vacancy);
            }
        }

        return new DrushimApiParseResult(
            vacancies.Values.OrderBy(v => v.ExternalId, StringComparer.OrdinalIgnoreCase).ToList(),
            warnings,
            resultList.GetArrayLength(),
            totalPages,
            nextPage,
            totalSearchResultCount);
    }

    private static JobVacancy? ParseItem(JsonElement item, string sourceName, DateTimeOffset collectedAtUtc, List<string> warnings)
    {
        var code = GetInt(item, "Code")?.ToString(CultureInfo.InvariantCulture)
            ?? GetNestedInt(item, "JobInfo", "JobCode")?.ToString(CultureInfo.InvariantCulture);
        var title = CleanText(GetNestedString(item, "JobContent", "Name") ?? GetNestedString(item, "JobContent", "FullName"));
        var link = GetNestedString(item, "JobInfo", "Link");

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
        {
            warnings.Add("Skipped Drushim API item without title, URL, or job id.");
            return null;
        }

        return new JobVacancy
        {
            Source = sourceName,
            ExternalId = code,
            Title = title,
            Company = CleanText(GetNestedString(item, "Company", "NameInHebrew") ?? GetNestedString(item, "Company", "CompanyDisplayName")),
            Location = ExtractLocation(item),
            Url = new Uri(BaseUri, link).ToString(),
            Description = ExtractDescription(item),
            DatePosted = ExtractDate(GetNestedString(item, "JobInfo", "Date")),
            EmploymentTypes = ExtractEmploymentTypes(item),
            CollectedAtUtc = collectedAtUtc
        };
    }

    private static string? ExtractLocation(JsonElement item)
    {
        if (!TryGetNested(item, out var addresses, "JobContent", "Addresses") || addresses.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var cities = addresses
            .EnumerateArray()
            .Select(address => CleanText(GetString(address, "City")))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return cities.Count == 0 ? null : string.Join(", ", cities);
    }

    private static string? ExtractDescription(JsonElement item)
    {
        var description = CleanHtml(GetNestedString(item, "JobContent", "Description"));
        var requirements = CleanHtml(GetNestedString(item, "JobContent", "Requirements"));

        return (description, requirements) switch
        {
            (not null, not null) => $"{description}\n\nRequirements: {requirements}",
            (not null, null) => description,
            (null, not null) => requirements,
            _ => null
        };
    }

    private static IReadOnlyList<string> ExtractEmploymentTypes(JsonElement item)
    {
        if (!TryGetNested(item, out var scopes, "JobContent", "Scopes") || scopes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return scopes
            .EnumerateArray()
            .Select(scope => CleanText(GetString(scope, "NameInHebrew")))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DateOnly? ExtractDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)
            ? DateOnly.FromDateTime(date.DateTime)
            : null;
    }

    private static int? GetNestedInt(JsonElement element, string property, string nestedProperty)
    {
        return TryGetNested(element, out var nested, property, nestedProperty) ? GetInt(nested) : null;
    }

    private static string? GetNestedString(JsonElement element, string property, string nestedProperty)
    {
        return TryGetNested(element, out var nested, property, nestedProperty) ? GetString(nested) : null;
    }

    private static bool TryGetNested(JsonElement element, out JsonElement nested, params string[] path)
    {
        nested = element;
        foreach (var property in path)
        {
            if (nested.ValueKind != JsonValueKind.Object || !nested.TryGetProperty(property, out nested))
            {
                return false;
            }
        }

        return true;
    }

    private static int? GetInt(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? GetInt(value)
            : null;
    }

    private static int? GetInt(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(element.GetString(), CultureInfo.InvariantCulture, out var value) => value,
            _ => null
        };
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? GetString(value)
            : null;
    }

    private static string? GetString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.ToString(),
            _ => null
        };
    }

    private static string? CleanHtml(string? value)
    {
        return CleanText(string.IsNullOrWhiteSpace(value) ? null : HtmlTagRegex().Replace(value, " "));
    }

    private static string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return WhitespaceRegex().Replace(WebUtility.HtmlDecode(value), " ").Trim();
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

public sealed record DrushimApiParseResult(
    IReadOnlyList<JobVacancy> Vacancies,
    IReadOnlyList<string> Warnings,
    int ResultItemCount,
    int TotalPages,
    int? NextPage,
    int? TotalSearchResultCount);
