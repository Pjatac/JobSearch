# Job Watcher Session Handoff

This file is the operational starting point for the next development session. Read it after
`AGENTS.md`, `README.md`, and `DEVELOPMENT_PLAN.md`.

## First Task: Establish Version Control

This repository currently has no Git history. Do this before changing application behavior:

1. Inspect the current tree and create a conservative `.gitignore`.
2. Ignore build output and local runtime state: `**/bin/`, `**/obj/`, `.vs/`, and `data/`.
3. Do not ignore `packages/TlsClient.0.6.0-preview.1.nupkg`; it is the locally built dependency
   required for restore on macOS.
4. Run `git init`, inspect `git status`, and verify that no `data/` files or local secrets are
   staged.
5. Stage the source, tests, solution, local NuGet package, and documentation deliberately.
6. Make one initial commit that represents the current working baseline.

Do not rewrite, delete, or regenerate any existing data while doing this.

## Immediate Engineering Priority

Investigate manual-run reliability before adding more UI or sources. The user observed a Windows
run apparently stuck while `AllJobs` was still `running`. The source runner executes work
sequentially, and an AllJobs profile can walk multiple pages, so the next session must determine
whether this is a slow request, a timeout/cancellation propagation bug, or an unbounded page walk.

Use this order:

1. Read the current app output and diagnostics in the MAUI AppData folder.
2. Inspect the AllJobs source and its page limit using local code and existing diagnostics first.
3. If a live request is needed, ask for or use the one-request budget from `AGENTS.md`, save the
   response immediately, and do not launch the full application to debug one source.
4. Improve progress/error reporting only after identifying the cause. A visible elapsed time and
   a clear per-source terminal error are more useful than an indefinite `running` label.

## Current Functional State

- Runtime targets: .NET 10 console/CLI plus a .NET MAUI application for Windows and MacCatalyst.
- Sources: JobKarov, Drushim, AllJobs, JobSwipe.co, Glassdoor, and Secret Tel Aviv.
- User-owned settings are separate from defaults and live under MAUI AppData. Do not assume that
  editing `src/JobWatcher/appsettings.json` changes an existing user's profiles.
- The current Windows MAUI data root is:
  `C:\Users\pjata\AppData\Local\User Name\com.jobwatcher.app\Data`
- The Glassdoor session under that root is a secret. Do not read, print, copy, or commit it.

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

- Full solution build succeeded.
- Offline unit tests passed: 123 tests.
- The latest code changes include Secret Tel Aviv registration in
  `JobWatcherServiceCollectionExtensions` and a readable source-status layout in `MainPage.xaml`.
