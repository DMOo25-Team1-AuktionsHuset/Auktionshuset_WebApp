# Architecture

## Status
- **Target direction, not verified implementation.** Inspect the repository before asserting components exist.

## Intended components
- UI: Blazor; API/services: ASP.NET Core / C#.
- Persistence: EF Core with PostgreSQL (verify actual provider).
- Messaging: RabbitMQ pub/sub; client updates: SignalR.
- Identity: ASP.NET Core Identity / authorization (verify configuration).

## Boundaries
- Keep domain rules outside UI and transport handlers.
- Keep database and messaging adapters behind explicit service boundaries.
- Treat external messages as at-least-once unless confirmed otherwise.

## Decisions to verify
- Bid concurrency strategy and database transaction boundaries.
- Message reliability (outbox/idempotency/retries) and SignalR event ownership.
- Actual solution/project structure and integration-test setup.

## Accepted decisions
- Customer SignalR uses invalidation-only messages through a Customer-policy hub with an active persisted profile check. Reads remain customer-scoped HTTP calls; reconnect refetches state. My-auctions/purchases route wrappers pass an explicit mode to their shared component.
- LoginUser links exactly one Customer or Employee, enforced by role/owner check and unique nullable FKs. Staff login mutations serialize on advisory lock 704291032 and protect the last active admin, including configured admins. JWTs and refresh tokens carry session versions; role/status/password changes invalidate old sessions. Employee deletion is restricted while a login exists.
- Billing uses AuctionInvoice headers and AuctionInvoiceLine snapshots, with unique source/customer-auction links, transactional persisted counters and PostgreSQL locking shared with auction stock writes. The legacy single-line Invoice table remains intact and blocks duplicate automatic issuance until reconciled.
- Invoice discovery is a persisted database scan on startup/every five seconds, independent of RabbitMQ. Staff payment and per-line collection preserve authenticated login UserId/email and UTC timestamps; customer reads filter using the persisted customer-login link. See BILLING.md for the confirmed business rules and verification limits.
