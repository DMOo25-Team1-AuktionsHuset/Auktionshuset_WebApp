# Kundeauktioner og medarbejderlogin – accepttest 29. oktober 2026

## Implementeret adfærd

- Auktionsoversigten viser Live før Kommende, med stigende starttid og stabil sortering på id ved samme starttid. Grupperne har egne tomme tilstande.
- Egen framelding findes på oversigten og Mine auktioner med bekræftelse. `DELETE /api/customer/auctions/{id}/registration` afviser alle tidligere bud fra kunden, også overbudte bud og bud fra enhedssessioner. Framelding, tilmelding og bud bruger samme transaktionelle auktionslås.
- Kundehubben kræver rollen Customer og en aktiv gemt kundetilknytning. Den sender kun `AuctionsChanged` uden kunde-, bud- eller auktionsdata. Alle oplysninger hentes gennem de eksisterende API-kald med egen kundekontrol. Kunden kan ikke vælge en anden kundes abonnement.
- Eksisterende outbox → RabbitMQ → inbox → SignalR sender invalideringer ved bud, start/opdatering, lukning af genstande og afslutning. Kundesiderne bruger automatisk genforbindelse og genindlæsning efter tilslutning. Prisopdateringsknappen og kundernes femsekunderspolling er fjernet.
- Mine køb indlæser køb og fakturaer parallelt og uafhængigt med en grænse på 20 sekunder. Fejl angiver den berørte del og giver mulighed for at prøve igen. Allerede indlæste oplysninger bevares. Fakturaer og kvitteringer har også fejl-/indlæsningstilstande.
- Mine auktioner og Mine køb har hver sin ruteindgang med en eksplicit parameter til den fælles visningskomponent. Browseren reproducerede fejlen, hvor adressen skiftede til Mine auktioner, mens indholdet blev stående på Mine køb. Rutevalget afhænger nu ikke af `NavigationManager.Uri` under rendering.
- Kun Admin kan oprette/ændre medarbejderlogin. Roller og danske rettighedsbeskrivelser følger eksisterende permissions; alle fire medarbejderroller kan registrere betaling og afhentning. Den gemte konto har enten kunde- eller medarbejdertilknytning, aldrig begge, beskyttet af databaseconstraint og unikke indeks.
- Ændret rolle, adgangskode eller aktiveringstilstand øger sessionsversionen, afviser gamle JWT'er, spærrer gamle refresh-tokens og lukker aktive hubforbindelser på API-instansen. Versionskontrollen forhindrer også genbrug efter genaktivering, selv hvis en refresh-session blev oprettet samtidig med ændringen.
- Sidste aktive administrator beskyttes af en særskilt PostgreSQL-lås, også ved samtidige ændringer. Konfigurerede administratorer tæller med; konfigurerede logins administreres i konfigurationen. E-mailkonflikter afvises. Medarbejdere med login kan ikke slettes gennem medarbejderendpointet.

## Udført i browseren

Afprøvningen brugte to separate kundesessioner og en administrator samt Employee, LotAdmin og AuctionAdmin i isolerede lokale PostgreSQL-/RabbitMQ-containere. Projektets rigtige database og konfigurerede credentials blev ikke anvendt.

| Forløb | Observeret resultat |
| --- | --- |
| Rækkefølge, tilmelding, framelding og gentilmelding | Live stod øverst før og efter handlingerne. Framelding uden bud gav bekræftelse og mulighed for gentilmelding. |
| Framelding efter overbud | Afvist med forklaring om bindende tidligere bud. |
| To kunders bud | Anden kundes side modtog prisændringen uden opdatering; første kunde skiftede automatisk fra Du fører til Du er overbudt. |
| Start af kommende auktion | Auktionen flyttede automatisk til Live-gruppen; Kommende viste tom tilstand. |
| Lukning og afslutning | Vinder fik Vundet og hammerslag automatisk efter lukning. Oversigten fjernede afsluttet auktion automatisk. |
| Mistet SignalR-forbindelse | Kundehubben blev afbrudt og nye tilslutninger midlertidigt afvist. Kunden viste forbindelsesstatus. Et bud på 140,01 kr. blev afgivet under afbrydelsen; modtageren stod fortsat på 100 kr. Efter genforbindelse hentede siden automatisk 140,01 kr. uden brugerhandling. |
| Mine køb og navigation | Tomme køb, vundne genstande, faktura og kvittering blev vist. Navigation mellem Mine køb og Mine auktioner blev kontrolleret efter rettelsen. |
| Fejl efter indlæst faktura | Testdatabasen blev pauset. Siden afsluttede indlæsning med konkrete serverfejl for køb og fakturaer og beholdt fakturaen på 154,01 kr. Efter genoptagelse hentede Prøv igen/opdatér data og fjernede fejlen. |
| Faktura og kvittering | Faktura åbnede med moms/salær og total. Efter lokal registrering af kontant testbetaling kunne betalingskvitteringen pr. linje åbnes. Ingen ekstern betaling blev udført. |
| Oprettelse af medarbejderlogin | Medarbejder uden login viste tom loginstatus. Et AuctionAdmin-login blev oprettet gennem UI og kunne logge ind og åbne auktioner. |
| Rolleændring og deaktivering | Employee viste kun betaling/afhentning. Ændring til LotAdmin afviste gammel session; nyt login gav lageradgang. Direkte medarbejderadresse blev afvist. Deaktivering sendte eksisterende session til login ved næste API-handling. |

Samtidigt bud/framelding og samtidige fjernelser af administratorrettigheder blev kontrolleret i automatiske PostgreSQL-tests, ikke ved manuel timing i browseren. Tests beskytter også kundeisolering, faktura-/kvitteringsadgang og uændrede kundelogin efter migration. Fejlsvar og timeouts for køb og fakturaer er særskilt dækket i frontendtests.

## Kør igen

Slutkontrol: 166 API-tests (inklusive PostgreSQL), 226 frontend-/applikationstests og 11 infrastrukturtests bestod: 403 i alt, ingen fejl eller fravalgte tests. Den manuelle browservært er udeladt fra disse tal. `git diff --check` bestod. De eksisterende compileradvarsler CS0105 og CS8625 er fortsat til stede.

De automatiske PostgreSQL-tests bruger `AUCTION_STOCK_TEST_CONNECTION` til en isoleret testserver. Hver test opretter og fjerner sin egen database. Brug aldrig projektets rigtige databaseforbindelse.

```powershell
dotnet test Auktionshuset.Api.Test --no-restore -m:1 --filter 'FullyQualifiedName!~BrowserAcceptanceHost'
dotnet test Auktionshuset.Tests --no-restore -m:1
dotnet test Auktionshuset.Infrastructure.Test --no-restore -m:1
```

`BrowserAcceptanceHost` er en manuel testvært og er fravalgt uden `AH_BROWSER_ACCEPTANCE=1`. Den kræver en separat RabbitMQ på `127.0.0.1:55679` med den lokale testbruger `codex` og testadgangskoden `codex-isolated-test`. API'en lytter kun på `127.0.0.1:55133`. Testkonti bruger `example.test` og adgangskoden `test-password-123`. Værten anvender projektets rigtige messaging- og databaserutiner uden Vault.

Start værten med `dotnet test Auktionshuset.Api.Test --filter FullyQualifiedName~BrowserAcceptanceHost` og start frontend med `Api__BaseUrl=http://127.0.0.1:55133` på en separat lokal port. Adskil cookies med fx `localhost`, `127.0.0.1` og `localhost.`. Angiv en lokal `AH_BROWSER_STOP_FILE`; opret filen for at stoppe værten og frigive testdatabasen. En valgfri `AH_BROWSER_DISCONNECT_FILE` aktiverer kontrolleret afbrydelse af kundehubben, mens filen findes. Disse kontroller findes kun i testprojektet.

## Deployment

Migrationen `20261029131752_AddEmployeeLogins` omfatter nullable CustomerId, unik EmployeeId, aktiv loginstatus, sessionsversion og ejer-/rolleconstraint samt sessionsversion på refresh-tokens. Eksisterende kundelogin får aktiv status og beholder kundetilknytning/passwordhash. Opgradering fra tidligere skema med en eksisterende kunde er testet mod PostgreSQL; modelsnapshot er kontrolleret. Ældre adgangstokens kræver nyt login efter opgradering.

Migrationer er kun kørt på testdatabaser. Anvend de udestående migrationer gennem normal deployment. Produktion og flere API-instanser er ikke browserafprøvet. Den eksisterende AUCTION-STOCK.md/BILLING.md-vejledning for lager, ældre fakturaer og deployment gælder fortsat.
