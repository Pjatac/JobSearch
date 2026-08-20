using System.Text.RegularExpressions;
using JobWatcher.Models;

namespace JobWatcher.Services;

/// <summary>
/// Reviews the final <c>newJobs</c> list for entries that are the same job more than once.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately different from <see cref="DuplicateCandidateService"/>. That one scores fuzzy
/// cross-site matches over full snapshots; this one asks whether the list actually delivered
/// repeats itself. So it runs after output deduplication, does not skip pairs from the same site —
/// a single site can list one job several times — and matches on exact normalized keys, because a
/// report meant to be acted on should be explainable rather than scored.
/// </para>
/// <para>
/// An identical description alone never groups jobs as duplicates. Employers reuse a boilerplate
/// opening across unrelated postings, and Glassdoor's search API returns a short shared teaser
/// instead of the posting text, so description equality is reported separately as context.
/// </para>
/// </remarks>
public sealed partial class OutputDuplicateService
{
    /// <summary>
    /// Descriptions shorter than this are too generic to mean anything ("Backend developer wanted"),
    /// so they are not treated as evidence in either direction.
    /// </summary>
    private const int MinimumComparableDescriptionLength = 60;

    public OutputDuplicatesOutput Review(DateTimeOffset generatedAtUtc, IReadOnlyList<SourceOutput> sourceOutputs)
    {
        var jobs = sourceOutputs.SelectMany(source => source.NewJobs).ToList();
        var keys = jobs.Select(Key.From).ToList();

        var groups = BuildDuplicateGroups(jobs, keys);
        var sharedDescriptions = BuildSharedDescriptionGroups(jobs, keys);

        return new OutputDuplicatesOutput
        {
            GeneratedAtUtc = generatedAtUtc,
            ReviewedJobCount = jobs.Count,
            DuplicateGroupCount = groups.Count,
            RedundantJobCount = groups.Sum(group => group.Count - 1),
            SharedDescriptionGroupCount = sharedDescriptions.Count,
            DuplicateGroups = groups,
            SharedDescriptionGroups = sharedDescriptions
        };
    }

    private static List<OutputDuplicateGroup> BuildDuplicateGroups(IReadOnlyList<JobVacancy> jobs, IReadOnlyList<Key> keys)
    {
        // Union-find, so that A~B and B~C report as one group of three rather than two pairs.
        var parents = Enumerable.Range(0, jobs.Count).ToArray();
        var reasonsByRoot = new Dictionary<int, SortedSet<string>>();

        for (var left = 0; left < jobs.Count; left++)
        {
            for (var right = left + 1; right < jobs.Count; right++)
            {
                var reason = MatchReason(keys[left], keys[right]);
                if (reason is null)
                {
                    continue;
                }

                var leftRoot = Find(parents, left);
                var rightRoot = Find(parents, right);
                var merged = MergeReasons(reasonsByRoot, leftRoot, rightRoot);
                merged.Add(reason);

                parents[rightRoot] = leftRoot;
                reasonsByRoot.Remove(rightRoot);
                reasonsByRoot[leftRoot] = merged;
            }
        }

        return jobs
            .Select((job, index) => (Job: job, Root: Find(parents, index)))
            .GroupBy(entry => entry.Root)
            .Where(group => group.Count() > 1)
            .Select(group => new OutputDuplicateGroup
            {
                Reasons = reasonsByRoot.TryGetValue(group.Key, out var reasons) ? [.. reasons] : [],
                Count = group.Count(),
                Members = group.Select(entry => ToMember(entry.Job)).ToList()
            })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Members[0].Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? MatchReason(Key left, Key right)
    {
        if (left.Url is not null && left.Url == right.Url)
        {
            return "same-url";
        }

        if (left.Title is null || left.Title != right.Title)
        {
            return null;
        }

        if (left.Company is not null && left.Company == right.Company)
        {
            return "same-title-and-company";
        }

        // The same posting relisted by an agency: the company differs, but title and description
        // match exactly. Description is only trusted here because the title already matches.
        if (left.Description is not null && left.Description == right.Description)
        {
            return "same-title-and-description";
        }

        return null;
    }

    private static List<OutputDuplicateGroup> BuildSharedDescriptionGroups(IReadOnlyList<JobVacancy> jobs, IReadOnlyList<Key> keys)
    {
        return jobs
            .Select((job, index) => (Job: job, Key: keys[index]))
            .Where(entry => entry.Key.Description is not null)
            .GroupBy(entry => entry.Key.Description, StringComparer.Ordinal)
            .Where(group => group.Count() > 1
                && group.Select(entry => entry.Key.Title).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => new OutputDuplicateGroup
            {
                Reasons = ["identical-description-different-titles"],
                Count = group.Count(),
                Members = group.Select(entry => ToMember(entry.Job)).ToList()
            })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Members[0].Company, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static SortedSet<string> MergeReasons(Dictionary<int, SortedSet<string>> reasonsByRoot, int leftRoot, int rightRoot)
    {
        var merged = reasonsByRoot.TryGetValue(leftRoot, out var leftReasons)
            ? leftReasons
            : new SortedSet<string>(StringComparer.Ordinal);

        if (reasonsByRoot.TryGetValue(rightRoot, out var rightReasons))
        {
            merged.UnionWith(rightReasons);
        }

        return merged;
    }

    private static int Find(int[] parents, int index)
    {
        while (parents[index] != index)
        {
            parents[index] = parents[parents[index]];
            index = parents[index];
        }

        return index;
    }

    private static OutputDuplicateMember ToMember(JobVacancy job)
    {
        return new OutputDuplicateMember
        {
            Source = job.Source,
            ExternalId = job.ExternalId,
            Title = job.Title,
            Company = job.Company,
            Url = job.Url
        };
    }

    private sealed record Key(string? Title, string? Company, string? Description, string? Url)
    {
        public static Key From(JobVacancy job)
        {
            return new Key(
                Normalize(job.Title),
                Normalize(job.Company),
                NormalizeDescription(job.Description),
                NormalizeUrl(job.Url));
        }

        private static string? NormalizeDescription(string? value)
        {
            var normalized = Normalize(value);
            return normalized is null || normalized.Length < MinimumComparableDescriptionLength ? null : normalized;
        }

        private static string? NormalizeUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return null;
            }

            // The query cannot be dropped: it frequently carries the listing id itself, as in
            // AllJobs' "UploadSingle.aspx?JobID=7186099". Dropping it collapses every listing on
            // such a site to one key. Only parameters that identify a click rather than a job are
            // removed, and the rest are sorted so ordering differences do not matter.
            var query = uri.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(parameter => !IsClickTracking(parameter))
                .Select(parameter => parameter.ToLowerInvariant())
                .Order(StringComparer.Ordinal)
                .ToList();

            var address = $"{uri.Host}{uri.AbsolutePath}".ToLowerInvariant().TrimEnd('/');
            return query.Count == 0 ? address : $"{address}?{string.Join('&', query)}";
        }

        private static bool IsClickTracking(string parameter)
        {
            var name = parameter.Split('=', 2)[0];
            return name.StartsWith("utm_", StringComparison.OrdinalIgnoreCase)
                || name.Equals("gclid", StringComparison.OrdinalIgnoreCase)
                || name.Equals("fbclid", StringComparison.OrdinalIgnoreCase)
                || name.Equals("msclkid", StringComparison.OrdinalIgnoreCase);
        }

        private static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = SeparatorsRegex().Replace(value.Trim().ToLowerInvariant(), " ");
            normalized = WhitespaceRegex().Replace(normalized, " ").Trim();
            return normalized.Length == 0 ? null : normalized;
        }
    }

    [GeneratedRegex(@"[^\p{L}\p{N}#+.]+")]
    private static partial Regex SeparatorsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
