# Agent contract

## Startup
- Use the `STATE.md` snapshot injected by the session hook; otherwise read `.agent/STATE.md` once.
- Read `.agent/PLAN.md` only for multi-step work. Read architecture/conventions only for relevant decisions.
- Repository code, tests, and git diff outrank all notes. Never assume a planned feature exists.

## Execution
- Work on one bounded milestone; avoid speculative rewrites and multi-agent review loops.
- Search narrowly (`rg`); respect `.ignore`. Do not crawl `bin/`, `obj/`, temp or secrets.
- Make the smallest change that meets the task; run focused tests first.
- Never run destructive commands, change dependencies, or commit without an explicit reason.

## Checkpoint (no user copy/paste)
- After a meaningful milestone or before finishing, update the HUMAN sections of `.agent/STATE.md`:
  Goal, Done (verified), Active, Blockers, Next, Verification.
- Record only facts confirmed in code/tests; say "not run" if not tested.
- Keep each section to 1–3 bullets. Replace stale facts, never append a chat log.
- Update `PLAN.md` only if scope/sequence changes; `ARCHITECTURE.md` only for durable decisions.
- Never edit the `AUTO-SNAPSHOT` block: hooks manage it without model calls.
- Do not start a new agent turn merely to update state. Stop normally, even if hooks fail.
