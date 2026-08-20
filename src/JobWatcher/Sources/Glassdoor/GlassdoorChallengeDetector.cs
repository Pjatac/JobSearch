using System.Net;
using HtmlAgilityPack;

namespace JobWatcher.Sources.Glassdoor;

/// <summary>
/// Recognises anti-bot interstitials so a blocked run reports "blocked" instead of "parser found
/// nothing". Detection deliberately stops the run: working around a challenge is an operator
/// decision, not something the adapter retries on its own.
/// </summary>
public static class GlassdoorChallengeDetector
{
    private static readonly string[] ChallengeTitleFragments =
    [
        "security | glassdoor",
        "just a moment",
        "attention required",
        "access denied",
        "are you a human",
        "checking your browser"
    ];

    public static string? Detect(HttpStatusCode statusCode, string html)
    {
        var title = ReadTitle(html);

        var matchedFragment = title is null
            ? null
            : ChallengeTitleFragments.FirstOrDefault(fragment => title.Contains(fragment, StringComparison.OrdinalIgnoreCase));

        if (matchedFragment is not null)
        {
            return $"Glassdoor served an anti-bot challenge page (HTTP {(int)statusCode}, title '{title}').";
        }

        if (statusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
        {
            return $"Glassdoor refused the request with HTTP {(int)statusCode}{(title is null ? string.Empty : $" (title '{title}')")}.";
        }

        return null;
    }

    private static string? ReadTitle(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);
        var title = document.DocumentNode.SelectSingleNode("//title")?.InnerText;
        return string.IsNullOrWhiteSpace(title) ? null : WebUtility.HtmlDecode(title).Trim();
    }
}
