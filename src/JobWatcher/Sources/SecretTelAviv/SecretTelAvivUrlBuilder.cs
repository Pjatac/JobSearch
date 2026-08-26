using JobWatcher.Configuration;

namespace JobWatcher.Sources.SecretTelAviv;

public static class SecretTelAvivUrlBuilder
{
    public static string Build(SecretTelAvivFilterOptions filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (!Uri.TryCreate(filter.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("Secret Tel Aviv base URL must be absolute.");
        }

        if (Uri.TryCreate(filter.SearchUrl, UriKind.Absolute, out var searchUri))
        {
            return searchUri.ToString();
        }

        if (string.IsNullOrWhiteSpace(filter.SearchUrl))
        {
            throw new InvalidOperationException("Secret Tel Aviv search URL is required.");
        }

        return new Uri(new Uri(baseUri.ToString().TrimEnd('/') + "/"), filter.SearchUrl.TrimStart('/')).ToString();
    }
}
