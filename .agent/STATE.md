# State

## Goal
- Explain the additional Markdown files and their relationship to repository state synchronization. Preserve application changes; no commit or push.

## Done (verified)
- Live-first/start-time ordering, confirmed own-customer unregistration serialized with bidding, and customer SignalR invalidations/reconnect replace price polling. Two-customer browser journeys verified bids, status, start, close and missed-update recovery through real RabbitMQ/PostgreSQL.
- Reproduced and fixed stale shared-component navigation between My purchases/My auctions using explicit route wrappers. Purchases/invoices load independently with bounded requests, retry and retained data; browser verified empty/won purchases, invoice/receipt access and recovery after database failure.
- Employee-owned logins, four existing staff roles, admin-only management, session/refresh version checks, active hub disconnection, email collision checks and last-admin concurrency guard implemented. Migration 20261029131752_AddEmployeeLogins preserves existing active customer accounts; production database not migrated.

## Active
- Inspected Git status, the agent contract, hook configuration and state_sync.py. Additional untracked documentation includes .agent/AUCTION-STOCK.md, .agent/BILLING.md and docs/customer-staff-acceptance.md; Git status does not establish their author or creation sequence.

## Blockers
- No blocker to explaining the synchronization behavior. The inspected files do not establish which agent created the extra documents or when.

## Next
- If documentation creation should be restricted, define that explicitly in AGENTS.md; the current contract does not prohibit additional Markdown files. No such rule change has been made.
- Deployment and intended-database migrations remain outside this investigation; retain existing stock/billing reconciliation guidance.

## Verification
- API suite: 166 passed, 0 failed/skipped with isolated PostgreSQL; frontend/application: 226 passed; infrastructure: 11 passed. Total 403. PostgreSQL coverage includes concurrent bid/unregistration, concurrent admin demotions and upgrade of an existing customer login.
- Browser: two customers plus Admin/Employee/LotAdmin/AuctionAdmin; signup/unregister/rejoin, overbid rejection, automatic bid/status/start/close/end, forced SignalR loss/recovery, navigation, purchase error/retry retention, invoices/receipts, staff account creation/role/deactivation and denied access verified. See acceptance document for exact coverage.
- Tests not run for this documentation investigation; the counts above describe documented earlier acceptance, not fresh validation. Inspection confirms sync() replaces only AUTO-SNAPSHOT content in an existing STATE.md; the hook also uses a lock and diagnostic log. No application changes, commit or push performed.

<!-- AUTO-SNAPSHOT:BEGIN -->
## Repository signal (automatic; NOT proof of completion)
- Branch / HEAD: `main` / `028aaa5`
- Working tree: 171 changed path(s) outside agent configuration.
- Paths (max 10; status + path):
  - ` M Auktionshuset.Api.Test/Admins/Auctions/AuctionEndpointsTest.cs`
  - ` M Auktionshuset.Api.Test/Admins/Employees/EmployeeEndpointsTest.cs`
  - ` M Auktionshuset.Api.Test/Admins/Lots/LotEndpointsTest.cs`
  - ` M Auktionshuset.Api.Test/Auktionshuset.Api.Test.csproj`
  - ` M Auktionshuset.Api.Test/Security/AuthRateLimitingTests.cs`
  - ` M Auktionshuset.Api.Test/Security/EndpointAuthorizationPipelineTest.cs`
  - ` M Auktionshuset.Api/Endpoints/Admin/Auction/AuctionEndpointMapping.cs`
  - ` M Auktionshuset.Api/Endpoints/Admin/Auction/CloseAuction/CloseAuctionLotEndpoint.cs`
  - ` M Auktionshuset.Api/Endpoints/Admin/Auction/GetAuctions/GetAuctionsEndpoint.cs`
  - ` M Auktionshuset.Api/Endpoints/Admin/Bid/BidEndpoints.cs`
  - … and 161 more.
<!-- AUTO-SNAPSHOT:END -->
