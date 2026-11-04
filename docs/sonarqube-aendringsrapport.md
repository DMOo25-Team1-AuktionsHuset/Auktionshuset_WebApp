# Samlet ændringsrapport: ECMA-afklaring og SonarQube-rettelser

**Projekt:** AuktionsHuset_WebApp  
**Dato:** 4. november 2026  
**Branch:** `Fixes/iso-ecma`  
**Git-baseline:** `88cf466e1c9d7a7a018fc049a86e95ba724ba280`  
**Status:** Lokale ændringer; ingen commit, push eller deployment.

## 1. Resultat og afgrænsning

Rapporten samler arbejdet med de syv SonarQube-skærmbilleder og forklarer sammenhængen med den forudgående ECMA-kontrol. De viste fund er behandlet i den aktuelle kode. Den afsluttende genbygning havde **0 fejl og 0 advarsler**, og den samlede testkørsel havde **389 beståede, 0 fejlede og 23 oversprungne tests**.

SonarQube-serveren er ikke blevet scannet igen. Rapporten dokumenterer derfor kodeændringer og lokal verifikation; den dokumenterer ikke, at serverens viste antal på 76 issues er faldet til nul. Flere af de viste issues vedrører samme kald og optræder både som Sonar- og Roslyn-fund.

Ændringerne omfatter **50 kildefiler: 46 C#-filer og fire Razor-filer**. Heraf er 49 eksisterende filer ændret, og én testfil er tilføjet. Dokumentation og agent-checkpoints er opgjort særskilt.

| Projekt | Berørte kildefiler |
| --- | ---: |
| Auktionshuset | 18 |
| Auktionshuset.Api | 4 |
| Auktionshuset.Application | 1 |
| Auktionshuset.Infrastructure | 14 |
| Auktionshuset.Tests | 8 |
| Auktionshuset.Api.Test | 5 |
| **I alt** | **50** |

## 2. Forholdet til ECMA-kontrollen

Den tidligere ECMA-vurdering fandt ingen dokumenterede normative brud inden for den aftalte afgrænsning. Den del af arbejdet førte derfor ikke til kildekodeændringer. Analysen og overdragelsen findes i `docs/ecma334-analyse.md` og `docs/ecma334-ai-instruktioner.md`.

Den efterfølgende anmodning om at rette SonarQube-fund udvidede opgaven til kvalitets- og analyzerrettelser. Det gav grundlag for blandt andet at behandle de to compileradvarsler, som ikke tidligere var dokumenteret som ECMA-brud. Ændringerne er således ikke en ny konklusion om, at disse konstruktioner overtrådte ECMA-334.

Nyere C# er bevaret sammen med `net10.0`, nullable-analyse og implicitte imports. Projektfiler, pakkeversioner og afhængigheder er ikke ændret.

## 3. Cancellation og asynkrone operationer

I `CustomerAuctionService` og `InvoiceService` videreføres det eksisterende `CancellationToken` nu til JSON-læsning og læsning af fejlmeddelelser. Tidligere nåede tokenet HTTP-kaldet, men ikke alle de efterfølgende operationer. Ændringen omfatter opslag af mine auktioner, køb, tilgængelige auktioner og fakturaer.

I `BackendSessionService` er fravalget af browserforespørgslens cancellation gjort eksplicit med `CancellationToken.None` ved fornyelse af login-token og læsning af svaret. Den eksisterende beskyttelse af engangs-refresh-tokenet bevares: en afbrudt browserforespørgsel må ikke opgive en igangværende tokenrotation, som en anden fane kan være afhængig af. Den eksisterende test af dette forløb består fortsat.

`RealtimeServiceBase` bruger nu sin levetids-token ved hentning af adgangstoken. Ventetiden på oprydningslåsen bruger eksplicit `CancellationToken.None`, så en allerede afbrudt levetid ikke forhindrer oprydning.

`BackendApiClient` og `CustomerResults.razor` bruger `await CancelAsync()` i de berørte asynkrone forløb. Frontendens og API'ets startpunkter bruger begge `await app.RunAsync()`.

## 4. Metodesign, oprydning og autorisation

`CancellationToken` er flyttet til sidste parameterposition i `ApiProblemReader.ReadMessageAsync` og de berørte private `EnsureSuccessAsync`-metoder i kunde- og medarbejderservicen. Alle kald er opdateret, inklusive kald fra `AuctionService`, `LotService` og `EmployeeLoginEditor.razor`.

`GC.SuppressFinalize(this)` er tilføjet i de berørte dispose-metoder i `RealtimeServiceBase`, `AuctionAdmin`, `EmployeeAdmin` og `Warehouse`. Den asynkrone dispose-metode i `CustomerResults` er også opdateret til at afvente cancellation og undertrykke finalisering.

Frontendens policies registreres nu via `AddAuthorizationBuilder().AddPolicy(...)`. De eksisterende policy-navne, roller og claimkrav er bevaret i diffen. Den overflødige eksplicitte `public partial class Program` er fjernet; frontendens integrationstests kompilerer og består med projektets eksisterende SDK.

## 5. Logging, namespace og compileradvarsler

| Sted | Ændring | Formål |
| --- | --- | --- |
| `RealtimeServiceBase` | Interpoleret logtekst erstattet af fast skabelon med `{HubUrl}` | Bevar strukturerede logfelter og en stabil skabelon. |
| `OutboxProcessor` | Interpoleret tekst erstattet af skabelon med `{OutboxId}` | Samme logstruktur på tværs af kald. |
| `AdminEventsConsumer` | Kontrol med `IsEnabled(LogLevel.Information)` før det berørte logkald | Undgå unødvendig argumentbehandling, når niveauet er slået fra. |
| `MessagingMaintenanceWorker` | Logniveaukontrol tilføjet til betingelsen for det berørte logkald | Samme afgrænsning af logarbejdet. |
| `MessagingOperations.cs` | `ReplayRow` flyttet ind i filens navngivne namespace | Fjern typen fra det globale namespace. |
| `PostgresAuctionWriteSessionFactory.cs` | Dubleret namespace-import fjernet | Fjern CS0105. |
| `Warehouse.razor.cs` | Den injicerede `LotRealtimeService` initialiseres med `default!` | Følg komponentens eksisterende mønster for DI-egenskaber og fjern CS8625. |

`default!` ændrer nullable-analysen; det opretter ikke en serviceinstans. Komponenten er fortsat afhængig af den eksisterende dependency injection.

## 6. Collection-typer, arrays og billedendelser

Private returtyper, feltdeklarationer og hjælperparametre er ændret til konkrete typer på de viste steder: arrays i auktions-endpointet, filtrerede komponentlister og målingshjælperen samt `Dictionary` i de berørte opslag. Valideringshjælpere i tre testfiler returnerer nu `List<ValidationResult>`. Razor-visninger og paginering er tilpasset med `Length`, hvor resultatet nu er et array.

Gentagne konstante arrays i de viste testfiler er flyttet til `static readonly`-felter. De tilsvarende ændringer i migrationsfiler er beskrevet særskilt nedenfor.

I `PostgresLotImageStore` er sammenligningen via `extension.ToLowerInvariant()` erstattet af en direkte sammenligning med `StringComparison.InvariantCultureIgnoreCase` på det udpegede sted. Normalisering til filnavn og valg af indholdstype er fortsat til stede.

Disse ændringer følger de viste analyzeranbefalinger. Der er ikke udført benchmarks, så rapporten angiver ingen målt hastigheds- eller hukommelsesgevinst.

## 7. Migrationer og dokumenteret SQL-lighed

I følgende syv eksisterende migrationer er konstante kolonne- og sorteringsarrays flyttet til private `static readonly`-felter:

1. `20260924185425_InitialCreate.cs`
2. `20260929115216_Updated_Entities_For_Bid.cs`
3. `20261019093449_rabbitmq_hardening.cs`
4. `20261020092712_Inbox_First_Integration_WIP.cs`
5. `20261020114911_Inbox_Processor_Policy_RabbitMQ.cs`
6. `20261027090409_MondayMorning.cs`
7. `20261029123907_AddAuctionInvoices.cs`

Der er ikke tilføjet en migration. Kolonnenavne, kolonnerækkefølge, sorteringsretninger og de eksisterende migrationsoperationer er bevaret.

Et midlertidigt kontrolprogram genererede Npgsql-SQL for både Up og Down fra de oprindelige kompilerede migrationer og fra de genbyggede migrationer. SQL-filerne var byte-identiske og havde begge SHA-256:

```text
DC6C99148787C56FEAC7D78C3687982D09E15EB03479D4828828DF8D3A1C9DDA
```

Kontrollen oprettede ingen databaseforbindelse og anvendte ingen migration på en database. Lighed af genereret SQL erstatter ikke den oversprungne PostgreSQL-acceptance.

## 8. Testændringer

Konstante regexer i fem eksisterende testfiler bruger nu `[GeneratedRegex]` og navngivne partial-metoder. Det omfatter mønstre til antiforgery-token, formularer, formularhandler, HTML-kommentarer og startknappen for auktioner. Den dynamiske regex, der afhænger af en statusværdi under testen, er bevaret.

I `CreateLotTest` er theory-data med DTO-objekter erstattet af serialiserbare `InlineData`-værdier. Testen opretter selv den konkrete ugyldige DTO ud fra en case-nøgle. De otte eksisterende valideringssituationer er bevaret, så testværktøjerne kan identificere de enkelte datarækker.

Den nye fil `CustomerResponseCancellationTests.cs` indeholder otte cases: fire opslag testet med henholdsvis et normalt svar og et fejlsvar. Testene afbryder operationen efter buffering af HTTP-svaret og forventer `OperationCanceledException`, så efterfølgende behandling af svaret også omfattes af cancellation.

`BrowserAcceptanceHost` har fået en konkret HTTP-assertion: et uautentificeret kald til `/api/customer/my-auctions` skal returnere HTTP 401. Denne assertion er tilføjet i koden, men er endnu ikke kørt, fordi browserhost-testen er opt-in og blev oversprunget.

## 9. Verifikation og resultater

Resultaterne nedenfor stammer fra rettelsesarbejdets afsluttende verifikation. Ved udarbejdelsen af denne rapport er diff, filinventar, buildlog og SQL-hashes kontrolleret igen. Build og tests er ikke kørt endnu en gang alene for rapportens skyld.

| Kontrol | Resultat |
| --- | --- |
| Fokuserede API-tests | 26 bestået, 0 fejlet |
| Fokuserede frontend-/domænetests | 104 bestået, 0 fejlet |
| Fuld solution-genbygning | 0 fejl, 0 advarsler |
| Valgt analyzer-kontrol på ændrede, trackede C#-filer | Bestået; ingen yderligere rettelser foreslået af den afgrænsede kontrol |
| `git diff --check` | Bestået |
| Npgsql Up/Down-SQL for syv migrationer | Byte-identisk før og efter |

De fokuserede tests indgår også i den samlede suite og skal ikke lægges oven i dens testantal:

| Testsuite | Bestået | Fejlet | Oversprunget |
| --- | ---: | ---: | ---: |
| Auktionshuset.Tests | 234 | 0 | 0 |
| Auktionshuset.Api.Test | 144 | 0 | 23 |
| Auktionshuset.Infrastructure.Test | 11 | 0 | 0 |
| **I alt** | **389** | **0** | **23** |

I forhold til den tidligere baseline på 381 beståede tests er der otte yderligere beståede regressionstest-cases.

Den samlede testkommando var:

```powershell
dotnet test AuktionsHuset_WebApp.slnx --no-build --no-restore --nologo -m:1
```

Analyzer-kontrollen brugte `dotnet format analyzers` med `--no-restore`, `--verify-no-changes`, `--severity info` og afgrænset `--include`. De valgte diagnostikker var `CA1068`, `CA1859`, `CA1861`, `CA1862`, `CA2254`, `CA1816`, `CA2016`, `SYSLIB1045`, `ASP0025`, `ASP0027` og `CA1873`. Det er en lokal, udvalgt analyzer-kontrol og ikke en erstatning for hele SonarQube-analysen.

Sandboxens named-pipe-begrænsninger krævede godkendte analyzer- og testkørsler uden for sandboxen. Et tidligt build fandt to manglende tilpasninger af Razor-kald til fejlmeddelelseslæseren; de blev rettet inden den afsluttende vellykkede genbygning.

## 10. Begrænsninger og resterende kontrol

- SonarQube er ikke genkørt. En ny scanning skal vise, hvilke serverregistreringer der lukkes, og om der findes yderligere aktuelle fund.
- De 23 oversprungne tests omfatter 22 PostgreSQL-tests og én browserhost-test. Database- og browseracceptance, inklusive den nye browserhost-assertion, er derfor ikke verificeret i denne ændring.
- Der er ikke udført produktionsdeployment, databaseopdatering, benchmark eller formel ECMA-certificering.

## 11. Dokumentation og arbejdsstatus

`.agent/PLAN.md` og HUMAN-sektionerne i `.agent/STATE.md` er opdateret med ændringer, resultater og begrænsninger. AUTO-SNAPSHOT-blokken er bevaret. De to tidligere ECMA-dokumenter er bevaret som dokumentation for den særskilte analyse.

Midlertidigt kontrolprogram, buildlog og SQL-filer ligger i:

```text
C:\Users\megan\.codex\visualizations\2026\11\04\01a1a5ef-1bb9-7842-b965-42158147b0d5\sonar-verification
```

Ændringerne er endnu ikke committet. Rapportens filinventar nedenfor medtager både den trackede diff og den nye, endnu utrackede testfil.

## Bilag: Komplet inventar over berørte kildefiler

### Auktionshuset

- `Auktionshuset/Components/EmployeeLoginEditor.razor`
- `Auktionshuset/Components/Pages/AuctionAdmin.razor`
- `Auktionshuset/Components/Pages/AuctionAdmin.razor.cs`
- `Auktionshuset/Components/Pages/CustomerResults.razor`
- `Auktionshuset/Components/Pages/EmployeeAdmin.razor`
- `Auktionshuset/Components/Pages/EmployeeAdmin.razor.cs`
- `Auktionshuset/Components/Pages/Warehouse.razor.cs`
- `Auktionshuset/Program.cs`
- `Auktionshuset/Security/BackendSessionService.cs`
- `Auktionshuset/Services/ApiProblemReader.cs`
- `Auktionshuset/Services/AuctionService.cs`
- `Auktionshuset/Services/BackendApiClient.cs`
- `Auktionshuset/Services/CustomerAuctionService.cs`
- `Auktionshuset/Services/CustomerService.cs`
- `Auktionshuset/Services/EmployeeService.cs`
- `Auktionshuset/Services/InvoiceService.cs`
- `Auktionshuset/Services/LotService.cs`
- `Auktionshuset/Services/RealtimeServiceBase.cs`

### Auktionshuset.Api

- `Auktionshuset.Api/Endpoints/Admin/Auction/GetAuctions/GetAuctionsEndpoint.cs`
- `Auktionshuset.Api/Program.cs`
- `Auktionshuset.Api/Security/ConfiguredAuthUserStore.cs`
- `Auktionshuset.Api/Security/SecurityRolePermissions.cs`

### Auktionshuset.Api.Test

- `Auktionshuset.Api.Test/Admins/Auctions/BrowserAcceptanceHost.cs`
- `Auktionshuset.Api.Test/Admins/Auctions/MessagingStatusPostgresTests.cs`
- `Auktionshuset.Api.Test/Admins/Lots/CreateLotTest.cs`
- `Auktionshuset.Api.Test/Security/ConfiguredAuthUserStoreTests.cs`
- `Auktionshuset.Api.Test/Security/EndpointAuthorizationPipelineTest.cs`

### Auktionshuset.Application

- `Auktionshuset.Application/Admin/Auctions/GetAuctions/GetAuctionsHandler.cs`

### Auktionshuset.Infrastructure

- `Auktionshuset.Infrastructure/Messaging/Consumers/AdminEventsConsumer.cs`
- `Auktionshuset.Infrastructure/Messaging/Consumers/MessagingMaintenanceWorker.cs`
- `Auktionshuset.Infrastructure/Messaging/MessagingServices/MessagingOperations.cs`
- `Auktionshuset.Infrastructure/Messaging/MessagingServices/MessagingRuntime.cs`
- `Auktionshuset.Infrastructure/Messaging/OutboxServices/OutboxProcessor.cs`
- `Auktionshuset.Infrastructure/Migrations/20260924185425_InitialCreate.cs`
- `Auktionshuset.Infrastructure/Migrations/20260929115216_Updated_Entities_For_Bid.cs`
- `Auktionshuset.Infrastructure/Migrations/20261019093449_rabbitmq_hardening.cs`
- `Auktionshuset.Infrastructure/Migrations/20261020092712_Inbox_First_Integration_WIP.cs`
- `Auktionshuset.Infrastructure/Migrations/20261020114911_Inbox_Processor_Policy_RabbitMQ.cs`
- `Auktionshuset.Infrastructure/Migrations/20261027090409_MondayMorning.cs`
- `Auktionshuset.Infrastructure/Migrations/20261029123907_AddAuctionInvoices.cs`
- `Auktionshuset.Infrastructure/Service/Auctions/PostgresAuctionWriteSessionFactory.cs`
- `Auktionshuset.Infrastructure/Service/Lots/PostgresLotImageStore.cs`

### Auktionshuset.Tests

- `Auktionshuset.Tests/Auction/CreateAuctionRequestTests.cs`
- `Auktionshuset.Tests/Auction/ManualAuctionPageTests.cs`
- `Auktionshuset.Tests/Auction/UpdateAuctionRequestTests.cs`
- `Auktionshuset.Tests/Auth/AuthenticationFlowIntegrationTests.cs`
- `Auktionshuset.Tests/Auth/CustomerAreaFlowTests.cs`
- `Auktionshuset.Tests/Auth/CustomerRegistrationFlowTests.cs`
- `Auktionshuset.Tests/Auth/CustomerResponseCancellationTests.cs` (ny fil)
- `Auktionshuset.Tests/Auth/SessionRefreshIntegrationTests.cs`

