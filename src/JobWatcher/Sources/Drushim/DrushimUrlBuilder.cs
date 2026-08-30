using System.Web;
using JobWatcher.Configuration;

namespace JobWatcher.Sources.Drushim;

public static class DrushimUrlBuilder
{
    public static string Build(JobSourceOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Url))
        {
            return options.Url;
        }

        if (options.DrushimFilter is null)
        {
            throw new InvalidOperationException($"Source '{options.Name}' must define either url or drushimFilter.");
        }

        var filter = options.DrushimFilter;
        var baseUri = new Uri(filter.BaseUrl.TrimEnd('/') + "/");
        var categories = GetCategoryIds(filter);
        var subcategories = filter.SubcategoryIds.Count > 0
            ? filter.SubcategoryIds
            : filter.SubcategoryId is { } subcategoryId ? [subcategoryId] : [];
        var categoryId = categories[0];
        var hasQuery = !string.IsNullOrWhiteSpace(filter.Query);

        var path = subcategories.Count > 0 && !hasQuery
            ? $"jobs/subcat/{string.Join('-', subcategories)}/"
            : $"jobs/cat{categoryId}/";

        if (filter.AreaIds.Count > 0)
        {
            path += $"area/{string.Join('-', filter.AreaIds)}/";
        }

        var builder = new UriBuilder(new Uri(baseUri, path));
        var query = HttpUtility.ParseQueryString(string.Empty);

        if (subcategories.Count > 0 && !hasQuery)
        {
            query["catdir"] = categoryId.ToString();
        }

        if (hasQuery)
        {
            query["searchterm"] = filter.Query.Trim();
        }

        if (filter.Scopes.Count > 0)
        {
            query["scope"] = string.Join("-", filter.Scopes);
        }

        if (!string.IsNullOrWhiteSpace(filter.ExperienceRange))
        {
            query["experience"] = filter.ExperienceRange;
        }

        if (filter.GeoLexId is not null)
        {
            query["geolexid"] = filter.GeoLexId.ToString();
        }

        if (filter.IncludeAreaAround)
        {
            query["isaa"] = "true";
        }

        if (filter.Experience is not null)
        {
            query["ssaen"] = filter.Experience.ToString();
        }

        if (filter.Range is not null)
        {
            query["range"] = filter.Range.ToString();
        }

        builder.Query = query.ToString();
        return builder.Uri.ToString();
    }

    public static string BuildApiSearch(JobSourceOptions options, int page, int? categoryIdOverride = null)
    {
        if (options.DrushimFilter is null)
        {
            throw new InvalidOperationException($"Source '{options.Name}' must define drushimFilter for API search.");
        }

        var filter = options.DrushimFilter;
        var builder = new UriBuilder(new Uri(new Uri(filter.BaseUrl.TrimEnd('/') + "/"), "api/jobs/search"));
        var query = HttpUtility.ParseQueryString(string.Empty);
        var categories = GetCategoryIds(filter);
        var categoryId = categoryIdOverride ?? categories[0];
        var subcategories = filter.SubcategoryIds.Count > 0
            ? filter.SubcategoryIds
            : filter.SubcategoryId is { } subcategoryId ? [subcategoryId] : [];
        var hasQuery = !string.IsNullOrWhiteSpace(filter.Query);

        query["catdir"] = categoryId.ToString();

        if (subcategories.Count > 0 && !hasQuery)
        {
            query["subcat"] = string.Join("-", subcategories);
        }

        if (hasQuery)
        {
            query["searchterm"] = filter.Query.Trim();
        }

        if (filter.AreaIds.Count > 0)
        {
            query["area"] = string.Join("-", filter.AreaIds);
        }

        if (filter.Scopes.Count > 0)
        {
            query["scope"] = string.Join("-", filter.Scopes);
        }

        if (!string.IsNullOrWhiteSpace(filter.ExperienceRange))
        {
            query["experience"] = filter.ExperienceRange;
        }

        if (filter.GeoLexId is not null)
        {
            query["geolexid"] = filter.GeoLexId.ToString();
        }

        if (filter.IncludeAreaAround)
        {
            query["isaa"] = "true";
        }

        if (filter.Experience is not null)
        {
            query["ssaen"] = filter.Experience.ToString();
        }

        if (filter.Range is not null)
        {
            query["range"] = filter.Range.ToString();
        }

        builder.Query = query.ToString();
        return $"{builder.Uri}&isAA=true&page={Math.Max(1, page)}";
    }

    public static IReadOnlyList<int> GetCategoryIds(DrushimFilterOptions filter)
    {
        return filter.CategoryIds.Count > 0
            ? filter.CategoryIds
            : [filter.CategoryId];
    }
}
