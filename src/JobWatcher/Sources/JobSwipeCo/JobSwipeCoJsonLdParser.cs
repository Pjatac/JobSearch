using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using JobWatcher.Models;
using JobWatcher.Utilities;

namespace JobWatcher.Sources.JobSwipeCo;

public sealed partial class JobSwipeCoJsonLdParser
{
    private static readonly Uri BaseUri = new("https://jobswipe.co");

    public JobSwipeCoSearchParseResult ParseSearch(string html)
    {
        var warnings = new List<string>();
        var scripts = GetJsonLdScripts(html);
        var urls = new List<string>();
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
                using var document = JsonDocument.Parse(json);
                foreach (var element in Traverse(document.RootElement))
                {
                    if (!HasType(element, "ItemList"))
                    {
                        continue;
                    }

                    itemListCount++;
                    urls.AddRange(ExtractItemListUrls(element));
                }
            }
            catch (JsonException ex)
            {
                warnings.Add($"Malformed JSON-LD block #{i + 1}: {ex.Message}");
            }
        }

        return new JobSwipeCoSearchParseResult(
            urls.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            warnings,
            scripts.Count,
            itemListCount);
    }

    public JobSwipeCoJobParseResult ParseJob(string html, string sourceName, DateTimeOffset collectedAtUtc)
    {
        var warnings = new List<string>();
        var scripts = GetJsonLdScripts(html);
        var jobPostingCount = 0;

        for (var i = 0; i < scripts.Count; i++)
        {
            var json = WebUtility.HtmlDecode(scripts[i]).Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                foreach (var element in Traverse(document.RootElement))
                {
                    if (!HasType(element, "JobPosting"))
                    {
                        continue;
                    }

                    jobPostingCount++;
                    var vacancy = ToVacancy(element, sourceName, collectedAtUtc, warnings);
                    if (vacancy is not null)
                    {
                        return new JobSwipeCoJobParseResult(vacancy, warnings, scripts.Count, jobPostingCount);
                    }
                }
            }
            catch (JsonException ex)
            {
                warnings.Add($"Malformed JSON-LD block #{i + 1}: {ex.Message}");
            }
        }

        return new JobSwipeCoJobParseResult(null, warnings, scripts.Count, jobPostingCount);
    }

    private static IReadOnlyList<string> GetJsonLdScripts(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        return document.DocumentNode
            .SelectNodes("//script[translate(@type, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='application/ld+json']")
            ?.Select(script => script.InnerText)
            .ToList() ?? [];
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

    private static IEnumerable<string> ExtractItemListUrls(JsonElement element)
    {
        if (!element.TryGetProperty("itemListElement", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in items.EnumerateArray())
        {
            var url = CleanText(GetString(item, "url"));
            if (!string.IsNullOrWhiteSpace(url))
            {
                yield return NormalizeUrl(url);
            }
        }
    }

    private static JobVacancy? ToVacancy(JsonElement element, string sourceName, DateTimeOffset collectedAtUtc, List<string> warnings)
    {
        var title = CleanText(GetString(element, "title"));
        var rawUrl = CleanText(GetString(element, "url"));
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(rawUrl))
        {
            warnings.Add("Skipped JobSwipe.co JobPosting without title or URL.");
            return null;
        }

        var absoluteUrl = NormalizeUrl(rawUrl);
        return new JobVacancy
        {
            Source = sourceName,
            ExternalId = ExtractExternalId(absoluteUrl) ?? VacancyIdentity.CreateFingerprint(sourceName, title, absoluteUrl),
            Title = title,
            Company = CleanText(GetNestedString(element, "hiringOrganization", "name")),
            Location = ExtractLocation(element),
            Url = absoluteUrl,
            Description = CleanDescription(GetString(element, "description")),
            DatePosted = ParseDate(GetString(element, "datePosted")),
            ValidThrough = ParseDate(GetString(element, "validThrough")),
            EmploymentTypes = NormalizeEmploymentTypes(GetStringOrArray(element, "employmentType")),
            CollectedAtUtc = collectedAtUtc
        };
    }

    private static bool HasType(JsonElement element, string expectedType)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("@type", out var type)
            && GetStringOrArray(type).Any(value => string.Equals(value, expectedType, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractLocation(JsonElement element)
    {
        if (!element.TryGetProperty("jobLocation", out var location))
        {
            return null;
        }

        var values = new List<string>();
        foreach (var loc in AsArray(location))
        {
            Add(CleanText(GetString(loc, "name")));
            if (loc.ValueKind == JsonValueKind.Object && loc.TryGetProperty("address", out var address))
            {
                Add(CleanText(GetString(address, "addressLocality")));
                Add(CleanText(GetString(address, "addressRegion")));
                Add(CleanText(GetString(address, "streetAddress")));
            }
        }

        return values.Count == 0 ? null : string.Join(", ", values.Distinct(StringComparer.OrdinalIgnoreCase));

        void Add(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }
    }

    private static IEnumerable<JsonElement> AsArray(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                yield return child;
            }
        }
        else
        {
            yield return element;
        }
    }

    private static string NormalizeUrl(string rawUrl)
    {
        return Uri.TryCreate(rawUrl, UriKind.Absolute, out var absolute)
            ? absolute.ToString()
            : new Uri(BaseUri, rawUrl).ToString();
    }

    private static string? ExtractExternalId(string url)
    {
        var match = JobSwipeCoJobIdRegex().Match(WebUtility.UrlDecode(url));
        return match.Success ? match.Groups["id"].Value : null;
    }

    private static DateOnly? ParseDate(string? raw)
    {
        return DateOnly.TryParse(CleanText(raw), out var date) ? date : null;
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

    private static IReadOnlyList<string> GetStringOrArray(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value)
            ? GetStringOrArray(value)
            : [];
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

    private static IReadOnlyList<string> NormalizeEmploymentTypes(IEnumerable<string> values)
    {
        return values
            .SelectMany(value => value.Trim().Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(value => value.Trim('"', '\''))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return WhitespaceRegex().Replace(WebUtility.HtmlDecode(value), " ").Trim();
    }

    private static string? CleanDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var separated = ListItemStartRegex().Replace(WebUtility.HtmlDecode(value), "\n- ");
        separated = BlockBoundaryRegex().Replace(separated, "\n");
        var document = new HtmlDocument();
        document.LoadHtml(separated);
        var lines = document.DocumentNode.InnerText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(CleanText)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Cast<string>()
            .ToList();

        return lines.Count == 0 ? null : string.Join("\n", lines);
    }

    [GeneratedRegex(@"-(?<id>6a[0-9a-f]{22,})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JobSwipeCoJobIdRegex();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ListItemStartRegex();

    [GeneratedRegex(@"<br\s*/?>|</(?:p|div|li|ul|ol|tr|td|th|h[1-6]|section|article)\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BlockBoundaryRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
