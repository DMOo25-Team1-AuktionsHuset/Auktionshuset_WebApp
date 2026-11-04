# State

## Goal
- Consolidate customer/employee logins in ASP.NET Core Identity and EF Core AspNetUsers, with employee login operations delegated to an application handler.

## Done (verified)
- EmployeeLoginEndpoints delegates reads/writes to EmployeeLoginHandler; IdentityEmployeeLoginStore preserves validation, last-admin protection, refresh/session revocation and connection disconnection.
- Customer and employee account creation use UserManager<LoginUser>; tests verify a shared AspNetUsers table/email namespace, password changes, session invalidation and configured-account persistence across restart/deactivation.
- Added UnifyIdentityUsers migration, upgrade coverage, and documentation; generated SQL and EF model consistency checks succeed. Updated two outdated device test doubles to match the existing repository contract.

## Active
- Implementation and available regression checks are complete; PostgreSQL migration execution/deployment is pending.

## Blockers
- Docker engine is unavailable. The PostgreSQL upgrade test and other PostgreSQL/external WebSocket integration tests were skipped.

## Next
- Run IdentityMigrationPreservesCustomerAndEmployeePasswordsIdsAndStatus against isolated PostgreSQL before applying UnifyIdentityUsers to the intended database.
- Migration requires reconciliation of duplicate/incomplete existing Identity accounts, and a backup for downgrade; no database migration or commit was performed here.

## Verification
- API: 218 passed, 48 skipped, 0 failed; Infrastructure: 45 passed, 0 failed; Frontend: 243 passed, 0 failed (rerun with access to its local Data Protection key store).
- Focused customer/employee account tests: 55 passed. EF reports no pending model changes; upgrade SQL generation succeeded. PostgreSQL upgrade execution: not run.

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
- Branch / HEAD: `Feature/Embedded-Device2.0` / `f7e31ea`
- Working tree: 0 changed path(s) outside agent configuration.
- No application-file changes detected by Git.
<!-- AUTO-SNAPSHOT:END -->
