# CLAUDE.md

Agent rules for this repository are in [AGENTS.md](AGENTS.md). Read that file before making
changes; it is mandatory and has priority.

Short version:

- Speak with the user in Ukrainian. Code and documentation are English.
- Stop after three failed fix attempts on the same problem and report options.
- Use saved fixtures/diagnostics for parser work; tests must not hit live sites.
- Live job-site requests have a strict budget and must be saved under `data/diagnostics/`.
- Stop immediately on anti-bot/challenge responses. Do not rotate headers, TLS profiles, cookies,
  proxies, or browser automation without explicit approval.
- Do not add packages, browser automation, databases, web APIs, schedulers, or proxies without
  approval.
- The repo is under git. Do not reset/revert user work.
- Runtime data and secrets live under `data/` or the MAUI app data directory. Never print or commit
  `data/secrets/glassdoor-session.txt`.
