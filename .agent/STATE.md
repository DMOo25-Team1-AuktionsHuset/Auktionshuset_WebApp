# State

## Goal
- Deliver a complete ER diagram of the current entities with relationships, annotations and cardinalities, as Mermaid code and graphics.
- Rewrite the AI and backend learning objectives in Danish using the supplied knowledge, skills and competencies structure.
- Gennemfør en afgrænset STRIDE-sikkerhedsanalyse af device 9001 fra registrering til bud, afklaring og genudlevering på grundlag af den aktuelle kode og fysiske accept.

## Done (verified)
- Inspected all domain entities, infrastructure entities and EF configurations. Exported the current Npgsql design model without connecting to a database: 28 mapped entities, 224 columns, 33 foreign keys; HasPendingModelChanges() is false.
- Generated docs/er-diagram/er-diagram.mmd, sectional Mermaid, complete SVG/PNG, and README with all columns, indexes, delete behaviors and owner check constraint. Unmapped Outbox is explicitly separate; Identity and legacy Invoice are included.
- Mermaid 11.12.1 parsing confirms 29 entity blocks, 229 properties (including unmapped Outbox) and 33 relations. Physical cardinalities include unique FK indexes; portable SVG labels and rendered section graphics were visually checked. Download follow-up delivered a native downloadable ZIP containing only complete Mermaid/SVG/PNG and catalogue; archived Mermaid exactly matches the validated repository source (29 entities, 33 relations).
- Inspected current bidding transactions/request deduplication, SignalR bid notifications, Outbox processing, EF Core PostgreSQL configuration and Identity DbContext.
- Inspected concurrency and invoice integration-test definitions; prepared objectives covering the original topics plus messaging, real-time communication and testing.
- Device 9001 beholdt ID/NVS, én backendpost og credentialversion 2; credentialkontrol og genplugning bestod. Firmwarestack-crash blev rettet, upload hashverificeret, og et 110 kr.-bud bevarede kvittering efter fuld strømcyklus uden dublet.
- Første lot blev lukket, andet testlot/200 kr. valgt og ny kunde udleveret uden arvet bud. Tabt svar efter accepteret 220 kr.-bud og fuld strømcyklus blev afklaret som samme request uden dublet. På separat USB-strøm uden stationskabel viste skærmen fortsat `Synchronized`/lot 2/220 kr.
- Første kodebaserede STRIDE-model med fem prioriterede scenarier, kontroller, negative testforslag og rest-risici er dokumenteret i `docs/security-stride-device-2026-11-12.md`.

## Active
- ER documentation milestone complete locally; no application source, schema, dependencies or database data changed. No commit or push.
- Deliver the rewritten learning objectives and suggested additions in the response.
- Sikkerhedsanalysens første dokumentmilepæl er færdig; ingen angrebstests eller accepttjenester er startet i denne session.

## Blockers
- Focused AHDBContextModelTests execution aborted because vstest could not connect to testhost within 90 seconds; tests did not complete. Independent model export, snapshot comparison and Mermaid coverage checks passed.
- No blocker for the learning-objective rewrite.
- Forkert Wi-Fi er ikke fysisk prøvet; testens SSID er ikke et dedikeret isoleret access point. En løs GPIO7-ledning gav et uplanlagt 200 kr.-testbud, så fast montage kræver kontrol.
- Normal API-migration og produktionens TLS/backend er ikke prøvet på en kørende applikationsdatabase.
- Stations- og firmwarekilden findes ikke i denne checkout; lokal Origin/Host/DPAPI-adfærd og flash-/NVS-beskyttelse er derfor ikke kodeverificeret her.

## Next
- Use docs/er-diagram/er-diagram.mmd for editing/import; SVG supports lossless zoom and README contains the full field/index/constraint catalogue.
- Select the learning objectives to include in the portfolio and connect each to concrete evidence.
- Kør isolerede negative budtests for ændret request-payload, gammel session og samtidighed samt direkte rolletests for registrering, rotation og manuel afklaring.
- Gennemgå stations-/firmwarekilden og prøv USB-udskiftning, TLS-afvisning, fysisk secretudlæsning og forkert Wi-Fi på dedikeret testudstyr efter fastmontage af GPIO7.
- Verificér EF-migration og TLS mod det planlagte backendmiljø før produktion.

## Verification
- Model exporter and referenced Domain/Application/Infrastructure projects built with 0 warnings and 0 errors using dotnet build --no-restore -m:1 -p:UseSharedCompilation=false. Model instantiation/export succeeded without a database connection.
- Complete Mermaid parsed and coverage checked against EF metadata; graphic labels converted from foreignObject to native SVG text so they survive raster export. Focused test command attempted: dotnet test Auktionshuset.Infrastructure.Test/Auktionshuset.Infrastructure.Test.csproj --no-restore --nologo --filter FullyQualifiedName~AHDBContextModelTests -m:1 -p:UseSharedCompilation=false; aborted on testhost timeout.
- git diff --check passed. No live database schema/deployment verification or full test suite run for this documentation-only change.
- Read-only inspection of application code and test definitions; tests not run for this writing task.
- No application code changed.
- Tidligere fysisk accept: 110 kr.-bud, lot-/kundeskift og `Unknown`→`Accepted` efter tabt 220 kr.-kvittering/strømbrud uden dublet; separat strøm viste fortsat synkronisering. Tidligere firmwarehosttests og 16 fokuserede WebSocket/gatewaytests bestod.
- Denne session: relevante endpoint-, grant-, policy- og testkilder gennemgået; `git diff --check` bestod. Nye sikkerhedstests: ikke kørt.

<!-- AUTO-SNAPSHOT:BEGIN -->
## Repository signal (automatic; NOT proof of completion)
- Branch / HEAD: `Fixes/iso-ecma` / `88cf466`
- Working tree: 53 changed path(s) outside agent configuration.
- Branch / HEAD: `Feature/Embedded-Device2.0` / `cf29778`
- Working tree: 23 changed path(s) outside agent configuration.
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

