# Job Watcher — Initial Implementation Brief for Codex

## 1. Goal

Create a **.NET 10 console application** that checks configured Israeli job-search pages, extracts vacancies, compares the current full result with the previous successful run, and writes a JSON file containing only newly discovered vacancies.

The first supported source is **JobKarov**.

The implementation must be extensible: each site must have its own source adapter behind a common interface. Do not build the application as a one-off JobKarov scraper.

## 2. Initial JobKarov URL

Use this URL for the first implementation:

```text
https://www.jobkarov.com/Search/?speciality=2119&role=2163%2C3893&area=50%2C70&size=2
```

The exact meaning and future construction of the filter parameters will be investigated separately. For now, treat the complete URL as configuration.

A manual browser check showed approximately **47 vacancies** and no visible pagination control.

Do not hard-code an expected count of 47. The count is only a diagnostic reference.

## 3. Important discovery about JobKarov

The vacancies are present in the ordinary HTTP response and are exposed as **JSON-LD structured data**.

The response contains data shaped approximately like this:

```json
{
  "@type": "ListItem",
  "position": 4,
  "item": {
    "@type": "JobPosting",
    "title": "...",
    "description": "...",
    "datePosted": "2026-06-07",
    "validThrough": "2026-07-07",
    "employmentType": ["FULL_TIME"],
    "hiringOrganization": {},
    "jobLocation": {},
    "url": "/Search/Site/2712464"
  }
}
```

Likely root shape:

```text
ItemList
└── itemListElement[]
    └── ListItem
        └── item
            └── JobPosting
```

The parser must also tolerate:

- a root `JobPosting`;
- an array containing `JobPosting` objects;
- nested `ItemList` / `ListItem` structures;
- more than one `<script type="application/ld+json">` block.

The location and company objects may be incomplete or have different nested shapes.

### Consequence

For JobKarov, use:

- `HttpClient`;
- HTML parsing only to locate JSON-LD script elements;
- `System.Text.Json` to inspect JSON-LD.

Do **not** add Playwright or Selenium unless a future source demonstrably requires a browser.

The address and login popups are client-side UI and do not prevent the server response from containing the vacancies.

## 4. Required solution shape

Suggested structure:

```text
JobWatcher/
├── JobWatcher.sln
├── src/
│   └── JobWatcher/
│       ├── JobWatcher.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── Configuration/
│       │   ├── JobWatcherOptions.cs
│       │   └── JobSourceOptions.cs
│       ├── Models/
│       │   ├── JobVacancy.cs
│       │   ├── SourceSnapshot.cs
│       │   ├── SourceRunResult.cs
│       │   ├── JobDiff.cs
│       │   └── RunOutput.cs
│       ├── Sources/
│       │   ├── IJobSource.cs
│       │   └── JobKarov/
│       │       ├── JobKarovSource.cs
│       │       └── JobKarovJsonLdParser.cs
│       ├── Persistence/
│       │   ├── ISnapshotStore.cs
│       │   └── JsonSnapshotStore.cs
│       ├── Services/
│       │   ├── JobComparisonService.cs
│       │   └── JobWatcherRunner.cs
│       └── Utilities/
│           ├── AtomicFileWriter.cs
│           ├── VacancyIdentity.cs
│           └── TextNormalization.cs
├── tests/
│   └── JobWatcher.Tests/
│       ├── JobWatcher.Tests.csproj
│       ├── JobKarovJsonLdParserTests.cs
│       ├── JobComparisonServiceTests.cs
│       └── JsonSnapshotStoreTests.cs
└── data/
    ├── snapshots/
    ├── output/
    └── diagnostics/
```

The exact file split may vary, but keep these responsibilities separate.

## 5. Technology constraints

Use:

- **.NET 10**
- nullable reference types enabled;
- implicit usings enabled;
- `System.Text.Json`;
- `IHttpClientFactory`;
- `Microsoft.Extensions.Hosting`;
- `Microsoft.Extensions.Options`;
- structured logging through `Microsoft.Extensions.Logging`;
- `HtmlAgilityPack` or AngleSharp only for locating JSON-LD scripts.

Prefer minimal dependencies.

Do not introduce:

- Entity Framework;
- a database;
- Playwright;
- Selenium;
- a web API;
- a background Windows Service;
- a scheduler inside the application.

Scheduling will be handled externally later, for example by Windows Task Scheduler, cron, a container scheduler, or CI.

## 6. Canonical vacancy model

Use a normalized model similar to:

```csharp
public sealed record JobVacancy
{
    public required string Source { get; init; }
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public string? Company { get; init; }
    public string? Location { get; init; }
    public required string Url { get; init; }
    public string? Description { get; init; }
    public DateOnly? DatePosted { get; init; }
    public DateOnly? ValidThrough { get; init; }
    public IReadOnlyList<string> EmploymentTypes { get; init; } = [];
    public required DateTimeOffset CollectedAtUtc { get; init; }
}
```

Minor changes are acceptable if justified.

### External identity

For JobKarov, derive `ExternalId` from the numeric part of a URL such as:

```text
/Search/Site/2712464
```

Result:

```text
2712464
```

Normalize relative URLs to absolute URLs:

```text
https://www.jobkarov.com/Search/Site/2712464
```

The primary identity key is:

```text
Source + ExternalId
```

If a future source has no stable external ID, use a deterministic SHA-256 fingerprint from normalized stable fields. Keep that fallback outside the JobKarov-specific parser.

## 7. Configuration

Initial `appsettings.json` example:

```json
{
  "JobWatcher": {
    "DataDirectory": "data",
    "RequestTimeoutSeconds": 30,
    "Sources": [
      {
        "Name": "JobKarov",
        "Enabled": true,
        "Url": "https://www.jobkarov.com/Search/?speciality=2119&role=2163%2C3893&area=50%2C70&size=2",
        "MinimumExpectedVacancies": 1
      }
    ]
  }
}
```

Requirements:

- source URL must be configurable;
- source can be enabled or disabled;
- output paths derive from `DataDirectory`;
- the app must create missing directories;
- no secrets are needed for the initial source.

Do not model the current filter parameters individually yet. Keep the complete URL.

## 8. Fetching requirements

Configure the JobKarov `HttpClient` with:

- a realistic, explicit `User-Agent`;
- an `Accept` header for HTML;
- a finite timeout;
- automatic decompression if appropriate.

Example intent:

```text
GET configured JobKarov URL
```

Do not require login or cookies unless testing proves they are necessary.

Do not send multiple concurrent requests to the same source.

For the initial version, one request should be sufficient because the full result appears to be embedded in one response.

## 9. JSON-LD parsing requirements

The parser must:

1. locate every `script[type='application/ld+json']`;
2. parse each block independently;
3. recursively traverse JSON objects and arrays;
4. extract every object whose `@type` includes `JobPosting`;
5. tolerate `@type` being either a string or an array of strings;
6. tolerate malformed unrelated JSON-LD blocks by recording a warning and continuing;
7. avoid returning duplicate vacancies from duplicate structured-data blocks;
8. decode HTML entities in text fields;
9. strip HTML tags from descriptions or normalize them to plain text;
10. preserve Hebrew and other Unicode text correctly as UTF-8.

Company extraction should inspect common fields such as:

```text
hiringOrganization.name
```

Location extraction should inspect common shapes such as:

```text
jobLocation.address.addressLocality
jobLocation.address.addressRegion
jobLocation.name
```

If values are unavailable, return `null`; do not invent them.

## 10. Comparison behavior

Every successful source run produces a **complete current snapshot**.

Load the previous successful snapshot for that source and compare by canonical identity.

Calculate:

- new vacancies: present now, absent before;
- unchanged vacancies;
- removed vacancies: present before, absent now.

The main user-facing JSON output should contain **only newly discovered vacancies**, but include summary counts.

Removed vacancies may be counted in metadata but do not need to be included in the initial output unless useful for diagnostics.

### First-run behavior

On the first successful run:

- save the complete snapshot;
- output all current vacancies as `newJobs`;
- mark the output with `"isInitialRun": true`.

This behavior must be documented.

## 11. Persistence requirements

Store one full snapshot per source, for example:

```text
data/snapshots/jobkarov.json
```

Store the latest run output at:

```text
data/output/new-jobs.json
```

Optionally also store timestamped outputs:

```text
data/output/history/2026-08-05T081500Z.json
```

The snapshot must survive process restarts.

### Atomicity

Never overwrite a valid snapshot directly.

Required flow:

1. write new content to a temporary file in the same directory;
2. flush and close it;
3. atomically replace or rename it over the destination.

If fetching, parsing, validation, or writing fails, preserve the previous snapshot.

## 12. Parser-health safeguards

A broken parser must not silently replace a valid snapshot with an empty or clearly implausible result.

At minimum:

- zero parsed vacancies is a failure for JobKarov;
- fewer than `MinimumExpectedVacancies` is a failure;
- on failure, do not replace the snapshot;
- write the raw HTML response into `data/diagnostics/` with a timestamp;
- include the failure in the output/logs;
- return a non-zero process exit code when every enabled source fails.

Do not hard-code 47 as the minimum. Initial configuration should use `1`.

A large count drop should generate a warning. A reasonable initial rule:

```text
if previous count >= 10
and current count < 50% of previous count
then warning
```

Do not fail solely because of the warning unless explicitly configured later.

## 13. Output JSON

Expected shape:

```json
{
  "generatedAtUtc": "2026-08-05T08:15:00Z",
  "hasFailures": false,
  "totalNewJobs": 3,
  "sources": [
    {
      "source": "JobKarov",
      "status": "success",
      "isInitialRun": false,
      "previousCount": 44,
      "currentCount": 47,
      "newCount": 3,
      "removedCount": 0,
      "warnings": [],
      "newJobs": [
        {
          "source": "JobKarov",
          "externalId": "2712464",
          "title": "מפתח ...",
          "company": null,
          "location": "...",
          "url": "https://www.jobkarov.com/Search/Site/2712464",
          "description": "...",
          "datePosted": "2026-06-07",
          "validThrough": "2026-07-07",
          "employmentTypes": ["FULL_TIME"],
          "collectedAtUtc": "2026-08-05T08:15:00Z"
        }
      ]
    }
  ]
}
```

Use camelCase JSON names.

Use indented JSON for human readability in the initial version.

Do not escape Hebrew into `\uXXXX` sequences unless required by the serializer. Configure relaxed Unicode escaping appropriately.

## 14. Failure isolation

The design must support multiple sources.

A failure in one source must not prevent other sources from being fetched and compared.

Each source result should have a status such as:

```text
success
failed
disabled
```

The application exit code policy:

- `0`: all enabled sources succeeded;
- `1`: at least one enabled source failed but at least one succeeded;
- `2`: all enabled sources failed or application-level initialization failed.

Document the policy.

## 15. Logging

Log:

- application start;
- source name and URL;
- HTTP status;
- response size;
- number of JSON-LD blocks;
- number of extracted `JobPosting` objects;
- deduplicated vacancy count;
- previous/current/new/removed counts;
- snapshot path;
- output path;
- warnings and failures.

Do not log full vacancy descriptions during normal execution.

## 16. Tests

Write focused automated tests.

### JobKarov parser tests

Include fixtures for:

1. `ItemList → ListItem → JobPosting`;
2. direct root `JobPosting`;
3. multiple JSON-LD scripts;
4. `@type` as an array;
5. duplicate `JobPosting`;
6. missing company and location;
7. relative URL normalization;
8. malformed unrelated JSON-LD block;
9. Hebrew content preservation;
10. HTML description cleanup.

Use local HTML fixtures. Tests must not depend on the live website.

### Comparison tests

Cover:

- first run;
- no changes;
- one new vacancy;
- removed vacancy;
- same title with different IDs;
- duplicate current vacancies;
- normalized identity behavior.

### Persistence tests

Cover:

- missing snapshot;
- valid snapshot round trip;
- atomic replacement;
- failed write does not destroy the previous snapshot where practical.

## 17. Live smoke command

Provide a simple command:

```powershell
dotnet run --project src/JobWatcher
```

Expected after the first successful run:

```text
data/snapshots/jobkarov.json
data/output/new-jobs.json
```

Also provide:

```powershell
dotnet test
```

The README must explain first-run behavior and how to reset local state by deleting the source snapshot.

## 18. README requirements

Document:

- purpose;
- architecture;
- why JobKarov uses `HttpClient` rather than browser automation;
- configuration;
- first run versus later runs;
- output format;
- snapshot persistence;
- failure safeguards;
- exit codes;
- test and run commands;
- how to add another source adapter;
- known limitation: JobKarov filter parameter semantics are not yet modeled and the complete URL is currently configured directly.

## 19. Explicit non-goals for the first implementation

Do not implement yet:

- automatic discovery of JobKarov filter parameter values;
- a UI;
- email, Telegram, Slack, or push notifications;
- an internal scheduler;
- PostgreSQL or another database;
- Docker deployment;
- cloud hosting;
- AI ranking of vacancies;
- CV matching;
- authentication;
- browser automation;
- full removed/changed-vacancy history;
- applying to vacancies.

## 20. Definition of Done

The initial task is complete when:

1. the solution builds on .NET 10;
2. unit tests pass;
3. the configured JobKarov URL is fetched through `HttpClient`;
4. vacancies are extracted from JSON-LD;
5. relative URLs and stable IDs are normalized;
6. a complete source snapshot is saved;
7. a diff against the previous successful snapshot is calculated;
8. `new-jobs.json` contains only new vacancies plus summary metadata;
9. a failed/empty parse cannot overwrite the previous valid snapshot;
10. Hebrew content is preserved correctly;
11. the code is structured so a second source can be added without modifying comparison or persistence logic;
12. the README contains run, test, configuration, and reset instructions.

## 21. Execution guidance for Codex

Before coding:

1. inspect the repository and decide whether this should be a standalone solution or fit into an existing solution;
2. report the intended file layout briefly;
3. do not change unrelated projects;
4. do not introduce browser automation;
5. use a saved HTML fixture for parser tests;
6. keep the live-site interaction behind `IJobSource`;
7. keep comparison and persistence source-independent.

During implementation:

- prefer small, reviewable commits;
- run targeted tests while developing;
- run the full test suite before declaring completion;
- perform one live smoke fetch only after parser tests pass;
- do not repeatedly hit the live site during unit-test work.

At completion, report:

- files added/changed;
- test results;
- live smoke result and parsed count;
- output and snapshot paths;
- any discrepancy between the browser-observed approximate count and the parsed count;
- any assumptions about JobKarov JSON-LD structure;
- remaining limitations.
