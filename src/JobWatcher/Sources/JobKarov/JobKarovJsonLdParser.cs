using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using JobWatcher.Models;
using JobWatcher.Utilities;

namespace JobWatcher.Sources.JobKarov;

public sealed partial class JobKarovJsonLdParser
{
    private static readonly Uri BaseUri = new("https://www.jobkarov.com");

    public JobKarovParseResult Parse(string html, string sourceName, DateTimeOffset collectedAtUtc)
    {
        var warnings = new List<string>();
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var scripts = document.DocumentNode
            .SelectNodes("//script[translate(@type, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='application/ld+json']")
            ?.ToList() ?? [];

        var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
        var jobPostingObjectCount = 0;

        for (var i = 0; i < scripts.Count; i++)
        {
            var json = WebUtility.HtmlDecode(scripts[i].InnerText)?.Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var element in Traverse(doc.RootElement))
                {
                    if (!HasType(element, "JobPosting"))
                    {
                        continue;
                    }

                    jobPostingObjectCount++;
                    var vacancy = ToVacancy(element, sourceName, collectedAtUtc, warnings);
                    if (vacancy is not null)
                    {
                        vacancies.TryAdd($"{vacancy.Source}:{vacancy.ExternalId}", vacancy);
                    }
                }
            }
            catch (JsonException ex)
            {
                warnings.Add($"Malformed JSON-LD block #{i + 1}: {ex.Message}");
            }
        }

        var enrichedCount = MergeRequirements(html, vacancies, warnings);

        return new JobKarovParseResult(
            vacancies.Values.OrderBy(v => v.ExternalId, StringComparer.OrdinalIgnoreCase).ToList(),
            warnings,
            scripts.Count,
            jobPostingObjectCount,
            enrichedCount);
    }

    /// <summary>
    /// Appends the requirements text that JSON-LD leaves out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// JobKarov splits a posting into a <c>description</c> and a separate <c>require</c> field, and
    /// publishes only the former as JSON-LD. The requirements hold the experience level and the
    /// technology stack, which is exactly what title and description filtering needs, so dropping
    /// them loses the most useful half of the posting.
    /// </para>
    /// <para>
    /// Both fields are read from the <c>window.__BASE_SITES__</c> array already present in the same
    /// search response, so this costs no extra requests. Fetching each vacancy's own page was
    /// measured and returns byte-identical field lengths: JobKarov caps each field at 999
    /// characters everywhere, so per-vacancy requests would buy nothing.
    /// </para>
    /// </remarks>
    private static int MergeRequirements(string html, Dictionary<string, JobVacancy> vacancies, List<string> warnings)
    {
        var match = BaseSitesRegex().Match(html);
        if (!match.Success)
        {
            return 0;
        }

        var enriched = 0;
        try
        {
            using var document = JsonDocument.Parse(match.Groups["json"].Value);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return 0;
            }

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                var id = GetString(entry, "id");
                var requirements = CleanDescription(GetString(entry, "require"));
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(requirements))
                {
                    continue;
                }

                foreach (var key in vacancies.Keys)
                {
                    var vacancy = vacancies[key];
                    if (!string.Equals(vacancy.ExternalId, id, StringComparison.Ordinal) ||
                        vacancy.Description?.Contains(requirements, StringComparison.Ordinal) == true)
                    {
                        continue;
                    }

                    vacancies[key] = vacancy with
                    {
                        Description = string.IsNullOrWhiteSpace(vacancy.Description)
                            ? requirements
                            : $"{vacancy.Description}\n\nRequirements: {requirements}"
                    };

                    enriched++;
                    break;
                }
            }
        }
        catch (JsonException ex)
        {
            warnings.Add($"Malformed __BASE_SITES__ block: {ex.Message}");
        }

        return enriched;
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

    private static JobVacancy? ToVacancy(JsonElement element, string sourceName, DateTimeOffset collectedAtUtc, List<string> warnings)
    {
        var title = CleanText(GetString(element, "title"));
        var rawUrl = CleanText(GetString(element, "url"));
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(rawUrl))
        {
            warnings.Add("Skipped JobPosting without title or URL.");
            return null;
        }

        var absoluteUrl = NormalizeUrl(rawUrl);
        var externalId = ExtractExternalId(absoluteUrl);
        if (externalId is null)
        {
            externalId = VacancyIdentity.CreateFingerprint(sourceName, title, absoluteUrl);
            warnings.Add($"Used fingerprint identity for JobPosting without numeric URL id: {absoluteUrl}");
        }

        return new JobVacancy
        {
            Source = sourceName,
            ExternalId = externalId,
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
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("@type", out var type))
        {
            return false;
        }

        return GetStringOrArray(type).Any(value => string.Equals(value, expectedType, StringComparison.OrdinalIgnoreCase));
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

        return values.Count == 0 ? null : string.Join(", ", values.Distinct());

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
        var match = JobKarovSiteUrlRegex().Match(url);
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

        // Stripping tags by concatenating inner text glues neighbouring blocks together, turning
        // "<b>C#</b><br/><b>Kubernetes</b>" into "C#Kubernetes". Keyword filtering splits on
        // whitespace, so a glued token is a token it can no longer match.
        var separated = BlockBoundaryRegex().Replace(WebUtility.HtmlDecode(value), " ");

        var doc = new HtmlDocument();
        doc.LoadHtml(separated);
        return CleanText(doc.DocumentNode.InnerText);
    }

    [GeneratedRegex(@"/Search/Site/(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JobKarovSiteUrlRegex();

    [GeneratedRegex(@"window\.__BASE_SITES__\s*=\s*(?<json>\[.*?\])\s*;", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex BaseSitesRegex();

    [GeneratedRegex(@"<br\s*/?>|</(?:p|div|li|ul|ol|tr|td|th|h[1-6]|section|article)\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BlockBoundaryRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
