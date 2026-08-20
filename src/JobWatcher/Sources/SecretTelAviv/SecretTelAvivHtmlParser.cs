using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using JobWatcher.Models;
using JobWatcher.Utilities;

namespace JobWatcher.Sources.SecretTelAviv;

public sealed partial class SecretTelAvivHtmlParser
{
    private static readonly Uri BaseUri = new("https://jobs.secrettelaviv.com");

    public SecretTelAvivParseResult Parse(string html, string sourceName, DateTimeOffset collectedAtUtc)
    {
        var warnings = new List<string>();
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var cards = document.DocumentNode
            .SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' wpjb-grid-row ')]")
            ?.ToList() ?? [];
        var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
        var skippedCards = 0;

        foreach (var card in cards)
        {
            var vacancy = ParseCard(card, sourceName, collectedAtUtc);
            if (vacancy is null)
            {
                skippedCards++;
                continue;
            }

            vacancies.TryAdd(vacancy.ExternalId, vacancy);
        }

        if (skippedCards > 0)
        {
            warnings.Add($"Skipped {skippedCards} Secret Tel Aviv job cards without title or URL.");
        }

        return new SecretTelAvivParseResult(
            vacancies.Values.OrderBy(vacancy => vacancy.ExternalId, StringComparer.OrdinalIgnoreCase).ToList(),
            warnings,
            cards.Count);
    }

    public SecretTelAvivDetailParseResult ParseDetail(string html)
    {
        var warnings = new List<string>();
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var scripts = document.DocumentNode
            .SelectNodes("//script[translate(@type, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='application/ld+json']")
            ?.ToList() ?? [];

        for (var i = 0; i < scripts.Count; i++)
        {
            var json = WebUtility.HtmlDecode(scripts[i].InnerText).Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                using var jsonDocument = JsonDocument.Parse(json);
                foreach (var element in Traverse(jsonDocument.RootElement))
                {
                    if (!HasType(element, "JobPosting"))
                    {
                        continue;
                    }

                    return new SecretTelAvivDetailParseResult(ToDetails(element), warnings, scripts.Count);
                }
            }
            catch (JsonException ex)
            {
                warnings.Add($"Malformed JSON-LD block #{i + 1}: {ex.Message}");
            }
        }

        return new SecretTelAvivDetailParseResult(null, warnings, scripts.Count);
    }

    private static JobVacancy? ParseCard(HtmlNode card, string sourceName, DateTimeOffset collectedAtUtc)
    {
        var titleColumn = card.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' wpjb-col-title ')]");
        var link = titleColumn?.SelectSingleNode(".//a[@href]");
        var title = CleanText(link?.InnerText);
        var href = CleanText(link?.GetAttributeValue("href", string.Empty));
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var url = NormalizeUrl(href);
        var externalId = ExtractExternalId(url) ?? VacancyIdentity.CreateFingerprint(sourceName, title, url);
        var locationColumn = card.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' wpjb-col-location ')]");

        return new JobVacancy
        {
            Source = sourceName,
            ExternalId = externalId,
            Title = title,
            Company = CleanText(titleColumn?.SelectSingleNode(".//span[contains(concat(' ', normalize-space(@class), ' '), ' wpjb-sub ')]")?.InnerText),
            Location = CleanText(locationColumn?.SelectSingleNode(".//span[contains(concat(' ', normalize-space(@class), ' '), ' wpjb-line-major ')]")?.InnerText),
            Url = url,
            EmploymentTypes = ExtractEmploymentTypes(locationColumn),
            CollectedAtUtc = collectedAtUtc
        };
    }

    private static IReadOnlyList<string> ExtractEmploymentTypes(HtmlNode? locationColumn)
    {
        var value = CleanText(locationColumn?.SelectSingleNode(".//span[contains(concat(' ', normalize-space(@class), ' '), ' wpjb-sub ')]")?.InnerText);
        return string.IsNullOrWhiteSpace(value) ? [] : [value];
    }

    private static SecretTelAvivJobDetails ToDetails(JsonElement element)
    {
        return new SecretTelAvivJobDetails(
            CleanText(GetString(element, "title")),
            CleanText(GetNestedString(element, "hiringOrganization", "name")),
            ExtractLocation(element),
            CleanDescription(GetString(element, "description")),
            ParseDate(GetString(element, "datePosted")),
            ParseDate(GetString(element, "validThrough")),
            NormalizeEmploymentTypes(GetStringOrArray(element, "employmentType")));
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
            foreach (var child in element.EnumerateArray())
            {
                foreach (var nested in Traverse(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private static bool HasType(JsonElement element, string expectedType)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty("@type", out var type) &&
               GetStringOrArray(type).Any(value => string.Equals(value, expectedType, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static string? GetNestedString(JsonElement element, string parentProperty, string childProperty)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(parentProperty, out var parent) &&
               parent.ValueKind == JsonValueKind.Object
            ? GetString(parent, childProperty)
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
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .ToList(),
            _ => []
        };
    }

    private static string? ExtractLocation(JsonElement element)
    {
        if (!element.TryGetProperty("jobLocation", out var location))
        {
            return null;
        }

        var values = new List<string>();
        foreach (var item in AsArray(location))
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("address", out var address))
            {
                Add(CleanText(GetString(address, "addressLocality")));
                Add(CleanText(GetString(address, "addressRegion")));
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
            foreach (var item in element.EnumerateArray())
            {
                yield return item;
            }

            yield break;
        }

        yield return element;
    }

    private static DateOnly? ParseDate(string? value) => DateOnly.TryParse(CleanText(value), out var date) ? date : null;

    private static IReadOnlyList<string> NormalizeEmploymentTypes(IEnumerable<string> values)
    {
        return values
            .SelectMany(value => value.Trim().Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(value => value.Trim('\"', '\''))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static string NormalizeUrl(string href) => Uri.TryCreate(href, UriKind.Absolute, out var absolute)
        ? absolute.ToString()
        : new Uri(BaseUri, WebUtility.HtmlDecode(href)).ToString();

    private static string? ExtractExternalId(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 && string.Equals(segments[^2], "job", StringComparison.OrdinalIgnoreCase)
            ? segments[^1]
            : null;
    }

    private static string? CleanText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : WhitespaceRegex().Replace(WebUtility.HtmlDecode(value), " ").Trim();
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ListItemStartRegex();

    [GeneratedRegex(@"<br\s*/?>|</(?:p|div|li|ul|ol|tr|td|th|h[1-6]|section|article)\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BlockBoundaryRegex();
}

public sealed record SecretTelAvivParseResult(
    IReadOnlyList<JobVacancy> Vacancies,
    IReadOnlyList<string> Warnings,
    int JobCardCount);

public sealed record SecretTelAvivDetailParseResult(
    SecretTelAvivJobDetails? Details,
    IReadOnlyList<string> Warnings,
    int JsonLdBlockCount);

public sealed record SecretTelAvivJobDetails(
    string? Title,
    string? Company,
    string? Location,
    string? Description,
    DateOnly? DatePosted,
    DateOnly? ValidThrough,
    IReadOnlyList<string> EmploymentTypes);
