using System.Web;
using JobWatcher.Configuration;

namespace JobWatcher.Sources.AllJobs;

public static class AllJobsUrlBuilder
{
    public static string Build(JobSourceOptions options, int page, int? positionOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(options.Url))
        {
            return WithPage(options.Url, page);
        }

        if (options.AllJobsFilter is null)
        {
            throw new InvalidOperationException($"Source '{options.Name}' must define either url or allJobsFilter.");
        }

        var filter = options.AllJobsFilter;
        var builder = new UriBuilder(filter.BaseUrl);
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["page"] = Math.Max(1, page).ToString();
        query["position"] = BuildPositionValue(filter, positionOverride);
        query["type"] = filter.Types.Count == 0 ? string.Empty : string.Join(",", filter.Types);
        query["source"] = filter.Source?.ToString() ?? string.Empty;
        query["duration"] = filter.Duration?.ToString() ?? string.Empty;
        query["exc"] = filter.Exclude ?? string.Empty;
        query["region"] = filter.Region ?? string.Empty;
        builder.Query = query.ToString();
        return builder.Uri.ToString();
    }

    public static IReadOnlyList<int> GetPositionIds(AllJobsFilterOptions filter)
    {
        return filter.Positions.Count > 0
            ? filter.Positions
            : filter.Position == 0 ? [] : [filter.Position];
    }

    private static string BuildPositionValue(AllJobsFilterOptions filter, int? positionOverride)
    {
        if (positionOverride is not null)
        {
            return positionOverride.Value.ToString();
        }

        var positions = GetPositionIds(filter);
        if (positions.Count > 0)
        {
            return positions[0].ToString();
        }

        return string.Empty;
    }

    private static string WithPage(string url, int page)
    {
        var builder = new UriBuilder(url);
        var query = HttpUtility.ParseQueryString(builder.Query);
        query["page"] = Math.Max(1, page).ToString();
        builder.Query = query.ToString();
        return builder.Uri.ToString();
    }
}
