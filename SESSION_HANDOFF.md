# Job Watcher Session Handoff

This file is the operational starting point for the next development session. Read it after
`AGENTS.md` and `README.md`.

## Immediate Engineering Priority

Validate the `Run / Pause / Stop` branch after the current PR is merged. `Pause` is
checkpoint-based: it waits before the next source/page/detail request and does not interrupt an
HTTP request already in flight. `Stop` cancels the run.

After that, the product is mostly in polish/release mode: verify a final all-profile run, prepare
screenshots for the public post, and keep README/distribution notes aligned with the current app.

## Current Functional State

- Runtime targets: .NET 10 collector core, console/CLI host, and .NET MAUI app for Windows and
  MacCatalyst.
- Sources: JobKarov, Drushim, AllJobs, JobSwipe.co, Secret Tel Aviv, DevJobs, and optional
  Glassdoor.
- User-owned MAUI settings live under AppData/Library container and are seeded from
  `src/JobWatcher/appsettings.json` only on first launch. Editing defaults does not change an
  existing user's settings.
- Windows MAUI data root observed during development:
  `C:\Users\pjata\AppData\Local\User Name\com.jobwatcher.app\Data`
- MacCatalyst data lives under the app container. To find logs:

```bash
find "$HOME/Library/Containers" "$HOME/Library/Application Support" \
  -path "*manual-runs/run-*.log" -print 2>/dev/null | sort | tail -5
```

- `data/secrets/glassdoor-session.txt` is a secret browser-session export. Do not read, print,
  copy, or commit it. Logs may show cookie names only.

## Collection and Output Behavior

- Source HTTP requests share one retry policy: one retry after a transient timeout/internal
  cancellation, after a fixed 5-second delay, with the same URL, body, headers, cookies, and client
  identity. HTTP responses and anti-bot challenges are not retried.
- Sources may return `partial_failed` after preserving collected listings from a transient failure.
  Partial new jobs can be written to `new-jobs.json`, but partial snapshots do not replace the last
  durable source snapshot.
- Manual run logs under `data/diagnostics/manual-runs/` are pruned when a new run log is created.
  Logs older than 24 hours are deleted; cleanup failures are ignored.
- Output history keeps the latest configured history entries. Each non-paused source also writes
  `data/output/latest-sources/<source>.json`.
- Results view supports `Latest run` and `Latest per source`, classification filters, source/search
  filters, numbered cards, detail copy, and readable plain-text descriptions.
- `Clear history` deletes snapshots and output while keeping settings, diagnostics, and secrets.

## Source Notes

- **JobKarov**: structured profile selectors cover categories, roles grouped by specialization,
  areas, and free query text. Requirements are merged from the search response without detail
  requests.
- **Drushim**: structured profiles support query, category IDs, subcategories, locations, scopes,
  experience, and nearby-area/range fields. The source uses public search pages and embedded
  Next.js data; do not route collection through `/api/jobs/search`.
- **AllJobs**: positions, employment types, and regions have selectors. The position ID text field
  stays synchronized with selected positions and preserves custom IDs. The source fetches one
  position per request and deduplicates the merged result set.
- **JobSwipe.co**: URL-profile based. Search pages provide detail URLs; detail pages provide
  `JobPosting` JSON-LD.
- **Secret Tel Aviv**: URL-profile based with a detail-page limit. Uses the browser TLS client.
- **DevJobs**: structured filters cover developer types, districts, cities, query text, paging, and
  bounded details. Partial detail failures preserve output without replacing snapshots.
- **Glassdoor**: optional source. Requires a manually exported browser session. Search uses the
  Glassdoor search API and detail enrichment uses the job-details API. Session expiry is expected;
  do not automate or bypass anti-bot controls without explicit user approval.

## UI and Product State

- Source profile editing is usable for JobKarov, Drushim, AllJobs, DevJobs, Secret Tel Aviv,
  JobSwipe.co, and Glassdoor.
- Advanced/opaque Drushim fields are hidden under an advanced section by default.
- Results rendering is paged with `Load more` to avoid heavy first render on MacCatalyst.
- The main page avoids nesting `CollectionView` inside `ScrollView`.
- For Mac testing, prefer a Release MacCatalyst build on Intel:

```bash
dotnet build src/JobWatcher.App/JobWatcher.App.csproj -c Release -f net10.0-maccatalyst -r maccatalyst-x64
open "src/JobWatcher.App/bin/Release/net10.0-maccatalyst/maccatalyst-x64/Job Watcher.app"
```

## Branch and PR Notes

- Main is protected with review required.
- Use PRs for feature branches. After merge, delete stale branches such as old `test/*` branches.
- Current feature branch at the time of this handoff: `feature/run-pause-stop`.

## Architecture Guardrails

- Keep source adapters behind `IJobSource`.
- Keep collection, comparison, snapshots, output JSON, classification, and run controls independent
  from each source adapter.
- Do not add packages, browser automation, proxies, a database, a web API, or a scheduler without
  explicit user approval.
- Tests are offline only. Use trimmed fixtures in `tests/JobWatcher.Tests/Fixtures/`.
- A live site response belongs in `data/diagnostics/` before parser work continues.
- After three failed fixes for the same problem, stop and report according to `AGENTS.md`.

## Verification Commands

```powershell
dotnet build JobWatcher.sln --no-restore -m:1 /p:UseSharedCompilation=false
dotnet test JobWatcher.sln --no-restore -m:1 /p:UseSharedCompilation=false
```

Do not use a full `dotnet run` as a single-source diagnostic.

## Last Verified Baseline

- Full solution build succeeded with 0 warnings and 0 errors after merging `origin/main` into
  `feature/run-pause-stop`.
- Offline unit tests passed: 170 tests.
- Latest verified branch: `feature/run-pause-stop` at commit `61a27e7`.
