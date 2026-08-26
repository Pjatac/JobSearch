using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using JobWatcher.Models;
using JobWatcher.Utilities;

namespace JobWatcher.Sources.DevJobs;

public sealed partial class DevJobsHtmlParser
{
    private static readonly Uri BaseUri = new("https://devjobs.co.il");

    public DevJobsSearchParseResult ParseSearch(string html, string sourceName, DateTimeOffset collectedAtUtc)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var cards = document.DocumentNode.SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' card-grid-2 ') and contains(concat(' ', normalize-space(@class), ' '), ' newDesign ')]")?.ToList() ?? [];
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

        var warnings = skippedCards == 0
            ? Array.Empty<string>()
            : [$"Skipped {skippedCards} DevJobs cards without title or URL."];
        var hasNextPage = document.DocumentNode.SelectSingleNode("//*[@dusk='nextPage' and not(@disabled)]") is not null;
        return new DevJobsSearchParseResult(vacancies.Values.OrderBy(item => item.ExternalId, StringComparer.OrdinalIgnoreCase).ToList(), warnings, cards.Count, hasNextPage);
    }

    public DevJobsLivewireSession ParseLivewireSession(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var token = document.DocumentNode.SelectSingleNode("//meta[translate(@name, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='csrf-token']")
            ?.GetAttributeValue("content", string.Empty)
            .Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("DevJobs search page did not contain a CSRF token.");
        }

        var components = document.DocumentNode.Descendants()
            .Where(node => node.Attributes["wire:snapshot"] is not null)
            .ToList();
        foreach (var component in components)
        {
            var snapshot = WebUtility.HtmlDecode(component.GetAttributeValue("wire:snapshot", string.Empty));
            if (string.IsNullOrWhiteSpace(snapshot))
            {
                continue;
            }

            try
            {
                using var json = JsonDocument.Parse(snapshot);
                if (json.RootElement.TryGetProperty("memo", out var memo) &&
                    memo.TryGetProperty("name", out var name) &&
                    string.Equals(name.GetString(), "find-job", StringComparison.OrdinalIgnoreCase))
                {
                    return new DevJobsLivewireSession(token, snapshot);
                }
            }
            catch (JsonException)
            {
                // Other Livewire components on the page do not affect job search.
            }
        }

        throw new InvalidOperationException("DevJobs search page did not contain the find-job Livewire component.");
    }

    public DevJobsLivewireResponse ParseLivewireResponse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("components", out var components) ||
                components.ValueKind != JsonValueKind.Array ||
                components.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("DevJobs Livewire response did not contain a component.");
            }

            var component = components[0];
            var snapshot = component.TryGetProperty("snapshot", out var snapshotValue) ? snapshotValue.GetString() : null;
            var html = component.TryGetProperty("effects", out var effects) && effects.TryGetProperty("html", out var htmlValue)
                ? htmlValue.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(snapshot) || string.IsNullOrWhiteSpace(html))
            {
                throw new InvalidOperationException("DevJobs Livewire response did not contain updated search HTML and snapshot.");
            }

            return new DevJobsLivewireResponse(snapshot, html);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("DevJobs returned invalid Livewire JSON.", ex);
        }
    }

    public DevJobsDetailParseResult ParseDetail(string html, string sourceName, string url, DateTimeOffset collectedAtUtc)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var title = CleanText(document.DocumentNode.SelectSingleNode("//h3")?.InnerText);
        var description = CleanDescription(document.DocumentNode.SelectSingleNode("//div[contains(concat(' ', normalize-space(@class), ' '), ' content-single ')]")?.InnerHtml);
        var company = CleanText(document.DocumentNode.SelectSingleNode("//div[contains(concat(' ', normalize-space(@class), ' '), ' author-single ')]//span")?.InnerText);
        var valuesByLabel = document.DocumentNode.SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' sidebar-text-info ')]")
            ?.Select(node => new
            {
                Label = CleanText(node.SelectSingleNode(".//span")?.InnerText),
                Value = CleanText(node.SelectSingleNode(".//strong")?.InnerText)
            })
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Label) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Label!, pair => pair.Value!, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var employmentType = GetValue(valuesByLabel, "Job Type");
        var skills = document.DocumentNode.SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' job-overview ')]//ul[contains(concat(' ', normalize-space(@class), ' '), ' courses ')]//li")
            ?.Select(item => CleanText(item.InnerText))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        var descriptionWithSkills = AppendSkills(description, skills);

        if (string.IsNullOrWhiteSpace(title))
        {
            return new DevJobsDetailParseResult(null, ["DevJobs detail page did not contain a job title."]);
        }

        var normalizedUrl = NormalizeUrl(url);
        return new DevJobsDetailParseResult(new JobVacancy
        {
            Source = sourceName,
            ExternalId = ExtractExternalId(normalizedUrl) ?? VacancyIdentity.CreateFingerprint(sourceName, title, normalizedUrl),
            Title = title,
            Company = company,
            Location = GetValue(valuesByLabel, "Location"),
            Url = normalizedUrl,
            Description = descriptionWithSkills,
            DatePosted = ParseDate(GetValue(valuesByLabel, "Updated")),
            EmploymentTypes = string.IsNullOrWhiteSpace(employmentType) ? [] : [employmentType],
            CollectedAtUtc = collectedAtUtc
        }, []);
    }

    private static JobVacancy? ParseCard(HtmlNode card, string sourceName, DateTimeOffset collectedAtUtc)
    {
        var link = card.SelectSingleNode(".//a[contains(concat(' ', normalize-space(@class), ' '), ' name-job ')][@href]");
        var title = CleanText(link?.InnerText);
        var href = CleanText(link?.GetAttributeValue("href", string.Empty));
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var url = NormalizeUrl(href);
        return new JobVacancy
        {
            Source = sourceName,
            ExternalId = ExtractExternalId(url) ?? VacancyIdentity.CreateFingerprint(sourceName, title, url),
            Title = title,
            Company = CleanText(card.SelectSingleNode(".//a[contains(concat(' ', normalize-space(@class), ' '), ' profession ')]")?.InnerText),
            Location = CleanText(card.SelectSingleNode(".//span[contains(concat(' ', normalize-space(@class), ' '), ' location-small ')]")?.InnerText),
            Url = url,
            DatePosted = ParseDate(CleanText(card.SelectSingleNode(".//span[contains(concat(' ', normalize-space(@class), ' '), ' card-time ')]")?.InnerText)),
            CollectedAtUtc = collectedAtUtc
        };
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> values, string label) => values.TryGetValue(label, out var value) ? value : null;

    private static string? AppendSkills(string? description, IReadOnlyList<string> skills)
    {
        if (skills.Count == 0)
        {
            return description;
        }

        var skillsLine = $"Skills: {string.Join(", ", skills)}";
        return string.IsNullOrWhiteSpace(description) ? skillsLine : $"{description}\n\n{skillsLine}";
    }

    private static DateOnly? ParseDate(string? value) => DateOnly.TryParse(CleanText(value), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date) ? date : null;

    private static string NormalizeUrl(string href) => Uri.TryCreate(href, UriKind.Absolute, out var absolute)
        ? absolute.ToString()
        : new Uri(BaseUri, WebUtility.HtmlDecode(href)).ToString();

    private static string? ExtractExternalId(string url)
    {
        return JobIdRegex().Match(url) is { Success: true } match ? match.Groups["id"].Value : null;
    }

    private static string? CleanDescription(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var separated = ListItemStartRegex().Replace(WebUtility.HtmlDecode(html), "\n- ");
        separated = BlockBoundaryRegex().Replace(separated, "\n");
        var document = new HtmlDocument();
        document.LoadHtml(separated);
        var lines = document.DocumentNode.InnerText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(CleanText)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Cast<string>();
        var result = string.Join("\n", lines);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static string? CleanText(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : WhitespaceRegex().Replace(WebUtility.HtmlDecode(value), " ").Trim();

    [GeneratedRegex(@"job-details/(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JobIdRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ListItemStartRegex();

    [GeneratedRegex(@"<br\s*/?>|</(?:p|div|li|ul|ol|tr|td|th|h[1-6]|section|article)\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BlockBoundaryRegex();
}

public sealed record DevJobsSearchParseResult(
    IReadOnlyList<JobVacancy> Vacancies,
    IReadOnlyList<string> Warnings,
    int JobCardCount,
    bool HasNextPage);

public sealed record DevJobsDetailParseResult(JobVacancy? Vacancy, IReadOnlyList<string> Warnings);

public sealed record DevJobsLivewireSession(string CsrfToken, string Snapshot);

public sealed record DevJobsLivewireResponse(string Snapshot, string Html);
