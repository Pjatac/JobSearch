namespace JobWatcher.Configuration;

/// <summary>
/// Validates only the configuration contract implemented by local source adapters. Site-specific
/// option labels and catalog completeness are deliberately outside this validator.
/// </summary>
public sealed class SourceProfileValidator
{
    public SourceProfileValidationResult Validate(JobSourceOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var errors = new List<string>();
        var warnings = new List<string>();
        var adapter = source.Adapter?.Trim();

        if (string.IsNullOrWhiteSpace(source.Name))
        {
            errors.Add("Profile name is required.");
        }

        if (string.IsNullOrWhiteSpace(adapter))
        {
            errors.Add("Source adapter is required.");
            return new SourceProfileValidationResult(errors, warnings);
        }

        if (source.MinimumExpectedVacancies < 0)
        {
            errors.Add("Minimum expected vacancies cannot be negative.");
        }

        switch (adapter)
        {
            case "JobKarov":
                ValidateJobKarov(source, errors);
                break;
            case "Drushim":
                ValidateDrushim(source, errors);
                break;
            case "AllJobs":
                ValidateAllJobs(source, errors, warnings);
                break;
            case "JobSwipeCo":
                ValidateJobSwipe(source, errors);
                break;
            case "Glassdoor":
                ValidateGlassdoor(source, errors);
                break;
            case "SecretTelAviv":
                ValidateSecretTelAviv(source, errors);
                break;
            case "DevJobs":
                ValidateDevJobs(source, errors);
                break;
            default:
                errors.Add($"Unknown source adapter '{adapter}'.");
                break;
        }

        return new SourceProfileValidationResult(errors, warnings);
    }

    private static void ValidateJobKarov(JobSourceOptions source, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(source.Url))
        {
            return;
        }

        if (source.JobKarovFilter is null)
        {
            errors.Add("JobKarov requires a direct URL or structured filters.");
        }
        else if (string.IsNullOrWhiteSpace(source.JobKarovFilter.Speciality))
        {
            errors.Add("JobKarov speciality ID is required for structured filters.");
        }
    }

    private static void ValidateDrushim(JobSourceOptions source, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(source.Url))
        {
            return;
        }

        if (source.DrushimFilter is null)
        {
            errors.Add("Drushim requires a direct URL or structured filters.");
        }
        else if (source.DrushimFilter.CategoryId <= 0)
        {
            errors.Add("Drushim category ID must be positive for structured filters.");
        }
    }

    private static void ValidateAllJobs(JobSourceOptions source, List<string> errors, List<string> warnings)
    {
        if (!string.IsNullOrWhiteSpace(source.Url))
        {
            return;
        }

        if (source.AllJobsFilter is null)
        {
            errors.Add("AllJobs requires a direct URL or structured filters.");
            return;
        }

        if (source.AllJobsFilter.Position == 0 && source.AllJobsFilter.Positions.Count == 0)
        {
            warnings.Add("AllJobs has no position IDs and may search an overly broad result set.");
        }
    }

    private static void ValidateJobSwipe(JobSourceOptions source, List<string> errors)
    {
        if (source.JobSwipeCoFilter?.SearchUrls.Count > 0)
        {
            return;
        }

        errors.Add("JobSwipe.co requires at least one search URL.");
    }

    private static void ValidateGlassdoor(JobSourceOptions source, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(source.Url) || source.GlassdoorFilter?.SearchUrls.Count > 0)
        {
            return;
        }

        errors.Add("Glassdoor requires at least one search URL or a direct URL.");
    }

    private static void ValidateSecretTelAviv(JobSourceOptions source, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(source.Url))
        {
            if (!IsSecretTelAvivUri(source.Url))
            {
                errors.Add("Secret Tel Aviv requires an absolute jobs.secrettelaviv.com search URL.");
            }

            return;
        }

        var filter = source.SecretTelAvivFilter;
        if (filter is null || !IsSecretTelAvivUri(filter.BaseUrl))
        {
            errors.Add("Secret Tel Aviv requires an absolute jobs.secrettelaviv.com base URL.");
            return;
        }

        try
        {
            if (!IsSecretTelAvivUri(Sources.SecretTelAviv.SecretTelAvivUrlBuilder.Build(filter)))
            {
                errors.Add("Secret Tel Aviv search URL must stay on jobs.secrettelaviv.com.");
            }
        }
        catch (InvalidOperationException)
        {
            errors.Add("Secret Tel Aviv requires a search URL.");
        }
    }

    private static bool IsSecretTelAvivUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var parsed) &&
        string.Equals(parsed.Host, "jobs.secrettelaviv.com", StringComparison.OrdinalIgnoreCase);

    private static void ValidateDevJobs(JobSourceOptions source, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(source.Url))
        {
            if (!IsDevJobsUri(source.Url))
            {
                errors.Add("DevJobs requires an absolute devjobs.co.il search URL.");
            }

            return;
        }

        var filter = source.DevJobsFilter;
        if (filter is null || !IsDevJobsUri(filter.BaseUrl))
        {
            errors.Add("DevJobs requires an absolute devjobs.co.il base URL.");
            return;
        }

        try
        {
            if (!IsDevJobsUri(Sources.DevJobs.DevJobsUrlBuilder.Build(filter, 1)))
            {
                errors.Add("DevJobs search URL must stay on devjobs.co.il.");
            }
        }
        catch (InvalidOperationException)
        {
            errors.Add("DevJobs requires a search URL.");
        }
    }

    private static bool IsDevJobsUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var parsed) &&
        string.Equals(parsed.Host, "devjobs.co.il", StringComparison.OrdinalIgnoreCase);
}

public sealed record SourceProfileValidationResult(
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}
