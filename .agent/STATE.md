# State

## Goal
- Rewrite the AI and backend learning objectives in Danish using the supplied knowledge, skills and competencies structure.

## Done (verified)
- Inspected current bidding transactions/request deduplication, SignalR bid notifications, Outbox processing, EF Core PostgreSQL configuration and Identity DbContext.
- Inspected concurrency and invoice integration-test definitions; prepared objectives covering the original topics plus messaging, real-time communication and testing.

## Active
- Deliver the rewritten learning objectives and suggested additions in the response.

## Blockers
- No blocker for the learning-objective rewrite.

## Next
- Select the learning objectives to include in the portfolio and connect each to concrete evidence.

## Verification
- Read-only inspection of application code and test definitions; tests not run for this writing task.
- No application code changed.
<!-- AUTO-SNAPSHOT:BEGIN -->
## Repository signal (automatic; NOT proof of completion)
- Branch / HEAD: `Feature/Embedded-Device2.0` / `cf29778`
- Working tree: 23 changed path(s) outside agent configuration.
- Paths (max 10; status + path):
  - ` M Auktionshuset.Api.Test/Admins/Auctions/CustomerConcurrencyPostgresTests.cs`
  - ` M Auktionshuset.Api.Test/Security/CustomerRegistrationTests.cs`
  - ` M Auktionshuset.Api.Test/Security/DeviceAuthenticationEndpointTests.cs`
  - ` M Auktionshuset.Api.Test/Security/EmployeeLoginTests.cs`
  - ` M Auktionshuset.Api/Endpoints/Admin/Employee/EmployeeLoginEndpoints.cs`
  - ` M Auktionshuset.Api/Endpoints/Auth/AuthEndpoints.cs`
  - ` M Auktionshuset.Api/Security/IAuthUserStore.cs`
  - ` M Auktionshuset.Api/Security/PersistentAuthUserStore.cs`
  - ` M Auktionshuset.Api/Security/SecurityServiceExtensions.cs`
  - ` M Auktionshuset.Api/Services/CustomerRegistrationService.cs`
  - … and 13 more.
<!-- AUTO-SNAPSHOT:END -->

