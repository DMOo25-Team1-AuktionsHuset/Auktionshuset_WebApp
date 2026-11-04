# Plan

## Identity consolidation milestone (2026-11-03)
- [x] Route employee login reads/writes through an application handler and Identity-backed store; keep SignalR handlers unchanged.
- [x] Use ASP.NET Identity with EF Core/AspNetUsers for customers, employees and provisioned configuration accounts; preserve existing public IDs and session invalidation.
- [x] Add a data-preserving migration and focused account/password/ownership/restart regression coverage.
- [ ] Execute the upgrade test against isolated PostgreSQL before deploying the migration; Docker is unavailable locally.

## Device contract milestone
- [x] Record the agreed assignment, shared lot, SignalR gateway, versioned message, ordering and exact-money contracts in ARCHITECTURE.md; they are target requirements, not deployed features.
- [x] Implement and test isolated device encoder calculations without changing web bidding rules.
- [ ] Later milestone: implement persistent assignment/registration, auctioneer selection, authoritative revisions, real SignalR/.NET-client gateway, device WebSocket and end-to-end tests; resolve credential provisioning and concrete protocol error codes.

## Customer and staff access milestone (2026-10-29)
- [x] Show live auctions first, with deterministic start-time ordering and empty states.
- [x] Add own-customer unregistration, serialized with bidding; confirmation and feedback in both overviews.
- [x] Connect customer pages to authorized SignalR invalidations; reconnect and refresh without price polling.
- [x] Reproduce purchase/invoice failures; independent bounded loads, retry, retained data and route-navigation coverage.
- [x] Persist employee-owned logins, administrator-only management, session invalidation and last-admin protection; generate migration.
- [x] Run focused and integration tests plus available browser journeys; document remaining acceptance limits.

## Outcome
- Complete persisted won-lot invoicing, manual payment and collection with historical values, customer isolation and retry safety. No commit or external payment/delivery.

## Completed milestone
- [x] Read instructions/status and verify the existing transactional winner/stock implementation and tests; inspect the legacy invoice model and employee identity.
- [x] Confirm business rules: 10% premium on whole-line hammer; both include 25% VAT; one invoice per customer/completed auction; Card/Cash manual full payment; all staff roles; collection and payment receipts per line after full payment; no invented deadline.
- [x] Persist consolidated invoice snapshots, atomic never-reset counters, approved number format, database uniqueness and startup/five-second recovery. Preserve legacy invoices and reject automatic reissuance where legacy rows exist.
- [x] Implement customer invoice/receipt/totals and historical purchase views, staff payment/collection with actor/time audit, validated transitions and repeat safety.
- [x] Verify amounts, ownership, staff rights, duplicate/concurrent/restart/rollback handling, rendered UI and previous-schema migration upgrade. Update HUMAN status, architecture, README and BILLING.md.

## Separate acceptance/deployment
- [ ] Apply migration to the intended project database and reconcile any legacy invoices/stock conflicts there.
- [x] Customer/staff browser acceptance with isolated PostgreSQL, RabbitMQ and SignalR is recorded in docs/customer-staff-acceptance.md. Exact elapsed-time guarantees and production deployment remain outside this acceptance.

## Current repair milestone
- [x] Reproduce upcoming-auction update failure with the real EF repositories and a temporary SQLite database.
- [x] Reuse tracked Employee instances in single-employee lookups; verify keeping, replacing, removing and assigning an auctionarius (55 focused tests passed).
- [ ] Manual browser acceptance after restarting the API with the fix.
