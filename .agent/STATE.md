# State

## Goal
- Rewrite the AI and backend learning objectives in Danish using the supplied knowledge, skills and competencies structure.
- Gennemfør en afgrænset STRIDE-sikkerhedsanalyse af device 9001 fra registrering til bud, afklaring og genudlevering på grundlag af den aktuelle kode og fysiske accept.

## Done (verified)
- Inspected current bidding transactions/request deduplication, SignalR bid notifications, Outbox processing, EF Core PostgreSQL configuration and Identity DbContext.
- Inspected concurrency and invoice integration-test definitions; prepared objectives covering the original topics plus messaging, real-time communication and testing.
- Device 9001 beholdt ID/NVS, én backendpost og credentialversion 2; credentialkontrol og genplugning bestod. Firmwarestack-crash blev rettet, upload hashverificeret, og et 110 kr.-bud bevarede kvittering efter fuld strømcyklus uden dublet.
- Første lot blev lukket, andet testlot/200 kr. valgt og ny kunde udleveret uden arvet bud. Tabt svar efter accepteret 220 kr.-bud og fuld strømcyklus blev afklaret som samme request uden dublet. På separat USB-strøm uden stationskabel viste skærmen fortsat `Synchronized`/lot 2/220 kr.
- Første kodebaserede STRIDE-model med fem prioriterede scenarier, kontroller, negative testforslag og rest-risici er dokumenteret i `docs/security-stride-device-2026-11-12.md`.

## Active
- Deliver the rewritten learning objectives and suggested additions in the response.
- Sikkerhedsanalysens første dokumentmilepæl er færdig; ingen angrebstests eller accepttjenester er startet i denne session.

## Blockers
- No blocker for the learning-objective rewrite.
- Forkert Wi-Fi er ikke fysisk prøvet; testens SSID er ikke et dedikeret isoleret access point. En løs GPIO7-ledning gav et uplanlagt 200 kr.-testbud, så fast montage kræver kontrol.
- Normal API-migration og produktionens TLS/backend er ikke prøvet på en kørende applikationsdatabase.
- Stations- og firmwarekilden findes ikke i denne checkout; lokal Origin/Host/DPAPI-adfærd og flash-/NVS-beskyttelse er derfor ikke kodeverificeret her.

## Next
- Select the learning objectives to include in the portfolio and connect each to concrete evidence.
- Kør isolerede negative budtests for ændret request-payload, gammel session og samtidighed samt direkte rolletests for registrering, rotation og manuel afklaring.
- Gennemgå stations-/firmwarekilden og prøv USB-udskiftning, TLS-afvisning, fysisk secretudlæsning og forkert Wi-Fi på dedikeret testudstyr efter fastmontage af GPIO7.
- Verificér EF-migration og TLS mod det planlagte backendmiljø før produktion.

## Verification
- Read-only inspection of application code and test definitions; tests not run for this writing task.
- No application code changed.
- Tidligere fysisk accept: 110 kr.-bud, lot-/kundeskift og `Unknown`→`Accepted` efter tabt 220 kr.-kvittering/strømbrud uden dublet; separat strøm viste fortsat synkronisering. Tidligere firmwarehosttests og 16 fokuserede WebSocket/gatewaytests bestod.
- Denne session: relevante endpoint-, grant-, policy- og testkilder gennemgået; `git diff --check` bestod. Nye sikkerhedstests: ikke kørt.

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

