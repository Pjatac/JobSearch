using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using JobWatcher.Models;

namespace JobWatcher.Sources.Drushim;

public sealed partial class DrushimHtmlParser
{
    private static readonly Uri BaseUri = new("https://www.drushim.co.il");

    public DrushimParseResult Parse(string html, string sourceName, DateTimeOffset collectedAtUtc)
    {
        var warnings = new List<string>();
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var nextDataVacancies = ParseNextDataVacancies(document, sourceName, collectedAtUtc, warnings);
        if (nextDataVacancies.Count > 0)
        {
            return new DrushimParseResult(
                nextDataVacancies.Values.OrderBy(v => v.ExternalId, StringComparer.OrdinalIgnoreCase).ToList(),
                warnings,
                nextDataVacancies.Count);
        }

        var cards = document.DocumentNode
            .SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' job-item-main ')]")
            ?.ToList() ?? [];
        if (cards.Count == 0)
        {
            cards = document.DocumentNode
                .SelectNodes("//article[@data-nagish='job-card-item']")
                ?.ToList() ?? [];
        }

        var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in cards)
        {
            var vacancy = ParseCard(card, sourceName, collectedAtUtc, warnings);
            if (vacancy is not null)
            {
                vacancies.TryAdd(vacancy.ExternalId, vacancy);
            }
        }

        return new DrushimParseResult(
            vacancies.Values.OrderBy(v => v.ExternalId, StringComparer.OrdinalIgnoreCase).ToList(),
            warnings,
            cards.Count);
    }

    public JobVacancy? ParseDetail(string html, string sourceName, string url, DateTimeOffset collectedAtUtc)
    {
        var warnings = new List<string>();
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var id = ExtractExternalId(new Uri(url).AbsolutePath);
        var nextDataVacancies = ParseNextDataVacancies(document, sourceName, collectedAtUtc, warnings);
        if (id is not null && nextDataVacancies.TryGetValue(id, out var vacancy))
        {
            return vacancy;
        }

        return nextDataVacancies.Values.FirstOrDefault()
            ?? ParseVisibleDetail(document, sourceName, url, collectedAtUtc);
    }

    private static JobVacancy? ParseVisibleDetail(HtmlDocument document, string sourceName, string url, DateTimeOffset collectedAtUtc)
    {
        var id = ExtractExternalId(new Uri(url).AbsolutePath);
        var title = CleanText(document.DocumentNode.SelectSingleNode("//h1")?.InnerText);
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return new JobVacancy
        {
            Source = sourceName,
            ExternalId = id,
            Title = title,
            Company = ExtractVisibleCompany(document),
            Location = null,
            Url = url,
            Description = ExtractVisibleDetailDescription(document),
            DatePosted = null,
            EmploymentTypes = ExtractEmploymentTypes(document.DocumentNode),
            CollectedAtUtc = collectedAtUtc
        };
    }

    private static Dictionary<string, JobVacancy> ParseNextDataVacancies(
        HtmlDocument document,
        string sourceName,
        DateTimeOffset collectedAtUtc,
        List<string> warnings)
    {
        var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
        var script = document.DocumentNode.SelectSingleNode("//script[@id='__NEXT_DATA__']");
        if (script is null || string.IsNullOrWhiteSpace(script.InnerText))
        {
            return vacancies;
        }

        try
        {
            using var json = JsonDocument.Parse(WebUtility.HtmlDecode(script.InnerText));
            AddNextDataVacancies(json.RootElement, sourceName, collectedAtUtc, vacancies);
        }
        catch (JsonException ex)
        {
            warnings.Add($"Skipped Drushim Next.js data: {ex.Message}");
        }

        return vacancies;
    }

    private static void AddNextDataVacancies(
        JsonElement element,
        string sourceName,
        DateTimeOffset collectedAtUtc,
        Dictionary<string, JobVacancy> vacancies)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (TryParseNextDataVacancy(element, sourceName, collectedAtUtc, out var vacancy))
            {
                vacancies.TryAdd(vacancy.ExternalId, vacancy);
            }

            foreach (var property in element.EnumerateObject())
            {
                AddNextDataVacancies(property.Value, sourceName, collectedAtUtc, vacancies);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AddNextDataVacancies(item, sourceName, collectedAtUtc, vacancies);
            }
        }
    }

    private static bool TryParseNextDataVacancy(
        JsonElement item,
        string sourceName,
        DateTimeOffset collectedAtUtc,
        out JobVacancy vacancy)
    {
        vacancy = null!;
        var id = CleanText(GetString(item, "id"));
        var title = CleanText(GetString(item, "title"));
        var link = CleanText(GetString(item, "jobUrl"));
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
        {
            return false;
        }

        vacancy = new JobVacancy
        {
            Source = sourceName,
            ExternalId = id,
            Title = title,
            Company = CleanText(GetString(item, "companyName")),
            Location = ExtractNextDataLocation(item),
            Url = new Uri(BaseUri, link).ToString(),
            Description = CleanText(GetString(item, "description")),
            DatePosted = ExtractNextDataDate(GetString(item, "publishedAtIso")),
            EmploymentTypes = ExtractNextDataEmploymentTypes(item),
            CollectedAtUtc = collectedAtUtc
        };
        return true;
    }

    private static JobVacancy? ParseCard(HtmlNode card, string sourceName, DateTimeOffset collectedAtUtc, List<string> warnings)
    {
        var title = CleanText(
            card.SelectSingleNode(".//span[contains(concat(' ', normalize-space(@class), ' '), ' job-url ')]")?.InnerText
            ?? card.SelectSingleNode(".//*[@data-nagish='job-card-title']")?.InnerText);
        var link = card.SelectSingleNode(".//a[@data-nagish='job-card-details-link' and starts-with(@href, '/job/')]")?.GetAttributeValue("href", string.Empty)
            ?? card.SelectSingleNode(".//a[starts-with(@href, '/job/')]")?.GetAttributeValue("href", string.Empty);
        var id = ExtractExternalId(link);

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(id))
        {
            warnings.Add("Skipped Drushim job card without title, URL, or job id.");
            return null;
        }

        return new JobVacancy
        {
            Source = sourceName,
            ExternalId = id,
            Title = title,
            Company = CleanText(
                card.SelectSingleNode(".//span[contains(concat(' ', normalize-space(@class), ' '), ' bidi ')]")?.InnerText
                ?? card.SelectSingleNode(".//span[contains(@class, '__companyName')]")?.InnerText),
            Location = ExtractLocation(card),
            Url = new Uri(BaseUri, link).ToString(),
            Description = CleanText(
                card.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' vacancyMain ')]//p")?.InnerText
                ?? card.SelectSingleNode(".//p[contains(@class, '__description')]")?.InnerText),
            DatePosted = ExtractDatePosted(card),
            EmploymentTypes = ExtractEmploymentTypes(card),
            CollectedAtUtc = collectedAtUtc
        };
    }

    private static string? ExtractLocation(HtmlNode card)
    {
        var details = card.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' job-details-sub ')]");
        var values = details?.SelectNodes(".//span[contains(concat(' ', normalize-space(@class), ' '), ' display-18 ')]")
            ?.Select(node => CleanText(node.InnerText))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToList() ?? [];

        if (values.Count == 0)
        {
            values = card.SelectNodes(".//div[contains(@class, 'job-card-meta-module') and contains(@class, '__row')][1]//span[contains(@class, '__text')]")
                ?.Select(node => CleanText(node.InnerText))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToList() ?? [];
        }

        return values
            .Select(value => value.Trim().Trim('|').Trim())
            .FirstOrDefault(value =>
            !value.Contains("שנים", StringComparison.OrdinalIgnoreCase) &&
            !value.Contains("משרה", StringComparison.OrdinalIgnoreCase) &&
            !value.Contains("לפני", StringComparison.OrdinalIgnoreCase) &&
            !DateOnly.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) &&
            !value.Contains("מספר מקומות", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(value, "|", StringComparison.OrdinalIgnoreCase));
    }

    private static DateOnly? ExtractDatePosted(HtmlNode card)
    {
        var text = CleanText(card.InnerText);
        if (text is null)
        {
            return null;
        }

        var match = DateRegex().Match(text);
        return match.Success &&
            DateOnly.TryParseExact(match.Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static IReadOnlyList<string> ExtractEmploymentTypes(HtmlNode card)
    {
        var text = CleanText(card.InnerText);
        if (text is null)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var value in new[] { "משרה מלאה", "משרה חלקית", "משרה זמנית", "עבודה מהבית", "היברידי" })
        {
            if (text.Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static string? ExtractVisibleCompany(HtmlDocument document)
    {
        return CleanText(document.DocumentNode.SelectSingleNode("//h1/following::a[normalize-space()][1]")?.InnerText);
    }

    private static string? ExtractVisibleDetailDescription(HtmlDocument document)
    {
        var lines = ExtractVisibleLines(document.DocumentNode)
            .Select(CleanText)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Cast<string>()
            .ToList();
        var descriptionStart = FindLine(lines, "תיאור משרה");
        var requirementsStart = FindLine(lines, "דרישות התפקיד");
        if (descriptionStart < 0 && requirementsStart < 0)
        {
            return null;
        }

        var selected = new List<string>();
        if (descriptionStart >= 0)
        {
            var end = requirementsStart > descriptionStart ? requirementsStart : FindStopLine(lines, descriptionStart + 1);
            selected.AddRange(lines.Skip(descriptionStart + 1).Take(end - descriptionStart - 1));
        }

        if (requirementsStart >= 0)
        {
            var end = FindStopLine(lines, requirementsStart + 1);
            selected.Add("דרישות התפקיד");
            selected.AddRange(lines.Skip(requirementsStart + 1).Take(end - requirementsStart - 1));
        }

        var result = selected
            .Where(line => !IsDetailStopLine(line))
            .ToList();
        return result.Count == 0 ? null : string.Join("\n", result);
    }

    private static IEnumerable<string> ExtractVisibleLines(HtmlNode node)
    {
        var lines = new List<string>();
        AddVisibleLines(node, lines);
        return lines;
    }

    private static void AddVisibleLines(HtmlNode node, List<string> lines)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = CleanText(node.InnerText);
            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(text);
            }

            return;
        }

        if (node.NodeType != HtmlNodeType.Element && node.NodeType != HtmlNodeType.Document)
        {
            return;
        }

        if (node.Name is "script" or "style" or "svg" or "noscript")
        {
            return;
        }

        foreach (var child in node.ChildNodes)
        {
            AddVisibleLines(child, lines);
        }
    }

    private static int FindLine(IReadOnlyList<string> lines, string value)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (string.Equals(lines[index], value, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindStopLine(IReadOnlyList<string> lines, int start)
    {
        for (var index = start; index < lines.Count; index++)
        {
            if (IsDetailStopLine(lines[index]))
            {
                return index;
            }
        }

        return lines.Count;
    }

    private static bool IsDetailStopLine(string line)
    {
        return line.Contains("הדרך החכמה", StringComparison.OrdinalIgnoreCase)
            || line.Contains("אתר זה מוגן", StringComparison.OrdinalIgnoreCase)
            || line.Contains("משרות נוספות", StringComparison.OrdinalIgnoreCase)
            || line.Contains("פרסום משרה", StringComparison.OrdinalIgnoreCase)
            || line.Contains("מדיניות פרטיות", StringComparison.OrdinalIgnoreCase)
            || line.Contains("תקנון האתר", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractNextDataLocation(JsonElement item)
    {
        if (item.TryGetProperty("cities", out var cities) && cities.ValueKind == JsonValueKind.Array)
        {
            var values = cities
                .EnumerateArray()
                .Select(GetString)
                .Select(CleanText)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (values.Count > 0)
            {
                return string.Join(", ", values);
            }
        }

        return CleanText(GetString(item, "city"));
    }

    private static IReadOnlyList<string> ExtractNextDataEmploymentTypes(JsonElement item)
    {
        if (item.TryGetProperty("scopes", out var scopes) && scopes.ValueKind == JsonValueKind.Array)
        {
            return scopes
                .EnumerateArray()
                .Select(GetString)
                .Select(CleanText)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var scope = CleanText(GetString(item, "scope"));
        return string.IsNullOrWhiteSpace(scope) ? [] : [scope];
    }

    private static DateOnly? ExtractNextDataDate(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)
            ? DateOnly.FromDateTime(date.DateTime)
            : null;
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

    private static string? ExtractExternalId(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var match = JobUrlRegex().Match(href);
        return match.Success ? match.Groups["id"].Value : null;
    }

    private static string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return WhitespaceRegex().Replace(WebUtility.HtmlDecode(value), " ").Trim();
    }

    [GeneratedRegex(@"/job/(?<id>\d+)/", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JobUrlRegex();

    [GeneratedRegex(@"\b\d{2}/\d{2}/\d{4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
