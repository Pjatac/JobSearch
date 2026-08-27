using JobWatcher.Configuration;

namespace JobWatcher.Sources.DevJobs;

public static class DevJobsUrlBuilder
{
    public static IReadOnlyList<DevJobsSearchScope> GetSearchScopes(DevJobsFilterOptions filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.UseSearchUrlOverride)
        {
            return [new DevJobsSearchScope(null, null)];
        }

        var districts = filter.Districts.Count > 0
            ? filter.Districts
            : string.IsNullOrWhiteSpace(filter.District) ? [] : [filter.District];
        var scopes = districts
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => new DevJobsSearchScope(value.Trim(), null))
            .Concat(filter.Cities
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => new DevJobsSearchScope(null, value.Trim())))
            .Distinct()
            .ToList();
        return scopes.Count == 0 ? [new DevJobsSearchScope(null, null)] : scopes;
    }

    public static string Build(DevJobsFilterOptions filter, int page, DevJobsSearchScope? scope = null)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (!Uri.TryCreate(filter.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("DevJobs base URL must be absolute.");
        }

        var effectiveScope = scope ?? GetSearchScopes(filter).First();
        if (!filter.UseSearchUrlOverride && (filter.DeveloperTypes.Count > 0 || effectiveScope.District is not null || effectiveScope.City is not null))
        {
            var structuredBuilder = new UriBuilder(new Uri(new Uri(baseUri.ToString().TrimEnd('/') + "/"), "jobs-grid"));
            var structuredQuery = System.Web.HttpUtility.ParseQueryString(structuredBuilder.Query);
            if (filter.DeveloperTypes.Count > 0)
            {
                structuredQuery["developerTypes"] = string.Join(',', filter.DeveloperTypes.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(effectiveScope.District))
            {
                structuredQuery["district"] = effectiveScope.District;
            }

            if (!string.IsNullOrWhiteSpace(effectiveScope.City))
            {
                structuredQuery["city"] = effectiveScope.City;
            }

            structuredQuery["page"] = Math.Max(1, page).ToString(System.Globalization.CultureInfo.InvariantCulture);
            structuredBuilder.Query = structuredQuery.ToString();
            return structuredBuilder.Uri.ToString();
        }

        if (string.IsNullOrWhiteSpace(filter.SearchUrl))
        {
            throw new InvalidOperationException("DevJobs search URL is required.");
        }

        var searchUri = Uri.TryCreate(filter.SearchUrl, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(new Uri(baseUri.ToString().TrimEnd('/') + "/"), filter.SearchUrl.TrimStart('/'));
        var builder = new UriBuilder(searchUri);
        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
        query["page"] = Math.Max(1, page).ToString(System.Globalization.CultureInfo.InvariantCulture);
        builder.Query = query.ToString();
        return builder.Uri.ToString();
    }
}

public sealed record DevJobsSearchScope(string? District, string? City);
