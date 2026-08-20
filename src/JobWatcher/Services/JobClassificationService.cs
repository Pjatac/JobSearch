using System.Text.RegularExpressions;
using JobWatcher.Configuration;
using JobWatcher.Models;
using Microsoft.Extensions.Options;

namespace JobWatcher.Services;

public sealed class JobClassificationService(IOptions<JobWatcherOptions> options)
{
    public JobClassification ClassifyJob(JobVacancy job) => ClassifyJob(job, options.Value.Classification);

    public static JobClassification ClassifyJob(JobVacancy job, JobClassificationOptions config)
    {
        if (!config.Enabled)
        {
            return new JobClassification
            {
                Classification = "review",
                Reasons = ["classification-disabled"],
                Flags = CreateFlags(job, config)
            };
        }

        var isGlassdoor = job.Source.Contains("glassdoor", StringComparison.OrdinalIgnoreCase);
        var description = isGlassdoor
            ? string.Empty
            : Truncate(job.Description, config.DescriptionScanLength);
        var searchable = JoinText(job.Title, job.Company, job.Location, description);
        var roleSearchable = JoinText(job.Title, description);
        var reasons = new List<string>();

        var includeMatches = FindMatches(searchable, config.IncludeSignals);
        var roleMatches = FindMatches(roleSearchable, config.RoleMismatchSignals);
        reasons.AddRange(roleMatches.Select(match => $"role-mismatch:{match}"));

        var juniorMatches = FindMatches(searchable, config.JuniorSignals);
        juniorMatches.AddRange(FindRegexMatches(searchable, config.JuniorExperiencePatterns));
        var hasSeniorOverride = FindMatches(searchable, config.SeniorOverrideSignals).Count > 0;
        if (juniorMatches.Count > 0 && !hasSeniorOverride)
        {
            reasons.AddRange(juniorMatches.Distinct(StringComparer.OrdinalIgnoreCase).Select(match => $"junior:{match}"));
        }

        var languageMatches = FindMatches(searchable, config.OtherPrimaryLanguages);
        if (languageMatches.Count > 0 && includeMatches.Count == 0)
        {
            reasons.AddRange(languageMatches.Select(match => $"other-language:{match}"));
        }

        if (reasons.Count > 0)
        {
            return new JobClassification
            {
                Classification = "excluded",
                Reasons = reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Flags = CreateFlags(job, config)
            };
        }

        if (includeMatches.Count > 0)
        {
            return new JobClassification
            {
                Classification = "relevant",
                Reasons = includeMatches.Select(match => $"include-signal:{match}").ToList(),
                Flags = CreateFlags(job, config)
            };
        }

        var reviewReasons = new List<string> { "no-include-signal" };
        if (isGlassdoor && string.IsNullOrWhiteSpace(job.Description))
        {
            reviewReasons.Add("glassdoor-short-description");
        }

        return new JobClassification
        {
            Classification = "review",
            Reasons = reviewReasons,
            Flags = CreateFlags(job, config)
        };
    }

    private static JobClassificationFlags CreateFlags(JobVacancy job, JobClassificationOptions config)
    {
        return new JobClassificationFlags
        {
            FarCommute = FindMatches(job.Location ?? string.Empty, config.FarCommuteLocations).Count > 0,
            Cyber = FindMatches(JoinText(job.Title, job.Company, job.Location, job.Description), config.CyberSignals).Count > 0
        };
    }

    private static List<string> FindMatches(string value, IReadOnlyList<string> terms)
    {
        return terms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Where(term => ContainsTerm(value, term))
            .ToList();
    }

    private static List<string> FindRegexMatches(string value, IReadOnlyList<string> patterns)
    {
        return patterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Where(pattern => Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .ToList();
    }

    private static bool ContainsTerm(string value, string term)
    {
        var phrasePattern = string.Join(@"\s+", term.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape));
        return Regex.IsMatch(value, $@"(?<![\p{{L}}\p{{N}}]){phrasePattern}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string JoinText(params string?[] values) => string.Join(' ', values.Where(value => !string.IsNullOrWhiteSpace(value))!);

    private static string Truncate(string? value, int length) =>
        string.IsNullOrWhiteSpace(value) || length <= 0 ? string.Empty : value[..Math.Min(value.Length, length)];
}
