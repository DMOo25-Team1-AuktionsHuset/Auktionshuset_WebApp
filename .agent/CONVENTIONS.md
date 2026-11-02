# Conventions

## C# / .NET
- Match existing naming, nullable and formatting rules before introducing new ones.
- Prefer clear services/interfaces; use async I/O and propagate `CancellationToken` when practical.
- Do not add abstractions or packages without demonstrated need.

## API, persistence and security
- Validate input at boundaries; enforce authorization server-side.
- Do not expose EF entities directly as public contracts without review.
- Add EF migrations only for intentional schema changes; avoid silent data loss.
- Never log secrets or persist credentials in repository notes.

## Tests and changes
- Prefer focused unit/integration tests matching existing project conventions.
- Verify concurrency, authorization and failure paths when touched.
- Report exact commands and observed results; never claim tests passed if not run.

## Documentation
- Keep STATE factual and short; PLAN prospective; ARCHITECTURE for durable choices.
