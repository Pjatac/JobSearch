using System.Text;
using JobWatcher.Models;
using JobWatcher.Utilities;

namespace JobWatcher.Tests;

public sealed class JsonDefaultsTests
{
    [Fact]
    public async Task DeserializesStringListPropertiesFromStringOrNull()
    {
        const string json = """
        {
          "generatedAtUtc": "2026-09-08T00:00:00Z",
          "hasFailures": false,
          "totalNewJobs": 1,
          "sources": [
            {
              "source": "AllJobs",
              "status": "success",
              "warnings": null,
              "newJobs": [
                {
                  "source": "AllJobs",
                  "externalId": "1",
                  "title": "Backend Engineer",
                  "url": "https://example.test/job/1",
                  "employmentTypes": "Full Time",
                  "collectedAtUtc": "2026-09-08T00:00:00Z",
                  "classification": {
                    "classification": "relevant",
                    "reasons": "include-signal:Backend",
                    "flags": {
                      "farCommute": false,
                      "cyber": false
                    }
                  }
                }
              ]
            }
          ]
        }
        """;

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var output = await JsonDefaults.DeserializeAsync<RunOutput>(stream, CancellationToken.None);

        var source = Assert.Single(output!.Sources);
        Assert.Empty(source.Warnings);
        var job = Assert.Single(source.NewJobs);
        Assert.Equal(["Full Time"], job.EmploymentTypes);
        Assert.Equal(["include-signal:Backend"], job.Classification!.Reasons);
    }
}
