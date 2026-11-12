# State

## Goal
- Gennemfør en afgrænset STRIDE-sikkerhedsanalyse af device 9001 fra registrering til bud, afklaring og genudlevering på grundlag af den aktuelle kode og fysiske accept.

## Done (verified)
- Device 9001 beholdt ID/NVS, én backendpost og credentialversion 2; credentialkontrol og genplugning bestod. Firmwarestack-crash blev rettet, upload hashverificeret, og et 110 kr.-bud bevarede kvittering efter fuld strømcyklus uden dublet.
- Første lot blev lukket, andet testlot/200 kr. valgt og ny kunde udleveret uden arvet bud. Tabt svar efter accepteret 220 kr.-bud og fuld strømcyklus blev afklaret som samme request uden dublet. På separat USB-strøm uden stationskabel viste skærmen fortsat `Synchronized`/lot 2/220 kr.
- Første kodebaserede STRIDE-model med fem prioriterede scenarier, kontroller, negative testforslag og rest-risici er dokumenteret i `docs/security-stride-device-2026-11-12.md`.

## Active
- Sikkerhedsanalysens første dokumentmilepæl er færdig; ingen angrebstests eller accepttjenester er startet i denne session.

## Blockers
- Forkert Wi-Fi er ikke fysisk prøvet; testens SSID er ikke et dedikeret isoleret access point. En løs GPIO7-ledning gav et uplanlagt 200 kr.-testbud, så fast montage kræver kontrol.
- Normal API-migration og produktionens TLS/backend er ikke prøvet på en kørende applikationsdatabase.
- Stations- og firmwarekilden findes ikke i denne checkout; lokal Origin/Host/DPAPI-adfærd og flash-/NVS-beskyttelse er derfor ikke kodeverificeret her.

## Next
- Kør isolerede negative budtests for ændret request-payload, gammel session og samtidighed samt direkte rolletests for registrering, rotation og manuel afklaring.
- Gennemgå stations-/firmwarekilden og prøv USB-udskiftning, TLS-afvisning, fysisk secretudlæsning og forkert Wi-Fi på dedikeret testudstyr efter fastmontage af GPIO7.
- Verificér EF-migration og TLS mod det planlagte backendmiljø før produktion.

## Verification
- Tidligere fysisk accept: 110 kr.-bud, lot-/kundeskift og `Unknown`→`Accepted` efter tabt 220 kr.-kvittering/strømbrud uden dublet; separat strøm viste fortsat synkronisering. Tidligere firmwarehosttests og 16 fokuserede WebSocket/gatewaytests bestod.
- Denne session: relevante endpoint-, grant-, policy- og testkilder gennemgået; `git diff --check` bestod. Nye sikkerhedstests: ikke kørt.

<!-- AUTO-SNAPSHOT:BEGIN -->
## Repository signal (automatic; NOT proof of completion)
- Branch / HEAD: `Feature/Embedded-Device2.0` / `4ffbeaa`
- Working tree: 0 changed path(s) outside agent configuration.
- No application-file changes detected by Git.
<!-- AUTO-SNAPSHOT:END -->
