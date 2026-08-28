# Job Watcher Session Handoff

This file is the operational starting point for the next development session. Read it after
`AGENTS.md`, `README.md`, and `DEVELOPMENT_PLAN.md`.

## Immediate Engineering Priority

Analyze Drushim search responses and finish Drushim profile controls. The next known product task
is to determine which dynamic lists can be extracted from a Drushim search response, especially
whether categories and filters are query-dependent. Work from saved diagnostics first; if a live
request is needed, follow the one-request budget in `AGENTS.md`, save the response under
`data/diagnostics/`, and continue offline from that file.

## Current Functional State

- Runtime targets: .NET 10 console/CLI plus a .NET MAUI application for Windows and MacCatalyst.
- Sources: JobKarov, Drushim, AllJobs, JobSwipe.co, Glassdoor, and Secret Tel Aviv.
- User-owned settings are separate from defaults and live under MAUI AppData. Do not assume that
  editing `src/JobWatcher/appsettings.json` changes an existing user's profiles.
- The current Windows MAUI data root is:
  `C:\Users\pjata\AppData\Local\User Name\com.jobwatcher.app\Data`
- The Glassdoor session under that root is a secret. Do not read, print, copy, or commit it.
- Source HTTP requests share a retry policy: one retry after a timeout or internal cancellation,
  with the same URL, body, headers, cookies, and client identity. HTTP responses and anti-bot
  challenges are not retried.
- Sources may return `partial_failed` output after preserving already collected listings from a
  transient failure. Partial new jobs can be written to `new-jobs.json`, but partial snapshots do
  not replace the last durable source snapshot.

## Secret Tel Aviv Status

- `SecretTelAvivSource` uses a Chrome TLS profile. A normal .NET client did not successfully read
  the tested detail page; the TLS client returned HTTP 200.
- The observed search URL returned HTTP 200 and ten job cards. Each card has title, company,
  location, employment type, date, and a detail URL.
- The observed detail URL returned HTTP 200 and a `JobPosting` JSON-LD block with description,
  posting dates, company, location, and employment type.
- The source parses search cards, then loads up to `MaxDetailsPerSearch` detail pages sequentially
  (default 30) and merges the JSON-LD data into the listed vacancy.
- Both CLI and MAUI manual runs must use `AddJobWatcherCollector()` for registration. The Secret
  Tel Aviv parser/source and TLS client were added there, with a unit test guarding the
  registration.
- The user's persisted `Secret Tel Aviv profile` was repaired to use adapter `SecretTelAviv` and
  a `secretTelAvivFilter`. `JobKarov-Software` was restored to adapter `JobKarov` after an earlier
  manual config edit accidentally changed it.

Do not make more exploratory Secret Tel Aviv requests unless the user explicitly asks. Work from
the saved diagnostics and test fixtures.

## Known Product and UI Debt

- The `Latest Source Status` UI now puts source/state on the first row and the full message below
  with wrapping. It still needs user validation on a real multi-source run.
- Friendly ID-to-name hints exist for the known JobKarov, Drushim, and AllJobs values. They are
  not yet named multi-select controls or complete site catalogs.
- JobSwipe.co and Glassdoor remain URL-profile based. Secret Tel Aviv is also search-URL based,
  with a detail-page limit.
- A configuration change does not clear snapshots automatically. The UI warns about comparison
  history; do not delete snapshots without an explicit user request.

## Architecture Guardrails

- Keep source adapters behind `IJobSource`.
- Keep collection, comparison, snapshots, output JSON, and classification independent from each
  source adapter.
- Do not add packages, browser automation, proxies, a database, a web API, or a scheduler without
  explicit user approval.
- Tests are offline only. Use trimmed fixtures in `tests/JobWatcher.Tests/Fixtures/`.
- A live site response belongs in `data/diagnostics/` before any further parsing work.
- After three failed fixes for the same problem, stop and report according to `AGENTS.md`.

## Verification Commands

The Windows environment may fail or stall when using the shared compiler server. Use:

```powershell
dotnet test tests/JobWatcher.Tests/JobWatcher.Tests.csproj --no-restore -m:1 /p:UseSharedCompilation=false
dotnet build JobWatcher.sln --no-restore -m:1 /p:UseSharedCompilation=false
```

Do not use a full `dotnet run` as a single-source diagnostic.

## Last Verified Baseline

- Full solution build succeeded with 0 warnings and 0 errors.
- Offline unit tests passed: 147 tests.
- The latest code changes include shared HTTP request retry handling, partial failed source output,
  explicit cancelled source output, JobKarov role/query/location controls, and README documentation
  for partial runs.
