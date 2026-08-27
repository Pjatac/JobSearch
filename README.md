# Job Watcher

Job Watcher has a .NET 10 collection core, a console host, and a .NET MAUI application shell. The
collector checks configured job-search sources, stores a complete snapshot per source, and writes
a JSON output containing only newly discovered vacancies.

The first source adapter is `JobKarov`; `Drushim`, `AllJobs`, `JobSwipe.co`, and `Glassdoor` are also supported. Each source has its own URL builder and filter taxonomy because the numeric parameters are site-specific.

## Architecture

- `Sources/IJobSource.cs` defines the adapter boundary.
- `Sources/JobKarov` fetches JobKarov with `HttpClient` and extracts vacancies from JSON-LD.
- `Sources/AllJobs` fetches AllJobs with cookie-aware `HttpClient` pagination and extracts vacancies from server-rendered HTML.
- `Sources/JobSwipeCo` fetches JobSwipe.co search pages, extracts job detail URLs from JSON-LD ItemList, and extracts vacancies from detail-page JobPosting JSON-LD.
- `Sources/Glassdoor` fetches Glassdoor search pages and extracts vacancies from server-rendered result cards, with JSON-LD `ItemList` as a fallback.
- `Http/TlsClientMessageHandler.cs` adapts `HttpClient` onto a browser-fingerprinted `TlsSession`.
- `Services/JobComparisonService.cs` compares complete snapshots by `Source + ExternalId`.
- `Services/OutputDuplicateService.cs` reviews the delivered `newJobs` list for repeated postings.
- `Persistence/JsonSnapshotStore.cs` stores one durable JSON snapshot per source.
- `Utilities/AtomicFileWriter.cs` writes JSON through a temporary file before replacing the destination.

JobKarov uses `HttpClient` instead of browser automation because the vacancies are present in the normal HTML response as JSON-LD structured data. Playwright/Selenium are not needed for this source.

Known JobKarov limitation: in live checks, different `speciality` values with the same `role` and `area` set returned identical vacancy IDs, and expanding the `role` list did not behave like a simple superset of the previous result.

JobKarov splits a posting into a `description` and a separate `require` field, and publishes only the former as JSON-LD. The requirements hold the experience level and the technology stack — the part title and description filtering actually needs — so they are read from the `window.__BASE_SITES__` array in the same search response and appended as `Requirements: …`. This costs no extra requests. Measured effect on one live search: average description length rose from roughly 900 to 1676 characters across 55 vacancies.

Fetching each vacancy's own page was measured and deliberately **not** implemented: the per-vacancy page returns byte-identical field lengths. JobKarov caps `description` and `require` at 999 characters each everywhere, so the text is truncated at source and per-vacancy requests would add load without adding content.

`JobWatcher:Classification` controls an explainable `relevant` / `review` / `excluded` decision
for each newly discovered listing. It does not remove listings from source snapshots or from the
output: excluded listings remain available for auditing. See [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)
for the durable data-flow contract and the UI roadmap.

## Source Filters

The current search is aimed at Backend/.NET/C# roles in Center + Hasharon. It is not senior-only: middle and mid+ roles are intended to stay in the result set. Senior-specific filters are used only as additional broad catchers where a site exposes them.

### JobKarov

JobKarov parameters are independent from Drushim parameters.

- default categories: software `2119`, cybersecurity `3921`, and information systems `1857`
- Backend role: `3893`
- .NET role: `2163`
- C# role: `2155`
- Software Engineer role: `3131`
- Senior Programmer role: `2177` (additional catcher, not the primary focus)
- optional search text is sent as `query`, for example `C# .Net`
- areas use one JobKarov region/city catalog; Hasharon region is `50`, Center region is `70`,
  Netanya area is `53`, and Raanana/Kfar Saba is `56`
- page size: `2`

Configured JobKarov URL shape:

```text
https://www.jobkarov.com/Search/?speciality=<category>&role=3893,2163,2155,3131,2177&area=50,70&size=2
```

JobKarov can also run a keyword search without a selected category:

```text
https://www.jobkarov.com/Search/?query=C%23+.Net&area=53&size=2
```

### Drushim

Drushim structured filters use the site's JSON API pagination behind the "show more" button. Explicit Drushim URLs still fall back to server-rendered HTML parsing for diagnostics. Browser automation is not required. Drushim category/subcategory/area IDs are not interchangeable with JobKarov IDs.

- software category: `cat6`
- Backend: `subcat/616`
- `.NET`: `subcat/69`
- Programmer: `subcat/183`
- `C#`: `subcat/372`
- Software Engineer: `subcat/380`
- High-tech general: `subcat/209` (not enabled by default; broad and noisy)
- Center + Hasharon area IDs: `1-2-3-4-5-6-7-8-9-10-11-12-13-14`
- full-time scope: `scope=1`
- experience focus: `experience=2-3-4` and `ssaen=3` (middle/mid+/senior-compatible, but not senior-only)
- active combined role URL: `https://www.drushim.co.il/jobs/subcat/69-183-372-380-616/area/1-2-3-4-5-6-7-8-9-10-11-12-13-14/?catdir=6&scope=1&experience=2-3-4&geolexid=539071&isaa=true&ssaen=3&range=3`

### AllJobs

AllJobs uses normal page-number pagination in `SearchResultsGuest.aspx?page=N`. A cookie-aware HTTP client is used because direct page 2 requests can hit a Radware interstitial without the first-page session.

- Backend Programmer: `position=1759`
- Backend Engineer: `position=1994`
- `.NET`: `position=1152`
- `C#`: `position=1203`
- Senior Backend Developer: `position=1848` (additional catcher, not the primary focus)
- Center region: `region=2`
- Hasharon region: `region=6`
- full-time type: `type=4`
- recency parameter: `duration=25`

Configured AllJobs URL shape:

```text
https://www.alljobs.co.il/SearchResultsGuest.aspx?page=<page>&position=1759,1994,1152,1203,1848&type=4&source=&duration=25&exc=&region=2,6
```

### JobSwipe.co

JobSwipe.co search pages expose the first result set as JSON-LD `ItemList`; individual job pages expose `JobPosting` JSON-LD. The adapter is configured with explicit search URLs because the site's SEO route encodes the search phrase in `SRCH_...` tokens rather than simple query parameters.

- Backend Developer search: `https://jobswipe.co/jobs/Israel-backend-developer-דרושים-SRCH_L0_Q7,17`
- Software development + Raanana/Kfar Saba search: `https://jobswipe.co/jobs/Israel-דרושים-פיתוח-תוכנה-רעננה-כפר-סבא-דרושים-SRCH_L0_Q7,38`
- max detail pages per search: `30`

The general title exclusion rules still remove noisy jobs such as Full Stack, QA, DevOps, Lead, Architect, BI, Embedded, and similar non-target roles.

### Glassdoor

Glassdoor search URLs are SEO routes rather than clean query strings. The location/job tokens are Glassdoor-specific and must not be reused with JobKarov, Drushim, or AllJobs.

- Kfar Saba location token from the browser URL: `IC4507116`
- Backend Developer keyword range in the route: `KO10,27`
- active URL: `https://www.glassdoor.com/Job/kfar-saba-backend-developer-jobs-SRCH_IL.0,9_IC4507116_KO10,27.htm`

#### Pagination

Results come from Glassdoor's search API, the endpoint behind the "Show more jobs" button:

```text
POST https://www.glassdoor.com/job-search-next/bff/jobSearchResultsQuery
```

It takes a plain JSON body and needs no CSRF token. Each response carries `paginationCursors` for
the *other* pages, so the walk follows the cursor for the next page and ends by itself when none is
offered. `MaxPages` is only a safety ceiling. A live walk of the configured search returned
**245 unique vacancies over 9 pages** out of 252 advertised, against 30 from the first page alone.

URL-based pagination does not exist here: the SEO `_IP<N>` suffix, `?p=2` and `?pageNumber=2` were
each tested against the live site and all return page one. The button is a client-side control with
no URL of its own, so the API is the only way to page.

`GlassdoorSearchUrl` decodes the search parameters Glassdoor encodes into the SEO route
(`IC4507116` → city id, `KO10,27` → the keyword's character range inside the slug), so a source
stays configured by a single URL rather than a restated set of API parameters.

The server-rendered HTML parser is kept as a fallback for when the API call fails, since it still
yields the first 30 results.

The API also carries an exact `ageInDays`, so `DatePosted` is a real date. The rendered cards only
expose a bucketed label (`data-test="job-age"`, for example `3d`, `24h`, `30d+`); on the HTML
fallback path `30d+` maps to the 30-day lower bound, keeping `MaximumVacancyAgeDays` filtering
conservative rather than optimistic.

#### Glassdoor access

Glassdoor rejects the default .NET TLS fingerprint. It is the only source wired to a
browser-fingerprinted primary handler (`TlsClientMessageHandler` over the `TlsClient` package,
Chrome 133 preset); every other source keeps the plain `HttpClientHandler`. The selection is made
by client name in `Program.cs` — the adapter itself only resolves its named `HttpClient`.

Two things the preset does not cover, both handled explicitly:

- **Headers.** `TlsPresets` are TLS and HTTP/2 presets. A bare session sends `Accept: */*` and no
  client-hint or fetch-metadata headers — generic-HTTP-client headers behind a Chrome handshake.
  `Http/BrowserSessionOptions.cs` supplies a Chrome 133 document-navigation header set and header
  order to match. It is a fixed set derived from the emulated browser, not a knob to tune: header
  rotation as a way past a block is out of scope.
- **Handler lifetime.** `HttpClientFactory` rotates primary handlers every two minutes by default.
  The handler owns the TLS session, and with it the cookie jar, TLS 1.3 tickets and pooled HTTP/2
  connection, so rotation would make each new handler look like a brand-new visitor mid-run. The
  Glassdoor client uses `SetHandlerLifetime(Timeout.InfiniteTimeSpan)`; the process is short-lived.

Requests are paced by `glassdoorFilter.requestDelaySeconds` (default `1`). This is a delay between
distinct requests only — a blocked request is never retried.

Search URLs are configured as bare SEO paths. Glassdoor's own search form appends redundant query
parameters (`locId`, `locT`, `sc.keyword`) that duplicate values already encoded in the path, and
its `sc.keyword` value is double-encoded (`%2520`). They are deliberately not copied into the
configuration.

A direct `TlsSession` request returned `200` with 30 parsed cards. The same URL through
`HttpClient` + `TlsClientMessageHandler` later returned `403` with Cloudflare's
`Security | Glassdoor` interstitial. A local HTTPS listener was used to compare both code paths
without contacting Glassdoor: both sent the same HTTP/2 protocol and the same header set and
values, so the handler itself was not the cause. (The listener exposes headers through a
dictionary, so it cannot confirm wire order; order is identical by construction, both paths using
the same session options.) Both were, however, sending the generic header set described above —
that gap is now closed.

Access was then measured: three samples through the fixed wiring, two minutes apart, each from a
cold session — **0 of 3 succeeded**, all with the same challenge page. Only the very first request
ever made from this machine returned `200`; every request after it has been blocked, while the
same URL keeps working in a signed-in browser.

As a control, the exact code path that produced that first `200` — a bare `TlsSession`, no browser
headers, no handler — was re-run unchanged and also returned `403`. The block therefore lives in
Cloudflare's per-IP/fingerprint state, not in this code, and creating a fresh session does not
reset it: the state being matched is not held in this process. Running total: 1 success out of 8
requests, with all seven failures consecutive after the first.

The difference from a browser is state and JavaScript, not request shape, so imitating a browser
more closely is not a way through.

#### Exported browser session

What does work is reusing a real browser's session instead of imitating one. With an exported
session the same request returns `200` with 30 parsed cards.

If `data/secrets/glassdoor-session.txt` exists, its cookies, User-Agent and Accept-Language are
applied to the Glassdoor client at startup; without the file, requests stay anonymous and are
blocked. `data/secrets/glassdoor-session.example.txt` documents the export steps.

- The file holds the **raw request-header block** copied from DevTools → Network → the document
  request → Request Headers → **view source**. It is pasted whole: comments, unrecognised lines and
  HTTP/2 pseudo-headers are ignored. Hand-extracting the `Cookie` value is the step most likely to
  go wrong, because DevTools truncates it in the display.
- `document.cookie` in the console is not enough: `cf_clearance` and `__cf_bm` are `HttpOnly`.
- The User-Agent must come from the same browser as the cookies. Cloudflare binds `cf_clearance`
  to it and rejects the cookie if they disagree, so the exported value overrides the preset's, and
  `sec-ch-ua` is derived from it so the client hint cannot contradict it either.
- Cookies whose values `CookieContainer` rejects (`g_state` carries raw JSON) are skipped with a
  warning rather than being allowed to fail startup.
- **The file is a live session — treat it as a password.** It is never logged; only cookie names
  appear in log output. Do not commit it if this repository is ever put under version control.
- `cf_clearance` and `__cf_bm` are short-lived (`__cf_bm` lasts 30 minutes), so the export has to
  be repeated when Glassdoor starts failing again. This is a manual, best-effort path, not an
  unattended one — which is why the source stays `"Optional": true`.

Because access is unreliable, the source is marked `"Optional": true`: it still runs and still
reports its failure, but its failure does not make the process exit non-zero.
`GlassdoorChallengeDetector` recognises the interstitial and reports it as an access failure so a
block is never mistaken for "the parser found nothing". The adapter never retries a challenge, and
never rotates headers or fingerprints to get around one.

## Configuration

`src/JobWatcher/appsettings.json` is the shipped default template. `name` is the unique
snapshot/output name; `adapter` selects the scraper implementation; `optional` (default `false`)
keeps a source's failure out of the exit code. Sources can either provide a complete `url` or the
structured filter for their adapter (`jobKarovFilter`, `drushimFilter`, `allJobsFilter`,
`jobSwipeCoFilter`, or `glassdoorFilter`). The MAUI app seeds a separate, full user settings file
from this template on first launch; it does not use a partial JSON overlay.

`classification` is entirely configuration-driven. It contains the include signals, primary
languages, role mismatches, junior/senior patterns, location flags, cyber flags, and the maximum
description prefix inspected by the classifier. Changes take effect on the next run and do not
rewrite historical snapshots. Each source output has `classificationSummary`, while every member
of `newJobs` carries its own `classification`, `reasons`, and `flags`.

In the MAUI app, the Results page has two optional quick filters over already collected jobs:
`Cyber / security` keeps only jobs whose classification has the special-interest flag, and
`Long commute` keeps only jobs whose classification has the far-commute flag. They do not alter
the requests sent to job sites. Their labels and signals are configured through the Classification
page (`specialInterestLabel`, `cyberSignals`, and `farCommuteLocations`).

```json
{
  "JobWatcher": {
    "DataDirectory": "data",
    "RequestTimeoutSeconds": 30,
    "Sources": [
      {
        "Name": "JobKarov-Backend",
        "Adapter": "JobKarov",
        "Enabled": true,
        "JobKarovFilter": {
          "Query": "",
          "Specialities": [ "2119", "3921", "1857" ],
          "Speciality": "",
          "Roles": [ "3893", "2163", "2155", "3131", "2177" ],
          "Areas": [ "50", "70" ],
          "Size": 2
        },
        "MinimumExpectedVacancies": 1
      }
    ]
  }
}
```

## Run

```powershell
dotnet run --project src/JobWatcher.Cli
```

After a successful first run:

```text
data/snapshots/jobkarov.json
data/output/new-jobs.json
data/output/new-jobs-duplicates.json
data/output/duplicate-candidates.json
data/output/history/<timestamp>.json
```

## First Run And Later Runs

On the first successful run there is no previous snapshot, so all current vacancies are emitted as `newJobs` and the source output has `"isInitialRun": true`.

On later runs, the app emits only vacancies present in the current complete snapshot and absent from the previous successful snapshot.

When several configured sources return the same vacancy, `new-jobs.json` shows it only once using a normalized title + company output key, with URL/identity fallback. Source snapshots remain separate so filter behavior can still be inspected.

`data/output/duplicate-candidates.json` is a diagnostic report for likely duplicates across different sites, such as JobKarov vs Drushim. It does not affect snapshots, diffs, or `newJobs`; it only surfaces pairs worth reviewing.

`data/output/new-jobs-duplicates.json` reviews the delivered `newJobs` list itself. The two reports answer different questions and neither replaces the other:

| | `duplicate-candidates.json` | `new-jobs-duplicates.json` |
|---|---|---|
| Input | full snapshots | the final `newJobs` list |
| Pairs | different sites only | any pair, including same-site |
| Matching | weighted fuzzy score | exact normalized keys |

Grouping uses union-find, so a chain of matches reports as one group rather than several pairs. Reasons are `same-url`, `same-title-and-company`, and `same-title-and-description` — the last catching an agency relisting one posting under a different company name.

An identical description on its own never groups jobs as duplicates. Employers reuse a boilerplate opening paragraph across unrelated postings, and Glassdoor's search API returns a short shared teaser rather than the posting text, so equal descriptions are reported separately under `sharedDescriptionGroups` as context — they explain why unrelated entries can look alike without implying the list repeats itself.

URL comparison keeps the query string and strips only unambiguous click trackers (`utm_*`, `gclid`, `fbclid`, `msclkid`). The query commonly carries the listing id itself — AllJobs uses `UploadSingle.aspx?JobID=…` — so discarding it would collapse every listing on such a site into a single false group.

To reset local state for a JobKarov source, delete its snapshot:

```text
data/snapshots/jobkarov-software.json
```

## Failure Safeguards

For JobKarov, parsing zero vacancies or fewer than `MinimumExpectedVacancies` is a failure. Failed runs do not replace the previous valid snapshot. Raw HTML from failed fetch/parse runs is written to `data/diagnostics/`.

After a successful run, snapshot retention keeps snapshots for every enabled configured source, including sources that failed in that run. Only snapshots for sources no longer present in the configuration are deleted. A failed source keeps its previous snapshot: deleting it would make the next successful run report every vacancy as new, which matters for sources that fail intermittently.

Output history retention keeps only the latest `OutputHistoryRetentionCount` timestamped files in `data/output/history/` after a successful history write. The default is `2`.

A large count drop logs and records a warning when the previous count is at least 10 and the current count is below 50% of the previous count.

## Exit Codes

- `0`: all enabled required sources succeeded. Failures of sources marked `"Optional": true` are reported in the output but do not change this.
- `1`: at least one enabled required source failed and at least one enabled source succeeded.
- `2`: no enabled source succeeded at all, no enabled sources exist, or application-level initialization fails.

A source entry with `"Optional": true` still runs, still reports `"status": "failed"` with its error, and is marked `"optional": true` in `new-jobs.json`. It is used for sources whose access is unreliable, so a blocked Glassdoor does not mark an otherwise healthy run as failed.

## Test

```powershell
dotnet test JobWatcher.sln -m:1
```

Parser tests use local HTML fixtures and do not call the live website.

## Open Questions

- Glassdoor works only with a manually exported browser session, and that session expires within
  roughly half an hour. Unattended scheduled runs will therefore find it blocked most of the time
  unless the export is refreshed. Making it unattended would mean browser automation for this one
  source, which has not been agreed.
- Glassdoor descriptions are the search API's `descriptionFragmentsText` teaser, averaging about
  150 characters, not the posting text. Fetching each vacancy's own page was considered and
  **deliberately not implemented**: it would cost roughly one request per vacancy against a source
  that blocks us and whose session lasts half an hour, and the `/job-listing/…htm` route answered
  `403` even while the search API was working. The teaser plus the diff — which surfaces only a
  handful of genuinely new vacancies per run — is enough to review by hand. Revisit only with a
  captured job-detail API request, and enrich new vacancies only.
- The exposed result window churns: two captures 40 minutes apart shared only 18 of 30 listings.
  Repeated runs therefore keep surfacing listings a single run would miss, which softens the impact
  of a blocked run but also means counts are not stable between runs.
- Glassdoor "Show more jobs" pagination is not automated; only the first 30 of ~251 results are captured.

## Agent Instructions

Automated agents working in this repository must follow [AGENTS.md](AGENTS.md): a three-failed-attempts
stop rule, a hard live-request budget, and a "stop immediately on anti-bot challenge" rule.
[CLAUDE.md](CLAUDE.md) points at the same file.

## Add Another Source

Add a new class implementing `IJobSource`, register it in `Program.cs`, and add a matching source entry in `appsettings.json`. Comparison, snapshot persistence, output writing, and exit-code handling are source-independent.
