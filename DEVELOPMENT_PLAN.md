# Job Watcher Development Plan

This document tracks current product direction and architecture decisions. `AGENTS.md` contains
mandatory safety and live-request rules and takes precedence over this file.

## Product Goal

Job Watcher is an auditable job-search utility for backend/.NET job hunting around central Israel.
It collects listings from several sources, keeps durable per-source snapshots for change detection,
and presents newly discovered listings with explainable classification.

## Current Architecture

- .NET 10 collector core in `src/JobWatcher`.
- Console/CLI host in `src/JobWatcher.Cli`.
- .NET MAUI app in `src/JobWatcher.App` for Windows and MacCatalyst.
- Source adapters implement `IJobSource`; comparison, snapshots, output writing, duplicate review,
  classification, and run controls stay source-independent.
- Runtime state is JSON on disk: snapshots, output, diagnostics, history, latest-per-source output,
  and secrets.

## Completed Capabilities

- Sources: JobKarov, Drushim, AllJobs, JobSwipe.co, Secret Tel Aviv, DevJobs, and optional
  Glassdoor.
- Classification: `relevant` / `review` / `excluded` with reasons and flags.
- Output deduplication across sources by normalized title/company, title/description, and URL.
- Duplicate diagnostic reports for full snapshots and delivered `newJobs`.
- Partial failed source output for transient failures that still collected useful listings.
- Shared HTTP timeout retry policy: one retry after a fixed 5-second delay for transient
  timeout/internal cancellation only.
- Manual run logs with 24-hour retention.
- MAUI settings seeded from defaults on first launch, separate from defaults thereafter.
- Profile editors for structured JobKarov, Drushim, AllJobs, DevJobs, and URL-based sources.
- Results view with `Latest run` / `Latest per source`, filters, numbering, detail copy, clear
  history, and paged rendering.
- Run controls in the app: `Run`, checkpoint-based `Pause/Resume`, and `Stop`.
- Windows public-repo workflow with branch protection and PR review.

## Current Source Constraints

- **Glassdoor** depends on a manually exported browser session. It is optional and expected to fail
  when the session expires. Do not automate or bypass anti-bot challenges without explicit user
  approval.
- **Drushim** collection uses public search pages and embedded Next.js payloads. Do not switch it
  to `/api/jobs/search`; live checks returned a Next.js 404 page there.
- **AllJobs** is fetched one position at a time and then deduplicated. This avoids the site's
  one-position search behavior.
- **JobKarov** requirements are merged from the search response; per-vacancy detail pages were
  measured and did not add useful content.

## Classification Contract

Classification is presentation state, not collection state:

1. Fetch and validate a source result.
2. Diff the unmodified result against the prior full snapshot.
3. Persist the unmodified full snapshot only after a complete success.
4. Classify only newly discovered listings.
5. Deduplicate delivered listings and emit source classification totals.

`excluded` listings remain in output for audit. `review` is the safe outcome for incomplete or
ambiguous evidence.

## Near-Term Work

1. Validate `Run / Pause / Stop` from the MAUI app on Windows and MacCatalyst release builds.
2. Run one final all-profile collection with fresh Glassdoor session if the user wants it.
3. Prepare public-facing screenshots and a LinkedIn post.
4. Decide release packaging:
   - Windows: unpackaged self-contained folder first; signed MSIX later if needed.
   - Mac: MacCatalyst Release build for local validation; public distribution needs signing/notarization decisions.
5. Delete stale remote branches after merged PRs.

## Future Ideas

- Better demo/sample output for README screenshots without exposing secrets or personal search data.
- More friendly option catalogs for remaining URL-based sources, only from verified local data.
- Optional export/import of non-secret settings.
- A signed installer and update story.
- Scheduling or digests only after a separate hosting/security design. Do not add an internal
  scheduler casually.

## Non-Goals Without Explicit Approval

- Browser automation, Playwright, Selenium.
- CAPTCHA or anti-bot bypass.
- Proxies.
- Database or Entity Framework.
- Web API or background service.
- New NuGet packages.
- Automatic job applications or CV submission.

## Verification

Use:

```powershell
dotnet build JobWatcher.sln --no-restore -m:1 /p:UseSharedCompilation=false
dotnet test JobWatcher.sln --no-restore -m:1 /p:UseSharedCompilation=false
```

Tests must remain offline. Parser changes should use trimmed fixtures under
`tests/JobWatcher.Tests/Fixtures/`.
