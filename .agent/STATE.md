# State

## Goal
- Deliver a complete ER diagram of the current entities with relationships, annotations and cardinalities, as Mermaid code and graphics.

## Done (verified)
- Inspected all domain entities, infrastructure entities and EF configurations. Exported the current Npgsql design model without connecting to a database: 28 mapped entities, 224 columns, 33 foreign keys; HasPendingModelChanges() is false.
- Generated docs/er-diagram/er-diagram.mmd, sectional Mermaid, complete SVG/PNG, and README with all columns, indexes, delete behaviors and owner check constraint. Unmapped Outbox is explicitly separate; Identity and legacy Invoice are included.
- Mermaid 11.12.1 parsing confirms 29 entity blocks, 229 properties (including unmapped Outbox) and 33 relations. Physical cardinalities include unique FK indexes; portable SVG labels and rendered section graphics were visually checked. Download follow-up delivered a native downloadable ZIP containing only complete Mermaid/SVG/PNG and catalogue; archived Mermaid exactly matches the validated repository source (29 entities, 33 relations).

## Active
- ER documentation milestone complete locally; no application source, schema, dependencies or database data changed. No commit or push.

## Blockers
- Focused AHDBContextModelTests execution aborted because vstest could not connect to testhost within 90 seconds; tests did not complete. Independent model export, snapshot comparison and Mermaid coverage checks passed.

## Next
- Use docs/er-diagram/er-diagram.mmd for editing/import; SVG supports lossless zoom and README contains the full field/index/constraint catalogue.

## Verification
- Model exporter and referenced Domain/Application/Infrastructure projects built with 0 warnings and 0 errors using dotnet build --no-restore -m:1 -p:UseSharedCompilation=false. Model instantiation/export succeeded without a database connection.
- Complete Mermaid parsed and coverage checked against EF metadata; graphic labels converted from foreignObject to native SVG text so they survive raster export. Focused test command attempted: dotnet test Auktionshuset.Infrastructure.Test/Auktionshuset.Infrastructure.Test.csproj --no-restore --nologo --filter FullyQualifiedName~AHDBContextModelTests -m:1 -p:UseSharedCompilation=false; aborted on testhost timeout.
- git diff --check passed. No live database schema/deployment verification or full test suite run for this documentation-only change.

<!-- AUTO-SNAPSHOT:BEGIN -->
## Repository signal (automatic; NOT proof of completion)
- Branch / HEAD: `Fixes/iso-ecma` / `88cf466`
- Working tree: 53 changed path(s) outside agent configuration.
- Paths (max 10; status + path):
  - ` M Auktionshuset.Api.Test/Admins/Auctions/BrowserAcceptanceHost.cs`
  - ` M Auktionshuset.Api.Test/Admins/Auctions/MessagingStatusPostgresTests.cs`
  - ` M Auktionshuset.Api.Test/Admins/Lots/CreateLotTest.cs`
  - ` M Auktionshuset.Api.Test/Security/ConfiguredAuthUserStoreTests.cs`
  - ` M Auktionshuset.Api.Test/Security/EndpointAuthorizationPipelineTest.cs`
  - ` M Auktionshuset.Api/Endpoints/Admin/Auction/GetAuctions/GetAuctionsEndpoint.cs`
  - ` M Auktionshuset.Api/Program.cs`
  - ` M Auktionshuset.Api/Security/ConfiguredAuthUserStore.cs`
  - ` M Auktionshuset.Api/Security/SecurityRolePermissions.cs`
  - ` M Auktionshuset.Application/Admin/Auctions/GetAuctions/GetAuctionsHandler.cs`
  - … and 43 more.
<!-- AUTO-SNAPSHOT:END -->
