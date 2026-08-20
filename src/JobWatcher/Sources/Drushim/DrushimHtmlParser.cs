using System.Globalization;
using System.Net;
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

        var cards = document.DocumentNode
            .SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' job-item-main ')]")
            ?.ToList() ?? [];

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

    private static JobVacancy? ParseCard(HtmlNode card, string sourceName, DateTimeOffset collectedAtUtc, List<string> warnings)
    {
        var title = CleanText(card.SelectSingleNode(".//span[contains(concat(' ', normalize-space(@class), ' '), ' job-url ')]")?.InnerText);
        var link = card.SelectSingleNode(".//a[starts-with(@href, '/job/')]")?.GetAttributeValue("href", string.Empty);
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
            Company = CleanText(card.SelectSingleNode(".//span[contains(concat(' ', normalize-space(@class), ' '), ' bidi ')]")?.InnerText),
            Location = ExtractLocation(card),
            Url = new Uri(BaseUri, link).ToString(),
            Description = CleanText(card.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' vacancyMain ')]//p")?.InnerText),
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
