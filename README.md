# UCL Team 1 ~ 4 Semester - Auktionshuset
## Formål
Projektet er udarbejdet som en prototype på et auktionssoftware. Formålet er at udarbejde et system med online budgivning og embedded devices til at styre budgivning. 

---
## Features
- [ ] 
- [ ] 
- [ ] 
- [ ] 
- [ ] Authentication og authorization
- [ ] Tests
- [ ] Docker containerisation

---
## Teknologier
- **C# / .NET 10** - Programmeringssprog og framework.
- **ASP.NET Core Web API** - Backend API til at håndtere requests fra client.
- **Blazor WebAssembly** - Frontend client til user interface.
- **Entity Framework Core** - Object-relational mapper brugt til database adgang.
- **Microsoft SQL Server** - Relational database brugt til at opbevare data.
- **ASP.NET Core Identity** - User og  role management.
- **Role-based Authorization** - Access control ved brug af roller som Admin, Employee og Storskaerm.
- **Swagger / OpenAPI** - API dokumentation og test.
- **Dtos** - Brugt til at transporterer data imellem frontend og backend.
- **EF Core Migrations** - Brugt til at håndtere databaseændringer.
- **Docker / Visual Studio Container Tools** - Container support for applikationen.
- **SignalR** - Teknologi til realtidskommunikation. 

---
## Projektstruktur og arkitektur
Projektet er struktureret efter Clean Architecture. Se evt. mere her: [Clean Architecture with ASP.NET Core 10](https://www.youtube.com/live/rjefnUC9Z90) 
Projektets overordnede arkitektur er Event-Driven. Se evt. mere her: [
Building Next-Generation Applications with EDA • Eric Johnson](https://youtu.be/u2lh7pbgcMM?si=8xekc0nTDh5bsRlk)

```txt

AuktionsHuset

│

├── AuktionsHuset                      # Server-side rendering

├── AuktionsHuset.API                  # Controllers & Middleware

├── AuktionsHuset.Application          # Interfaces # Dto

├── AuktionsHuset.Client               # Client-side rendering & UI

├── AuktionsHuset.Client.Test          # Test class

├── AuktionsHuset.Domain               # Entities 

├── AuktionsHuset.Infrastructure       # EF Core, DtoServices & DBcontext

├── AuktionsHuset.Infrastructure.Test  # Test class             

│

├── Dockerfile

├── docker-compose.yml

└── README.md

```

---
## Installation
### Krav
- .NET SDK 10.0
- Docker Desktop
- Visual Studio 2022 og nyere
- Git
---
## Kør projektet lokalt med Visual Studio

Følg [trin-for-trin-vejledningen til teamets Windows-pc'er](docs/team-setup.md).
Den dækker VPN, certifikat, personlig Vault-adgang, de resterende lokale
indstillinger og opstart af **Auktionshuset.Api** og **Auktionshuset**.

Den projektansvarlige bruger [vejledningen til sikker udlevering af Vault-adgang](docs/vault-admin-onboarding.md).

---
## Kør med Docker

```bash

docker compose up --build

```

Applikationen kører på:

```txt

https://localhost:[5001]

https://localhost:[7201]

```

---
## Database
Ved lokal API-opstart hentes databasebrugernavn og adgangskode fra Vault.
Host, port og databasenavn læses fra `Database`-sektionen i API'ens konfiguration.
API'en kører migrationer og seeding ved opstart mod den konfigurerede database.
### Migrations

```bash

dotnet ef migrations add InitialCreate

dotnet ef database update

```

--- 
## Test
Udvalgte klasser er testet på baggrund af, hvor meget funktionalitet, der minder om hinanden. Vi har altså valgt eksemplariske klasser, der ligner andre klasser. Der er foretaget integrationstest og unittest. 

```bash

***dotnet test .\Slottet\Slottet.Client.Test\Slottet.Client.Test.csproj***
***dotnet test .\Slottet\Slottet.API.Test\Slottet.API.Test.csproj***
***dotnet test .\Slottet\Slottet.Infrastructure.Test\Slottet.Infrastructure.Test.csproj***

```

---
## Login og administratorroller
API'et bruger ASP.NET Core Identity med EF Core og `AspNetUsers` til både kunde- og medarbejderkonti. JWT-roller og permissions afledes af kontoens gemte rolle. Medarbejderlogin administreres gennem `EmployeeLoginHandler`; frontend-login bruger en HttpOnly-cookie og en serveropbevaret backend-token.

Administratorrollerne er:

| Rolle | Lager/genstande | Auktioner | Medarbejdere |
| --- | --- | --- | --- |
| `LotAdmin` | Fuld adgang | Ingen adgang | Ingen adgang |
| `AuctionAdmin` | Fuld adgang | Fuld adgang | Ingen adgang |
| `Admin` | Fuld adgang | Fuld adgang | Fuld adgang |

Den eksisterende `Authentication:BootstrapAdmin`-konfiguration opretter fortsat en `Admin`. Supplerende loginbrugere konfigureres på API-projektet med .NET user-secrets; eksempelværdierne herunder er pladsholdere, ikke credentials:

```powershell
dotnet user-secrets set "Authentication:BootstrapAdmin:Email" "admin@example.test" --project .\Auktionshuset.Api
dotnet user-secrets set "Authentication:BootstrapAdmin:Password" "<vælg-en-adgangskode>" --project .\Auktionshuset.Api
dotnet user-secrets set "Authentication:Users:0:Email" "lager@example.test" --project .\Auktionshuset.Api
dotnet user-secrets set "Authentication:Users:0:Password" "<vælg-en-adgangskode>" --project .\Auktionshuset.Api
dotnet user-secrets set "Authentication:Users:0:Role" "LotAdmin" --project .\Auktionshuset.Api
dotnet user-secrets set "Authentication:Users:1:Email" "auktion@example.test" --project .\Auktionshuset.Api
dotnet user-secrets set "Authentication:Users:1:Password" "<vælg-en-adgangskode>" --project .\Auktionshuset.Api
dotnet user-secrets set "Authentication:Users:1:Role" "AuctionAdmin" --project .\Auktionshuset.Api
dotnet user-secrets set "Authentication:SigningKey" "<tilfældig-hemmelig-nøgle-på-mindst-32-bytes>" --project .\Auktionshuset.Api
```

`Authentication:Users` er en liste med `Email`, `Password` og `Role`. Konfigurationskonti, inklusive bootstrap-admin, oprettes én gang i `AspNetUsers` ved første opslag. Derefter bruges den gemte konto; genstart overskriver ikke rolle, adgangskode eller deaktivering. Ukendte roller, ufuldstændige konfigurationsbrugere og emailadresser med dubletter (uanset store/små bogstaver, også på tværs af bootstrap-admin) afvises ved opstart. Kontooprettelse og adgangskodekontrol bruger Identity `UserManager<LoginUser>`. Rollen og permissions kan ikke vælges eller overskrives i loginanmodningen. Brug user-secrets lokalt eller secret store/miljøvariabler i deployment; læg aldrig credentials eller signing keys i versionsstyrede filer.

Migrationen `UnifyIdentityUsers` flytter eksisterende `LoginUsers` til `AspNetUsers` med samme offentlige bruger-id, adgangskodehash, kunde-/medarbejdertilknytning, rolle og sessionsversion. Dublerede e-mails mellem tabellerne og eksisterende Identity-konti uden e-mail/adgangskode skal afklares før migrationen. Tag backup før opgradering; downgrade kræver gendannelse fra backup. Opgraderingstesten kræver isoleret PostgreSQL via `AUCTION_STOCK_TEST_CONNECTION` og er endnu ikke kørt lokalt.

---
## Kundeoprettelse og kundelogin
Kunder opretter selv en konto via "Opret ny bruger" på `/login`. Registreringssiden `/registrer` sender en valideret anmodning til API'ets `POST /api/auth/register`, som altid tildeler rollen `Customer` på serveren - kontrakten indeholder hverken roller, permissions eller CustomerId. Identity-kontoen i `AspNetUsers` og `Customer`-posten gemmes i samme EF Core-transaktion med unik, normaliseret email, fælles for kunder og medarbejdere.

Efter login vælges destinationen ud fra servervaliderede roller: `Customer` sendes til `/kunde`, administrative roller til det eksisterende administrationsområde, og brugere uden gyldig rolle nægtes adgang. Kunder afvises fra administrationssider og administrative API-endpoints af de eksisterende policies. Både login- og registreringsflowet er rate limited pr. klient-IP.

### Medarbejderlogin
Administratorer kan vælge en medarbejder på `/admin/employees` og oprette login med e-mail og adgangskode, vælge Admin, AuctionAdmin, LotAdmin eller Employee og deaktivere login. `LoginUsers` har en unik medarbejder- eller kundetilknytning; medarbejdere oprettes uden en kundeprofil. E-mailadresser normaliseres, og sammenfald med gemte eller konfigurerede logins afvises. Konfigurerede bootstrap-/administrationslogins administreres fortsat i konfigurationen.

Rolleændring og deaktivering ugyldiggør eksisterende adgangs- og refresh-tokens; medarbejderen skal logge ind igen. Den sidste aktive administrator beskyttes under en fælles databaselås, også ved samtidige ændringer. Et konfigureret administratorlogin tæller som en aktiv administrator. Medarbejdere med login kan ikke slettes; deaktivér deres login i stedet.

Migrationen `20261029131752_AddEmployeeLogins` skal anvendes sammen med de tidligere migrationer ved normal deployment. Eksisterende kundelogin bevares aktive med uændrede passwordhashes. Opgraderingen kræver nyt login for allerede udstedte adgangstokens uden sessionsversion. Se [accepttest og kørselsvejledning](docs/customer-staff-acceptance.md).

### Kundesider
`/kunde` (og `/kunde/auktioner`) viser igangværende auktioner først og derefter kommende auktioner åbne for tilmelding. Begge grupper sorteres stigende efter starttid og har tydelige tomme tilstande. Kunder kan tilmelde sig en auktion og åbne `/kunde/auktioner/{auctionId}` for at se genstande og afgive bud, når administrationen har startet auktionen. Budsiden viser kundens eget højeste bud og det aktuelle højeste bud samt Ingen bud, Du fører og Du er overbudt.

`/kunde/mine-auktioner` bevarer kundens egne tilmeldte auktioner, inklusive afsluttede, med links til genstande og resultater. `/kunde/mine-koeb` viser vundne linjer, antal og hammerslagspris for hele linjen. Kundesiderne opdateres via den autoriserede SignalR-hub `/hubs/customer-auctions`, som kun sender invalideringer. Priser, budstatus, start, lukning og afslutning hentes derefter gennem kundeafgrænsede API-kald. Ved afbrydelse vises forbindelsesstatus, og genforbindelse henter den aktuelle tilstand. Der er ingen normal prisopdateringsknap eller femsekunderspolling. Mine køb henter køb og fakturaer uafhængigt, viser konkrete fejl med mulighed for at prøve igen og bevarer tidligere indlæste data ved opdateringsfejl.

Når budfristen er passeret, men auktionen endnu ikke er lukket, vises Afventer resultat. Vundet, Tabt og Usolgt samt køb kræver linjens gemte `ClosedAtTime`; vinderen og hammerslagsprisen læses fra den gemte `CurrentHighestBid` og beregnes ikke igen i kundevisningen. En individuelt lukket linje kan derfor have et endeligt resultat, mens resten af auktionen stadig afventer lukning. En lukket solgt linje, som kunden ikke har budt på, vises som Solgt.

`GET /api/customer/my-auctions` og `GET /api/customer/purchases` afgrænser resultater efter den indloggede kundes servervaliderede profil. Svarene indeholder hverken andre kunders id, navn, kontaktoplysninger eller budhistorik. Detaljeadgang kræver fortsat egen tilmelding, også efter afslutning.

Manuel prøverute (udførte browserkontroller og resultater er beskrevet i [accepttestnotatet](docs/customer-staff-acceptance.md)):

1. Opret en testauktion med to forskellige genstande, fx en linje med to stole og en uden bud. Log ind som to kunder i hver sin browsersession, og tilmeld begge. Kontrollér Mine auktioner og Ingen bud før første bud.
2. Start auktionen som administrator. Lad kunde A byde 110 kr. og derefter kunde B 140 kr. på stolelinjen. Vent op til næste femsekundersopdatering: A skal se eget bud 110 kr., højeste bud 140 kr. og Du er overbudt; B skal se eget bud 140 kr. og Du fører.
3. På en isoleret testopsætning kan API-processen standses hen over sluttiden og genstartes, eller afslutningsservicen midlertidigt deaktiveres af testværten. Hvis fristen er passeret uden gemt lukning, skal budsiden vise Afventer resultat uden Vundet/Tabt/Usolgt eller køb. Denne mellemtilstand testes automatisk med gemte testdata; den kan være meget kort med den normale afslutningsservice.
4. Afslut auktionen manuelt eller afvent automatisk lukning. Efter opdatering skal A se Tabt, B Vundet og begge Usolgt på linjen uden bud. Kun B skal have stolelinjen under Mine køb med antal 2 og hammerslagspris 140 kr. for hele linjen.
5. Åbn igen via Mine auktioner og Mine køb efter genindlæsning. Resultatlinks skal stadig virke. Kontrollér i netværkssvar, at andre kunders personoplysninger ikke vises; en tredje kunde uden tilmelding skal afvises på den direkte resultatadresse og have tomme egne oversigter.

`/kunde/profil` bevarer redigering af kundens egne oplysninger. Kundens identitet findes fra den validerede session og kontoens kundekobling. `GET/PUT /api/customer/me` tillader kun ændring af fornavn, efternavn, telefon og adresse.

Auktionstilmeldinger gemmes i `AuctionRegistrations` med en unik nøgle for kunde og auktion. Migrationen `AddAuctionRegistrations` opretter tabellen ved næste API-opstart sammen med de øvrige migrationer. Ingen eksisterende kunder tilmeldes automatisk. Kunde-API'et udstiller `GET /api/customer/auctions`, `POST /api/customer/auctions/{auctionId}/registration`, `GET /api/customer/auctions/{auctionId}` og `POST /api/customer/lots/{auctionLotId}/bids`. Alle kræver Customer-rollen. Budsiden og bud kræver kundens egen tilmelding; også den eksisterende `/api/bids/{auctionLotId}` kontrollerer konto og tilmelding. Budlageret kontrollerer online-tilmelding samt de eksisterende regler om pris, tidsrum og åbne genstande, inden buddet gemmes.

---
## Automatisk afslutning af auktioner

API-serverens `AuctionExpirationWorker` søger straks ved opstart og derefter med fem sekunders pause mellem gennemløb efter faktisk startede (`StartedAtTime`), åbne Live-auktioner med `EndedAt <= TimeProvider.GetUtcNow()` i UTC. Overskredne sluttider indhentes efter nedetid. Kommende auktioner startes eller afsluttes ikke automatisk; auktioner uden sluttid afsluttes fortsat manuelt.

Automatisk og manuel afslutning bruger samme transaktion, PostgreSQL-låse og lukningslogik for linjer, vindere, usolgte resultater, lager og outbox. Sluttiden genkontrolleres efter låsning. Flere API-instanser må køre servicen mod samme database. Bud afvises ved selve sluttiden, selv om afslutningsservicen endnu ikke har behandlet auktionen. Der oprettes ikke ekstra resultater, lagertræk eller outbox-hændelser ved gentagen afslutning; beskedleveringen følger fortsat projektets eksisterende outbox/inbox-mekanisme.

Drift: Hold mindst én API-instans kørende og servernes UTC-ure synkroniserede. Afslutningen kan ske senere end fem sekunder ved kø, låse eller databasefejl. Fejl logges med auktions-id, og åbne forfaldne auktioner forsøges igen ved næste gennemløb; fejl ved søgningen logges og forsøges også igen. Vedvarende lager-/datakonflikter kræver afstemning; de må ikke løses ved at omgå lagerkontrollen. Før rollout skal `scripts/audit-auction-stock.sql` køres mod den tiltænkte database, og eksisterende konflikter afstemmes. Funktionen kræver ingen ny migration, scheduler eller pakke.

PostgreSQL-testene i `AuctionStockPostgresTests` kræver `AUCTION_STOCK_TEST_CONNECTION` til en isoleret testserver med rettighed til at oprette/slette testdatabaser. De dækker tidsgrænse, genstart, gentagelse, fejl/rollback/genforsøg og samtidige bud samt manuelle/automatiske lukninger.

`TwoCustomerJourneyPersistsRealBidsManualAndTimedResultsAndProtectsAdminOperations` kører API-forløbet fra oprettelse og tilmelding til bud, manuel lukning, udløb, lager og kundernes resultater med `AuctionLotBiddingStore`, PostgreSQL-låse, budkvitteringer og `EfOutboxWriter`. Testuret styrer tidsgrænsen, og afslutningsservicens `ProcessDueAsync` kaldes direkte; browseradfærd, den løbende femsekundersplanlægning og RabbitMQ-levering indgår ikke i denne automatiske test. Den separate browseraccepttest bruger rigtig RabbitMQ og SignalR. `MessagingStatusReadsBothPipelinesAndFiltersAdminConsumerAsText` kontrollerer tomme og fyldte inbox/outbox-tabeller, køfilter, statusoptællinger og at kønavnet sendes som tekstparameter. Kør med `dotnet test Auktionshuset.Api.Test --no-restore --filter FullyQualifiedName~AuctionStockPostgresTests`. Hver test opretter og sletter sin egen database; brug aldrig en produktionsforbindelse. Testskemaet oprettes med `EnsureCreated`, så migrationsopgradering af eksisterende data er en særskilt kontrol.

## Roadmap

- [ ] Deployment
- [ ] CI/CD pipeline

---
## Læringsmål
Projektet er lavet med følgende læringsmål for øje: 

- ASP.NET Core MVC
- Clean Architecture
- SQL Server
- Entity Framework Core
- Docker
- Lagdeling
- SOLID
- Testbarhed

---

# Faktura, betaling og afhentning

Efter afslutning af hele auktionen danner en worker automatisk én faktura pr. kunde med 10 % salær. Hammerslag og salær er inklusive 25 % moms. Under **Mine køb** kan kunden se samlet beløb, faktura, linjestatus og kvittering pr. betalt auktionslinje. Medarbejdere bruger **Betaling og afhentning** (`/admin/invoices`) til at registrere fuld kort-/kontantbetaling og derefter afhentning pr. linje. Registreringerne gemmer tidspunkt og medarbejderens loginidentitet; gentagne kald bevarer den oprindelige registrering.

Migrationen `20261029123907_AddAuctionInvoices` skal anvendes før brug. Fakturanumre følger fx `AH42-29-10-2026-000001`; auktionsnummer tildeles ved første fakturering, og den fælles fakturatæller nulstilles aldrig. Ingen ekstern betalingsudbyder, betaling eller fakturaafsendelse er tilsluttet. Se [.agent/BILLING.md](.agent/BILLING.md) for regler, historiske værdier, genbehandling, ældre fakturaer og testafgrænsning.

# Agent State Sync — Codex + Claude Code

A minimal, provider-neutral project memory for C#/.NET. No cloud service, extra model, transcript scraping, or prompt-based stop loop.

## One-time installation

1. Copy this kit's contents **into the root of your existing Git repository**, retaining hidden `.claude/`, `.codex/` and `.agent/` folders. **Do not overwrite existing `AGENTS.md`, `CLAUDE.md`, `.gitignore`, `.ignore` or agent settings without comparing and merging them.**
2. Install **Python 3** and ensure `python` is on your PATH. On Windows, Codex hooks use the Windows Python Launcher (`py -3`); install it or change `.codex/hooks.json` to `python`. On Linux/macOS, Codex uses `python3`. Git must also be installed.
3. From your repository root, run `python scripts/state_sync.py doctor` and `python -m unittest discover -s scripts/tests -v` (Windows with launcher: `py -3 ...`). The doctor validates both hook config JSON files and updates the automated Git section once.
4. Start **Claude Code** from the repository root. Its project-local `.claude/settings.json` registers hooks; review the project configuration when requested. Check `/hooks` for effective hooks.
5. Start **Codex** in this Git repository. Review/trust project hooks with **`/hooks`** when prompted. Codex deliberately skips untrusted project hooks. Run the doctor again if hooks don't fire.

The hook commands do **not** call any model. They read local Git metadata and edit only the marker-delimited section of `.agent/STATE.md`. On session start the current concise STATE file is injected once as hook context; at `PreCompact` and `Stop`, the Git signal is refreshed silently. Human sections are **maintained by the coding agent** under `AGENTS.md` at real milestones. No manual copying between chats.

## What is automatic, exactly?

| Data | Who updates it? | Trigger |
|---|---|---|
| Branch, HEAD, changed application paths | Python script | SessionStart, PreCompact, Stop |
| Goal, Done, Active, Blockers, Next, Verification | Codex/Claude following `AGENTS.md` | Meaningful milestone / finish |
| Plan / architectural decisions | Codex/Claude, only when needed | Scope or long-lived decision changes |

**Important:** A hook cannot determine from a Git diff whether tests passed, or whether the implementation is correct. The script never fabricates completed work. Agent-maintained notes are instructions, not an enforcement guarantee. If interrupted before a checkpoint, the next hook will still refresh Git status, but semantic task status might be stale; the agent must verify it against the code.

## Files and why they exist

| Path | Purpose |
|---|---|
| `AGENTS.md` | Small, shared contract for Codex and other agents |
| `CLAUDE.md` | Imports `AGENTS.md` via **`@AGENTS.md`**; Claude-specific notes only (no duplicated instructions) |
| `.agent/STATE.md` | Shared current state with fixed human sections and a protected machine section |
| `.agent/PLAN.md` | Small milestone plan, only read for multi-step work |
| `.agent/ARCHITECTURE.md` | Architecture intent vs verified facts; long-lived decisions |
| `.agent/CONVENTIONS.md` | Code, persistence, security and test conventions |
| `.claude/rules/dotnet.md`, `blazor.md` | Claude path-scoped YAML `paths` rules; loaded on relevant files |
| `.claude/settings.json` | Claude Code lifecycle hooks |
| `.codex/hooks.json` | Codex lifecycle hooks, with Windows-specific command override |
| `scripts/state_sync.py` | No-dependency, no-LLM, idempotent Git state synchronization |
| `scripts/tests/test_state_sync.py` | Regression tests to protect human notes and avoid redundant updates |
| `.ignore` | Reduces ripgrep search noise; not a file-access prohibition |
| `.gitignore` | Suggested build/secret excludes; merge into your existing rules |

There is deliberately **no** `.codexignore` or `.agentsignore`: support is tool/version-specific, while `.ignore` works with ripgrep. Codex can still access any file that other tools can read. Ignoring secrets in search is not security isolation.

## Daily workflow

- Give Codex or Claude a task normally. Don't paste state between sessions.
- The initial hook injects `STATE.md` and the agent verifies relevant code.
- After a meaningful milestone the agent updates only the short human sections.
- Hooks refresh the Git signal at turn stop and compaction; next sessions see it automatically.
- Keep `STATE.md` compact; avoid putting raw test logs or entire file contents into it.

## Troubleshooting and boundaries

- Run `python scripts/state_sync.py doctor`. If missing Python or `py -3`, update the command in the relevant JSON file.
- For Codex, use `/hooks` to trust the current definitions. If launched outside a Git repository, its git-root hook command will fail; `git init` or run Codex inside the repo.
- If you already have `.claude/settings.json` or `.codex/hooks.json`, **merge the `hooks` entries** rather than replacing unrelated settings.
- If you already have a `CLAUDE.md`, add `@AGENTS.md` once, without duplicating the agent contract. The import path resolves relative to `CLAUDE.md`.
- Project-specific paths/conventions here reflect a **proposed** Blazor + ASP.NET Core + PostgreSQL + RabbitMQ + SignalR + EF Core direction. They are not claims that the code already implements them.
- Git-based sync doesn't run in remote environments without these local files/scripts. Claude Cowork's file/hook execution environment can differ from Claude Code; this kit is specifically tested as a **Claude Code/Codex local configuration**, not a Cowork integration.
- Windows agents sometimes execute under a different shell; Codex's `commandWindows` uses PowerShell syntax and the `py` launcher. If your installation uses another shell, adapt the hook command.
- Hooks run local code, so inspect the scripts before trusting them. They do not send files or data over the network, commit, push, or modify application code.
- This optimizes repeated context/tool operations. It does **not** guarantee a particular reduction in subscription usage or 5-hour quota.

## Sources consulted (Oct 2026)

- [Codex hooks](https://learn.chatgpt.com/docs/hooks)
- [OpenAI Codex AGENTS.md workflow](https://developers.openai.com/cookbook/examples/codex/iterating-development-workflows-with-codex)
- [Claude Code hooks](https://code.claude.com/docs/en/hooks)
- [Claude Code memory, AGENTS.md imports, scoped rules](https://code.claude.com/docs/en/memory)
