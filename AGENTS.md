# Job Watcher - Agent Rules

This file is mandatory for any AI agent working in this repository. If it conflicts with an
agent's usual habits, this file wins. If a rule blocks the task, stop and ask the user instead of
working around it.

User-facing conversation is Ukrainian. Code, comments, identifiers, README, and product
documentation are English.

---

## 1. Three-Attempt Budget

**Three failed attempts in a row on the same problem means STOP.**

An attempt is one cycle of `change -> run/check -> still broken`.

After the third failed attempt, the agent must:

1. stop making code changes for that problem;
2. report briefly:
   - what still fails, with the exact error/status/symptom;
   - which three approaches were tried and what each produced;
   - 2-3 options for what to do next, with rough cost/risk;
   - what is missing: data, access, or a user decision;
3. wait for the user's answer.

Reading files, searching with `rg`, running tests, or building the solution does not count as an
attempt. The counter is for attempts to fix one concrete problem.

---

## 2. Network Budget

Live HTTP requests to job sites are expensive and should be rare during development.

Rules:

- One live request per hypothesis. Do not loop, retry, or vary headers "just to see".
- Save the response immediately under `data/diagnostics/<source>-<timestamp>.html` or `.json`.
  Continue parser/debug work offline from that saved response.
- Maximum 3 live requests per session without explicit user approval.
- Do not run the full collector just to debug one source. Use a targeted probe or saved fixture.

### Anti-Bot Protection

If a response is `403`, `429`, `503`, a security/challenge page, or a noindex/no useful content
interstitial:

1. stop immediately;
2. save the response under `data/diagnostics/`;
3. report status, page title, byte count, and whether target selectors/payloads exist;
4. wait for the user's decision.

Without explicit approval, do not:

- rotate User-Agent, headers, TLS profiles, cookies, or proxies;
- attempt CAPTCHA/challenge bypasses;
- add Playwright, Selenium, or browser automation;
- repeatedly retry anti-bot responses.

The product has a normal HTTP retry policy for transient timeouts during real runs. That is not a
license for exploratory retry loops while developing or debugging a source.

---

## 3. Before Coding

- Read `README.md` for current behavior and `SESSION_HANDOFF.md` for current operational state.
- `JOB_WATCHER_CODEX_BRIEF.md` is historical context only; do not treat it as current scope.
- Ask before coding if the task is ambiguous.
- Do not add NuGet packages without user approval.
- Keep edits scoped to the requested source/feature.

---

## 4. Parser Work

- Parser development and verification must use local fixtures in `tests/JobWatcher.Tests/Fixtures/`.
- Tests must not call live websites.
- Real HTML/JSON for new fixtures comes from `data/diagnostics/`, created from the single approved
  live request.
- Trim fixtures to the needed nodes. Do not commit full large pages when a small fixture is enough.
- If a parser returns zero vacancies, first check whether the saved response is a challenge,
  redirect, or genuinely empty search result.

---

## 5. Technical Boundaries

Do not introduce without explicit user approval:

- Playwright, Selenium, browser automation;
- a database or Entity Framework;
- a web API, background service, or scheduler;
- proxies or CAPTCHA-solving services;
- new NuGet packages.

Keep:

- .NET 10, nullable enabled, implicit usings;
- `System.Text.Json`, `IHttpClientFactory`, `Microsoft.Extensions.*`;
- HtmlAgilityPack only for HTML navigation/parsing;
- source adapters behind `IJobSource`;
- comparison, snapshots, output writing, classification, and run controls source-independent.

---

## 6. Commands

```powershell
dotnet build JobWatcher.sln --no-restore -m:1 /p:UseSharedCompilation=false
dotnet test JobWatcher.sln --no-restore -m:1 /p:UseSharedCompilation=false
dotnet run --project src/JobWatcher.Cli
```

Before saying "ready": build is clean and tests are green. If tests/build cannot be run, say that
clearly and explain why.

Do not use a full run as a single-source diagnostic: it hits every enabled profile.

---

## 7. Repository and Data State

- The repository is under git. Do not rewrite history, reset, or revert user changes unless the
  user explicitly asks.
- `data/` is runtime state: snapshots, output, diagnostics, and secrets. Do not clean it "for
  tidiness".
- Deleting `data/snapshots/` makes the next successful run treat current vacancies as new.
- The MAUI `Clear history` action deletes snapshots and output only; it keeps settings, diagnostics,
  and Glassdoor session data.
- `data/secrets/glassdoor-session.txt` is a live browser session export. Treat it like a password:
  do not read it unless needed, do not print it, do not copy it into diagnostics, and never commit
  it. Logs may include cookie names only, never values.

---

## 8. Reporting

At the end of a task, report briefly:

- what changed;
- build/test results;
- anything not verified and why;
- the branch/commit/push state when relevant.
