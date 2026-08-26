using JobWatcher.Configuration;

namespace JobWatcher.Sources.DevJobs;

public static class DevJobsUrlBuilder
{
    public static string Build(DevJobsFilterOptions filter, int page)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (!Uri.TryCreate(filter.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("DevJobs base URL must be absolute.");
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
