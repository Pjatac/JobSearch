using System.Text.RegularExpressions;
using JobWatcher.Models;

namespace JobWatcher.Services;

public sealed partial class DuplicateCandidateService
{
    private const double MinimumCandidateScore = 0.78;
    private const double MinimumTitleScore = 0.62;

    public DuplicateCandidatesOutput FindCandidates(
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<SourceSnapshot> snapshots)
    {
        snapshots = MergeSnapshotsBySiteFamily(snapshots);
        var candidates = new List<DuplicateCandidate>();

        for (var leftIndex = 0; leftIndex < snapshots.Count; leftIndex++)
        {
            var leftSnapshot = snapshots[leftIndex];
            for (var rightIndex = leftIndex + 1; rightIndex < snapshots.Count; rightIndex++)
            {
                var rightSnapshot = snapshots[rightIndex];
                if (IsSameSiteFamily(leftSnapshot.Source, rightSnapshot.Source))
                {
                    continue;
                }

                foreach (var left in leftSnapshot.Vacancies)
                {
                    foreach (var right in rightSnapshot.Vacancies)
                    {
                        var candidate = Score(left, right);
                        if (candidate is not null)
                        {
                            candidates.Add(candidate);
                        }
                    }
                }
            }
        }

        var ordered = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Left.Source, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Left.ExternalId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DuplicateCandidatesOutput
        {
            GeneratedAtUtc = generatedAtUtc,
            CandidateCount = ordered.Count,
            Candidates = ordered
        };
    }

    private static IReadOnlyList<SourceSnapshot> MergeSnapshotsBySiteFamily(IReadOnlyList<SourceSnapshot> snapshots)
    {
        return snapshots
            .GroupBy(snapshot => GetSiteFamily(snapshot.Source), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var vacancies = new Dictionary<string, JobVacancy>(StringComparer.OrdinalIgnoreCase);
                foreach (var snapshot in group)
                {
                    foreach (var vacancy in snapshot.Vacancies)
                    {
                        vacancies.TryAdd(vacancy.ExternalId, vacancy);
                    }
                }

                var first = group.First();
                return new SourceSnapshot
                {
                    Source = GetSiteFamily(first.Source),
                    CollectedAtUtc = first.CollectedAtUtc,
                    Vacancies = vacancies.Values.ToList()
                };
            })
            .ToList();
    }

    private static DuplicateCandidate? Score(JobVacancy left, JobVacancy right)
    {
        var titleScore = TokenSimilarity(NormalizeTitle(left.Title), NormalizeTitle(right.Title));
        if (titleScore < MinimumTitleScore)
        {
            return null;
        }

        var leftCompany = NormalizeCompany(left.Company);
        var rightCompany = NormalizeCompany(right.Company);
        var companyComparable = leftCompany is not null && rightCompany is not null;
        var companyScore = companyComparable ? TokenSimilarity(leftCompany!, rightCompany!) : 0;

        var locationScore = TokenSimilarity(NormalizeFreeText(left.Location), NormalizeFreeText(right.Location));
        var totalScore = (titleScore * 0.65) + (companyScore * 0.25) + (locationScore * 0.10);

        if (!companyComparable && titleScore >= 0.90)
        {
            totalScore += 0.08;
        }

        totalScore = Math.Min(1, totalScore);
        if (totalScore < MinimumCandidateScore)
        {
            return null;
        }

        var reasons = new List<string> { $"title:{titleScore:0.00}" };
        if (companyComparable)
        {
            reasons.Add($"company:{companyScore:0.00}");
        }

        if (locationScore > 0)
        {
            reasons.Add($"location:{locationScore:0.00}");
        }

        return new DuplicateCandidate
        {
            Score = Math.Round(totalScore, 3),
            Reasons = reasons,
            Left = ToCandidateVacancy(left),
            Right = ToCandidateVacancy(right)
        };
    }

    private static DuplicateCandidateVacancy ToCandidateVacancy(JobVacancy vacancy)
    {
        return new DuplicateCandidateVacancy
        {
            Source = vacancy.Source,
            ExternalId = vacancy.ExternalId,
            Title = vacancy.Title,
            Company = vacancy.Company,
            Location = vacancy.Location,
            Url = vacancy.Url
        };
    }

    private static bool IsSameSiteFamily(string leftSource, string rightSource)
    {
        return string.Equals(GetSiteFamily(leftSource), GetSiteFamily(rightSource), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSiteFamily(string source)
    {
        var separatorIndex = source.IndexOf('-', StringComparison.Ordinal);
        return separatorIndex <= 0 ? source : source[..separatorIndex];
    }

    private static double TokenSimilarity(string? left, string? right)
    {
        var leftTokens = Tokenize(left);
        var rightTokens = Tokenize(right);
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0;
        }

        var intersection = leftTokens.Intersect(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
        var union = leftTokens.Union(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
        var jaccard = union == 0 ? 0 : (double)intersection / union;
        var edit = EditSimilarity(string.Join(' ', leftTokens), string.Join(' ', rightTokens));

        return Math.Max(jaccard, edit);
    }

    private static IReadOnlyList<string> Tokenize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeTitle(string? value)
    {
        var normalized = NormalizeFreeText(value);
        if (normalized is null)
        {
            return string.Empty;
        }

        normalized = DotNetRegex().Replace(normalized, " dotnet ");
        normalized = BackendRegex().Replace(normalized, " backend ");
        normalized = FullStackRegex().Replace(normalized, " fullstack ");
        normalized = SeniorityRegex().Replace(normalized, " ");
        normalized = DeveloperRegex().Replace(normalized, " developer ");
        return WhitespaceRegex().Replace(normalized, " ").Trim();
    }

    private static string? NormalizeCompany(string? value)
    {
        var normalized = NormalizeFreeText(value);
        if (normalized is null)
        {
            return null;
        }

        if (HiddenCompanyRegex().IsMatch(normalized))
        {
            return null;
        }

        return normalized;
    }

    private static string? NormalizeFreeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        normalized = SeparatorsRegex().Replace(normalized, " ");
        normalized = WhitespaceRegex().Replace(normalized, " ").Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    private static double EditSimilarity(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return 0;
        }

        var distance = LevenshteinDistance(left, right);
        return 1 - ((double)distance / Math.Max(left.Length, right.Length));
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var index = 0; index <= right.Length; index++)
        {
            previous[index] = index;
        }

        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;
            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                var cost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                    previous[rightIndex - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    [GeneratedRegex(@"(?<![a-z0-9])(?:\.?net|dotnet|asp\.?net)(?![a-z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex DotNetRegex();

    [GeneratedRegex(@"(?<![a-z0-9])back\s*-?\s*end(?![a-z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex BackendRegex();

    [GeneratedRegex(@"(?<![a-z0-9])full\s*-?\s*stack(?![a-z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex FullStackRegex();

    [GeneratedRegex(@"(?<![a-z0-9])(?:senior|sr|middle|mid|junior|jr)(?![a-z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex SeniorityRegex();

    [GeneratedRegex(@"(?<![a-z0-9])(?:developer|programmer|engineer|מתכנת|מפתח|מהנדס)(?![a-z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex DeveloperRegex();

    [GeneratedRegex(@"(?:^|\s)(?:-?\s*חסוי\s*-?|confidential|hidden)(?:\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex HiddenCompanyRegex();

    [GeneratedRegex(@"[^\p{L}\p{N}#+.]+")]
    private static partial Regex SeparatorsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
