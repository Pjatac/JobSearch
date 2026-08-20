using System.Security.Cryptography;
using System.Text;
using JobWatcher.Models;

namespace JobWatcher.Utilities;

public static class VacancyIdentity
{
    public static string CreateKey(JobVacancy vacancy)
    {
        return $"{vacancy.Source}:{vacancy.ExternalId}".ToLowerInvariant();
    }

    public static string CreateFingerprint(string source, params string?[] stableFields)
    {
        var normalized = string.Join("|", stableFields.Select(field => (field ?? string.Empty).Trim().ToLowerInvariant()));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{source.ToLowerInvariant()}|{normalized}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
