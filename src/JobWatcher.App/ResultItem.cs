using JobWatcher.Models;

namespace JobWatcher.App;

public sealed record ResultItem
{
    public ResultItem(JobVacancy job, int index)
    {
        Index = index;
        Title = job.Title;
        Company = string.IsNullOrWhiteSpace(job.Company) ? "Company not provided" : job.Company;
        Classification = job.Classification?.Classification ?? "review";
        SourceLabel = job.Source.Split('-', 2)[0];
        Metadata = string.Join(" | ", new[]
        {
            FormatCompanyRating(job.CompanyRating),
            job.Location,
            job.DatePosted is { } datePosted ? $"Posted {datePosted:d}" : null,
            job.EmploymentTypes.Count > 0 ? string.Join(", ", job.EmploymentTypes) : null
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        ReasonTags = (job.Classification?.Reasons ?? []).Select(ToReasonTag).ToList();
        Url = job.Url;
        Description = job.Description?.Trim() ?? string.Empty;
        DetailsMetadata = string.Join(" | ", new[]
        {
            Company,
            SourceLabel,
            Metadata
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    public int Index { get; }
    public string ListNumber => $"#{Index}";
    public string Title { get; }
    public string Company { get; }
    public string Classification { get; }
    public string SourceLabel { get; }
    public string Metadata { get; }
    public IReadOnlyList<string> ReasonTags { get; }
    public string Url { get; }
    public string Description { get; }
    public string DetailsMetadata { get; }
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public string ClassificationBackground => Classification switch
    {
        "relevant" => "#E6F4F0",
        "excluded" => "#FDE8E7",
        _ => "#FFF3D6"
    };

    public string ClassificationForeground => Classification switch
    {
        "relevant" => "#147D72",
        "excluded" => "#A61B1B",
        _ => "#8A5A00"
    };

    private static string? FormatCompanyRating(double? rating)
    {
        return rating is > 0
            ? $"Rating {rating.Value:0.#}/5"
            : null;
    }

    private static string ToReasonTag(string reason)
    {
        var separator = reason.IndexOf(':');
        var kind = separator < 0 ? reason : reason[..separator];
        var value = separator < 0 ? string.Empty : reason[(separator + 1)..];
        return kind switch
        {
            "include-signal" => $"Matches {value}",
            "role-mismatch" => $"Different role: {value}",
            "other-language" => $"Other specialization: {value}",
            "junior" => $"Junior: {value}",
            "no-include-signal" => "No target match",
            "glassdoor-short-description" => "Short description",
            _ => reason
        };
    }
}
