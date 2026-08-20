using System.Web;
using JobWatcher.Configuration;

namespace JobWatcher.Sources.JobKarov;

public static class JobKarovUrlBuilder
{
    public static string Build(JobSourceOptions options)
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

        query["speciality"] = filter.Speciality;

        if (filter.Roles.Count > 0)
        {
            query["role"] = string.Join(",", filter.Roles);
        }

        if (filter.Areas.Count > 0)
        {
            query["area"] = string.Join(",", filter.Areas);
        }

        if (filter.Size > 0)
        {
            query["size"] = filter.Size.ToString();
        }

        builder.Query = query.ToString();
        return builder.Uri.ToString();
    }
}
