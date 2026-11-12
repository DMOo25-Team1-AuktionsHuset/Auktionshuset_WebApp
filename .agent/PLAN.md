# Plan

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

## ECMA-334 assessment milestone (2026-11-04)
- [x] Confirm branch `Fixes/iso-ecma`, all own C# including tests, and ECMA-only scope; clarify that newer C# must be preserved.
- [x] Read the attached seventh edition, inventory source, inspect relevant normative clauses and code, run normal build/tests and supplementary syntax/NFC/version checks without source changes.
- [x] Deliver the evidence-based analysis and instructions for another AI in `docs/ecma334-analyse.md` and `docs/ecma334-ai-instruktioner.md`; no documented normative repair task identified within scope.
- [x] ECMA follow-up found no additional normative repair; source remained unchanged in that milestone.

## SonarQube screenshot repair milestone (2026-11-04)
- [x] Verify and fix the screenshot findings: cancellation/disposal, startup/authorization, logging, collection types, regexes and test data; generated Up/Down SQL is byte-identical for all seven edited migrations.
- [x] Add eight response-cancellation regression cases; focused tests (26 API, 104 frontend/domain), solution rebuild (0 warnings/errors), selected analyzer verification and full tests (389 passed, 23 skipped) passed. Live SonarQube rescan and opt-in acceptance remain unverified.
- [x] Deliver consolidated Danish change report in `docs/sonarqube-aendringsrapport.md`, checked against the current diff and complete 50-file source inventory; report retains test/scan limitations.

## Complete ER documentation milestone (2026-11-09)
- [x] Inspect all domain/infrastructure entities and Fluent API; export the current Npgsql design model without opening a database connection and confirm it matches the migration snapshot.
- [x] Document all 28 mapped tables, 224 columns and 33 foreign keys, including Identity, legacy Invoice, and the separately marked unmapped Outbox class; include physical cardinalities, indexes, nullability, precision and constraints.
- [x] Generate complete Mermaid plus readable sectional Mermaid, SVG and PNG graphics; validate model-to-Mermaid coverage and visually inspect the rendered diagrams. Focused model tests were attempted but aborted on testhost connection timeout; do not count them as passing.
