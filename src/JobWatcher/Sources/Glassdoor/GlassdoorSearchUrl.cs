using System.Text.RegularExpressions;

namespace JobWatcher.Sources.Glassdoor;

/// <summary>
/// The search parameters Glassdoor encodes into its SEO route, decoded so the same search can be
/// replayed against the search API.
/// </summary>
/// <remarks>
/// A route such as
/// <c>/Job/kfar-saba-backend-developer-jobs-SRCH_IL.0,9_IC4507116_KO10,27.htm</c> carries the whole
/// query: <c>kfar-saba-backend-developer-jobs</c> is the SEO slug, <c>IC4507116</c> is the city id,
/// and <c>KO10,27</c> is the character range inside the slug holding the keyword — here
/// <c>backend-developer</c>. Deriving these keeps a source configured by one URL, as it is today,
/// instead of restating every parameter in configuration.
/// </remarks>
public sealed partial record GlassdoorSearchUrl
{
    public required string Url { get; init; }
    public required string SeoFriendlyUrlInput { get; init; }
    public required string ParameterUrlInput { get; init; }
    public required string Keyword { get; init; }
    public required long LocationId { get; init; }
    public required string LocationType { get; init; }

    /// <summary>The single-letter location type Glassdoor uses in query strings (<c>C</c> for city).</summary>
    public required string LocationTypeCode { get; init; }

    public static GlassdoorSearchUrl? TryParse(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var route = RouteRegex().Match(uri.AbsolutePath);
        if (!route.Success)
        {
            return null;
        }

        var seoSlug = route.Groups["seo"].Value;
        var parameters = route.Groups["parameters"].Value;

        var location = LocationRegex().Match(parameters);
        if (!location.Success || !long.TryParse(location.Groups["id"].Value, out var locationId))
        {
            return null;
        }

        var keyword = ExtractKeyword(seoSlug, parameters);
        if (keyword is null)
        {
            return null;
        }

        var locationTypeCode = location.Groups["type"].Value.ToUpperInvariant();
        return new GlassdoorSearchUrl
        {
            Url = url,
            SeoFriendlyUrlInput = seoSlug,
            ParameterUrlInput = parameters,
            Keyword = keyword,
            LocationId = locationId,
            LocationType = ToLocationType(locationTypeCode),
            LocationTypeCode = locationTypeCode
        };
    }

    /// <summary>
    /// Rebuilds the query string Glassdoor's own search form appends. The keyword is encoded twice
    /// because Glassdoor encodes it twice: the captured request carries
    /// <c>sc.keyword=backend%2520developer</c>, and the value is replayed as the site produces it
    /// rather than as it arguably should be.
    /// </summary>
    public string BuildQueryString()
    {
        var doubleEncodedKeyword = Uri.EscapeDataString(Uri.EscapeDataString(Keyword));
        return $"locId={LocationId}&locT={LocationTypeCode}&sc.keyword={doubleEncodedKeyword}";
    }

    public string BuildOriginalPageUrl()
    {
        var keyword = Uri.EscapeDataString(Keyword);
        return $"{Url}?locId={LocationId}&locT={LocationTypeCode}&sc.keyword={keyword}";
    }

    private static string? ExtractKeyword(string seoSlug, string parameters)
    {
        var keywordRange = KeywordRangeRegex().Match(parameters);
        if (!keywordRange.Success ||
            !int.TryParse(keywordRange.Groups["start"].Value, out var start) ||
            !int.TryParse(keywordRange.Groups["end"].Value, out var end))
        {
            return null;
        }

        if (start < 0 || end > seoSlug.Length || end <= start)
        {
            return null;
        }

        return seoSlug[start..end].Replace('-', ' ').Trim();
    }

    private static string ToLocationType(string code)
    {
        return code switch
        {
            "C" => "CITY",
            "S" => "STATE",
            "M" => "METRO",
            "N" => "COUNTRY",
            _ => "CITY"
        };
    }

    [GeneratedRegex(@"^/Job/(?<seo>.+?)-SRCH_(?<parameters>.+?)\.htm$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RouteRegex();

    [GeneratedRegex(@"_I(?<type>[A-Z])(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LocationRegex();

    [GeneratedRegex(@"_KO(?<start>\d+),(?<end>\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KeywordRangeRegex();
}
