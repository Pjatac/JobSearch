# Job Watcher Development Plan

This document is the current handoff and implementation plan. Update it when an architectural
decision, source constraint, or milestone changes. `AGENTS.md` contains the mandatory safety and
live-request rules; it takes precedence over this document.

## Immediate Next Step

1. Establish Git version control and make an initial baseline commit. See `SESSION_HANDOFF.md`
   for the required ignore rules, especially the exclusion of runtime `data/` and inclusion of the
   local `packages/TlsClient.0.6.0-preview.1.nupkg` dependency.
2. Investigate manual-run reliability before expanding source setup UI. The Windows application
   was observed staying on `running` for AllJobs; diagnose the bounded page walk, cancellation,
   request timeout, and progress reporting with the live-request budget in `AGENTS.md`.

## Product Goal

Publish Job Watcher as a useful, auditable job-search utility for a Senior C#/.NET backend
engineer in the Kfar Saba area. It collects listings from several Israeli sources, preserves a
per-source snapshot for change detection, and presents new listings with an explainable fit
classification.

## Current State

- Runtime: .NET 10 console application with JSON files as its durable state.
- Sources: JobKarov, Drushim, AllJobs, JobSwipe.co, Glassdoor, and Secret Tel Aviv.
- State: each successful source run stores its full current snapshot under `data/snapshots/`.
- Delivery: `data/output/new-jobs.json` contains listings new since the prior successful snapshot;
  timestamped output history retains the latest two runs.
- Output deduplication: the user-facing new-listing output is deduplicated across sources by a
  normalized title/company key. Source snapshots deliberately remain independent.
- Glassdoor: search access depends on a manually exported browser session. Do not automate or
  attempt to bypass anti-bot controls. The job detail pages are deliberately not fetched; the
  search result teaser is the available description.

## Classification Contract

Classification is a presentation decision, not collection state:

1. Fetch and validate a complete source result.
2. Diff the unmodified result against the source's prior full snapshot.
3. Persist the unmodified full snapshot.
4. Classify only the newly discovered listings.
5. Deduplicate the delivered listings and emit classification totals per source.

Every delivered listing has:

```json
{
  "classification": "relevant | review | excluded",
  "reasons": ["include-signal:c#"],
  "flags": { "farCommute": false, "cyber": true }
}
```

`excluded` listings remain in `newJobs` so false negatives can be audited. `review` is the safe
outcome for incomplete or ambiguous evidence. Classification never affects snapshot identities,
diffing, or cross-source duplicate detection.

All tunable words, phrases, regex-like experience patterns, flags, and description scan length
are in `JobWatcher:Classification` in `appsettings.json`. Matching is case-insensitive and uses
letter/digit boundaries, so an English token does not match inside another word while Hebrew
phrases remain supported.

## Delivery Phases

### Phase 1: Classification

- Replace the destructive title exclusion path with a pure classifier.
- Add bilingual include, role/discipline, seniority, language, commute, and cyber rules.
- Keep existing date safeguards independent of classification.
- Add bucket totals to each successful source output.
- Cover the core contract with local unit tests.

### Phase 2: Cross-Platform Application

- Build a .NET MAUI client for Windows first, with an iOS target in the same application. The UI
  hosts the existing collection and classification workflow; it does not replace the source
  adapters, snapshots, comparison logic, or JSON output contract.
- On first launch, prefill source and classification forms from the existing configuration. The
  user can clear, remove, add, or edit every persisted setting and then save it as their own
  configuration.
- Provide a source setup screen for JobKarov, Drushim, AllJobs, JobSwipe.co, and Glassdoor. It
  must expose every search parameter currently known and supported for each individual source;
  source-specific fields must never be presented as shared URL parameters.
- Provide global relevance rules as editable form fields: include signals, role and discipline
  exclusions, seniority rules, language exclusions, commute/cyber rules, and description scan
  settings. The form writes the existing `JobWatcher:Classification` configuration contract.
- Keep Glassdoor session input separate from ordinary configuration. It supports replace and
  clear actions, never redisplays a saved cookie value, and must not write secrets to logs or
  diagnostics.
- Allow manual runs only. Show per-source progress, a completed/error status, and preserve the
  existing live-request and anti-bot stop rules.
- Present the generated `new-jobs.json` in Relevant, Review, and Excluded views with source,
  location, cyber, far-commute, and reason filtering. Show source links and matched reasons so
  classification can be audited quickly.
- UI configuration actions may update configuration and the Glassdoor secret, but result browsing
  must not mutate source snapshots or output history.

#### Configuration and Form Design

- Ship the current `appsettings.json` values as a read-only default template. On first launch,
  create one complete, user-writable settings document from that template. Do not layer a partial
  user override over the default JSON: .NET configuration merges arrays by index, which makes
  deletion and reordering of configured sources unreliable.
- Keep user settings, source snapshots, output history, diagnostics, and the Glassdoor session as
  separate storage concerns. Saving changed search or classification configuration preserves prior
  collection history, but the UI must warn that snapshots and results created under different
  criteria are no longer directly comparable. Clearing that history is a separate explicit user
  action, never an automatic side effect.
- Design the source editor around a list of independent search profiles. Each profile has a name,
  enabled switch, optional-source switch, minimum expected vacancy count, maximum vacancy age, and
  exactly one source adapter/filter shape. Multiple profiles for one site are intentional.
- The initial profiles are the existing JobKarov Software/Cyber/InformationSystems, Drushim
  SoftwareRoles, AllJobs BackendDotNet, JobSwipe.co BackendDotNet, and optional Glassdoor
  BackendKfarSaba searches.
- The initial field inventory is:
  - JobKarov: base URL, speciality ID, role IDs, area IDs, company-size value, and direct URL
    override.
  - Drushim: base URL, category ID, one or more subcategory IDs, area IDs, scopes, experience
    range, nearby-area switch, GeoLex ID, experience value, range, and direct URL override.
  - AllJobs: base URL, one or more position IDs, employment-type IDs, source ID, duration, exclude
    phrase, region, maximum pages, and direct URL override.
  - JobSwipe.co: base URL, one or more complete search URLs, and maximum job-detail pages per
    search URL.
  - Glassdoor: base URL, one or more complete search URLs, request delay, maximum pages, and jobs
    per page. Its raw session headers remain in a secret store, outside the profile document.
- Where the repository knows only a site ID, the first UI will render an editable ID chip/list and
  preserve the current value. Friendly option catalogs may be added only from local fixtures,
  diagnostics, or other verified source metadata; never invent labels or issue exploratory live
  requests from the UI.
- Show a read-only generated request URL beneath structured fields for JobKarov, Drushim, and
  AllJobs. It is a verification aid, not a second editable representation of the same profile.

#### Application Structure and Delivery Slices

1. Extract the collector, configuration loading, and runner composition from the console entry
   point into reusable .NET projects without changing collection behaviour. Keep a small console
   host as an optional diagnostic entry point.
2. Add settings and secret stores, first-launch seeding, validation, reset-settings, and import/
   export of non-secret settings. Verify that saving a source list can add, delete, and reorder
   profiles without touching snapshots.
3. Add the MAUI shell and source-profile editor, starting with the current prefilled profiles.
   Implement source-specific forms and URL previews before adding new site filter capabilities.
4. Add the classification editor and Glassdoor session replace/clear flow. Session values are never
   rendered after save, copied into diagnostics, or included in configuration export.
5. Add manual run orchestration, cancellation, and a per-source progress/result screen. It must
   surface anti-bot stops as a terminal result and must not retry or alter browser identity.
6. Add result views over the existing JSON output: Relevant, Review, Excluded, filters, reasons,
   flags, source links, and a clear initial-run explanation.
7. Prove the Windows build first, then validate the iOS target on macOS/Xcode. The current
   Glassdoor HTTP/TLS dependency requires a separate platform-compatibility check before promising
   Glassdoor collection on iOS; the UI and stored results remain useful there even if that source
   must initially run on Windows.

### Phase 3: Scheduled Delivery

- Keep scheduling out of the first client release. A future recurring email digest needs a
  dedicated always-available execution environment and an explicit delivery/security design;
  iOS background execution is not a reliable scheduler for this purpose.
- Decide the hosting, email provider, credential handling, and opt-in cadence before introducing
  a background service, web API, or database.

### Phase 4: Publishability

- Add a short product README section, screenshots, and a reproducible demo data path.
- Decide a public hosting/deployment model separately from the scraper runtime and its browser
  session constraints.
- Only then add repository history and the first commit, as requested by the owner.

## Completion Criteria For Phase 1

- A C#/.NET senior vacancy is `relevant`.
- A Java-only vacancy is `excluded` with `other-language` evidence.
- A junior .NET vacancy is `excluded` with `junior` evidence unless a senior override exists.
- A full-stack vacancy is `excluded` with `role-mismatch` evidence.
- A Glassdoor listing without a meaningful description is never excluded merely because the
  description is missing; it becomes `review` when title-level evidence is insufficient.
- Build and the complete unit suite pass without live site requests.

## Known Constraints

- Source-specific URL parameters must never be copied between sites.
- Do not clear `data/` without a deliberate user request: removing a snapshot makes the next run
  report every current listing as new.
- Live source access has a strict budget and anti-bot stop rule; use local fixtures for parser and
  classification changes.
