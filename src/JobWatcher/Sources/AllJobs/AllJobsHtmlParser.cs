using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using JobWatcher.Models;

namespace JobWatcher.Sources.AllJobs;

public sealed partial class AllJobsHtmlParser
{
    private static readonly Uri BaseUri = new("https://www.alljobs.co.il");

    public AllJobsParseResult Parse(string html, string sourceName, DateTimeOffset collectedAtUtc)
    {
        var warnings = new List<string>();
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var cards = document.DocumentNode
            .SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' job-content-top ')]")
            ?.ToList() ?? [];

        var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
        var skippedCards = 0;
        foreach (var card in cards)
        {
            var vacancy = ParseCard(card, sourceName, collectedAtUtc);
            if (vacancy is not null)
            {
                vacancies.TryAdd(vacancy.ExternalId, vacancy);
                continue;
            }

            skippedCards++;
        }

        if (skippedCards > 0)
        {
            warnings.Add($"Skipped {skippedCards} AllJobs job cards without title, URL, or job id.");
        }

        return new AllJobsParseResult(
            vacancies.Values.OrderBy(v => v.ExternalId, StringComparer.OrdinalIgnoreCase).ToList(),
            warnings,
            cards.Count,
            GetHiddenInt(document, "hdnTotalPages"),
            GetHiddenInt(document, "hdnJobsCount"));
    }

    private static JobVacancy? ParseCard(HtmlNode card, string sourceName, DateTimeOffset collectedAtUtc)
    {
        var linkNode = card.SelectSingleNode(".//div[contains(@class, 'job-content-top-title')]//a[contains(@href, 'JobID=')]");
        var href = linkNode?.GetAttributeValue("href", string.Empty);
        var id = ExtractExternalId(href);
        var title = CleanText(linkNode?.SelectSingleNode(".//h2")?.InnerText ?? linkNode?.GetAttributeValue("title", string.Empty));

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        return new JobVacancy
        {
            Source = sourceName,
            ExternalId = id,
            Title = title,
            Company = ExtractCompany(card),
            Location = ExtractLocation(card),
            Url = new Uri(BaseUri, WebUtility.HtmlDecode(href)).ToString(),
            Description = ExtractDescription(card),
            DatePosted = null,
            EmploymentTypes = ExtractEmploymentTypes(card),
            CollectedAtUtc = collectedAtUtc
        };
    }

    private static string? ExtractCompany(HtmlNode card)
    {
        return CleanText(card.SelectSingleNode(".//div[contains(@class, 'job-content-top-title')]//div[contains(concat(' ', normalize-space(@class), ' '), ' T14 ')]//a")?.InnerText);
    }

    private static string? ExtractLocation(HtmlNode card)
    {
        var locationNode = card.SelectSingleNode(".//div[contains(@class, 'job-content-top-location')]");
        var text = CleanText(locationNode?.InnerText);
        if (text is null)
        {
            return null;
        }

        text = text.Replace("מיקום המשרה:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        text = text.Replace("Location:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        text = AverageTimeRegex().Replace(text, string.Empty);
        return CleanText(text);
    }

    private static string? ExtractDescription(HtmlNode card)
    {
        return CleanText(card.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' job-content-top-desc ')]")?.InnerText);
    }

    private static IReadOnlyList<string> ExtractEmploymentTypes(HtmlNode card)
    {
        var typeNode = card.SelectSingleNode(".//div[contains(@class, 'job-content-top-type')]");
        return typeNode?.SelectNodes(".//a")
            ?.Select(node => CleanText(node.InnerText))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    private static int? GetHiddenInt(HtmlDocument document, string id)
    {
        var value = document.GetElementbyId(id)?.GetAttributeValue("value", string.Empty);
        return int.TryParse(value, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static string? ExtractExternalId(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var match = JobIdRegex().Match(WebUtility.HtmlDecode(href));
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

    [GeneratedRegex(@"JobID=(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JobIdRegex();

    [GeneratedRegex(@"\(זמן ממוצע\s*:\s*[^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex AverageTimeRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
