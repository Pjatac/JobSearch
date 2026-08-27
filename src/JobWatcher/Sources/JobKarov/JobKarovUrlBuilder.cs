using System.Web;
using JobWatcher.Configuration;

namespace JobWatcher.Sources.JobKarov;

public static class JobKarovUrlBuilder
{
    public const int FixedSearchSize = 2;

    public static IReadOnlyList<string> GetSpecialities(JobSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.IsNullOrWhiteSpace(options.Url))
        {
            return [];
        }

        var filter = options.JobKarovFilter
            ?? throw new InvalidOperationException($"Source '{options.Name}' must define either url or jobKarovFilter.");
        var specialities = filter.Specialities
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return specialities.Count > 0 ? specialities : string.IsNullOrWhiteSpace(filter.Speciality) ? [] : [filter.Speciality.Trim()];
    }

    public static string Build(JobSourceOptions options, string? speciality = null)
    {
        if (!string.IsNullOrWhiteSpace(options.Url))
        {
            return options.Url;
        }

        if (options.JobKarovFilter is null)
        {
            throw new InvalidOperationException($"Source '{options.Name}' must define either url or jobKarovFilter.");
        }

        var filter = options.JobKarovFilter;
        var builder = new UriBuilder(filter.BaseUrl);
        var query = HttpUtility.ParseQueryString(builder.Query);

        var effectiveSpeciality = speciality ?? GetSpecialities(options).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(effectiveSpeciality) && string.IsNullOrWhiteSpace(filter.Query))
        {
            throw new InvalidOperationException($"Source '{options.Name}' must define at least one JobKarov speciality or query.");
        }

        if (!string.IsNullOrWhiteSpace(effectiveSpeciality))
        {
            query["speciality"] = effectiveSpeciality;
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            query["query"] = filter.Query.Trim();
        }

        if (filter.Roles.Count > 0)
        {
            query["role"] = string.Join(",", filter.Roles);
        }

        if (filter.Areas.Count > 0)
        {
            query["area"] = string.Join(",", filter.Areas);
        }

        query["size"] = FixedSearchSize.ToString(System.Globalization.CultureInfo.InvariantCulture);

        builder.Query = query.ToString();
        return builder.Uri.ToString();
    }
}
