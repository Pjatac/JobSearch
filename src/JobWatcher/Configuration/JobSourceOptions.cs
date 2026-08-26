namespace JobWatcher.Configuration;

public sealed class JobSourceOptions
{
    public required string Name { get; init; }
    public string? Adapter { get; init; }
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// An optional source still runs and still reports its failure, but its failure does not make
    /// the process exit non-zero. Used for sources with unreliable access (anti-bot protection),
    /// so a blocked Glassdoor does not mark an otherwise healthy run as failed.
    /// </summary>
    public bool Optional { get; init; }

    public string? Url { get; init; }
    public int MinimumExpectedVacancies { get; init; } = 1;
    public int? MaximumVacancyAgeDays { get; init; }
    public JobKarovFilterOptions? JobKarovFilter { get; init; }
    public DrushimFilterOptions? DrushimFilter { get; init; }
    public AllJobsFilterOptions? AllJobsFilter { get; init; }
    public JobSwipeCoFilterOptions? JobSwipeCoFilter { get; init; }
    public GlassdoorFilterOptions? GlassdoorFilter { get; init; }
    public SecretTelAvivFilterOptions? SecretTelAvivFilter { get; init; }
    public DevJobsFilterOptions? DevJobsFilter { get; init; }
}

public sealed class JobKarovFilterOptions
{
    public string BaseUrl { get; init; } = "https://www.jobkarov.com/Search/";
    public required string Speciality { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<string> Areas { get; init; } = [];
    public int Size { get; init; } = 2;
}

public sealed class DrushimFilterOptions
{
    public string BaseUrl { get; init; } = "https://www.drushim.co.il";
    public int CategoryId { get; init; } = 6;
    public int? SubcategoryId { get; init; }
    public IReadOnlyList<int> SubcategoryIds { get; init; } = [];
    public IReadOnlyList<int> AreaIds { get; init; } = [];
    public int? GeoLexId { get; init; }
    public bool IncludeAreaAround { get; init; } = true;
    public int? Experience { get; init; } = 3;
    public string? ExperienceRange { get; init; }
    public IReadOnlyList<int> Scopes { get; init; } = [];
    public int? Range { get; init; } = 3;
}

public sealed class AllJobsFilterOptions
{
    public string BaseUrl { get; init; } = "https://www.alljobs.co.il/SearchResultsGuest.aspx";
    public int Position { get; init; }
    public IReadOnlyList<int> Positions { get; init; } = [];
    public IReadOnlyList<int> Types { get; init; } = [];
    public int? Source { get; init; }
    public int? Duration { get; init; }
    public string? Exclude { get; init; } = string.Empty;
    public string? Region { get; init; } = string.Empty;
    public int MaxPages { get; init; } = 25;
}

public sealed class JobSwipeCoFilterOptions
{
    public string BaseUrl { get; init; } = "https://jobswipe.co";
    public IReadOnlyList<string> SearchUrls { get; init; } = [];
    public int MaxDetailsPerSearch { get; init; } = 30;
}

public sealed class GlassdoorFilterOptions
{
    public string BaseUrl { get; init; } = "https://www.glassdoor.com";
    public IReadOnlyList<string> SearchUrls { get; init; } = [];

    /// <summary>
    /// Pause between consecutive Glassdoor requests. Glassdoor sits behind anti-bot protection, so
    /// requests are deliberately paced even though the volume is small. This is a delay between
    /// distinct requests, never a retry backoff: a blocked request is not retried.
    /// </summary>
    public double RequestDelaySeconds { get; init; } = 1;

    /// <summary>
    /// Safety ceiling on result pages walked per search URL. The walk normally ends on its own when
    /// Glassdoor stops offering a cursor for the next page; this only bounds the damage if it does
    /// not. The default covers the ~250 results an observed search advertised, at 30 per page.
    /// </summary>
    public int MaxPages { get; init; } = 12;

    /// <summary>Results requested per page. The site itself asks for 30.</summary>
    public int JobsPerPage { get; init; } = 30;
}

public sealed class SecretTelAvivFilterOptions
{
    public string BaseUrl { get; init; } = "https://jobs.secrettelaviv.com";
    public string SearchUrl { get; init; } = "/list/find/?query=Back+End";
    public int MaxDetailsPerSearch { get; init; } = 30;
}

public sealed class DevJobsFilterOptions
{
    public string BaseUrl { get; init; } = "https://devjobs.co.il";
    public string SearchUrl { get; init; } = "/jobs-grid?developerTypes=Backend&district=Hasharon";
    public string? NameFilter { get; init; }
    public int MaxPages { get; init; } = 10;
    public int MaxDetailsPerPage { get; init; } = 30;
}
