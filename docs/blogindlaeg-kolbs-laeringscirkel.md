# Blogindlæg om de seneste ændringer i Auktionshuset

Udkastene tager udgangspunkt i [Kolbs læringscirkel](https://experientiallearninginstitute.org/wp-content/uploads/2020/10/The-Learning-Way-Learning-from-Experience-as-the-Path-to-Learning-and-Development.pdf): **konkret erfaring**, **reflekterende observation**, **abstrakt begrebsliggørelse** og **aktiv afprøvning**. Indholdet bygger på projektets kode, commits fra oktober–november 2026 og lokale acceptnotater. Overskrifterne kan få links, når indlæggene er publiceret.

| Indlæg | Ændringer | Afprøvning og status |
| --- | --- | --- |
| Kundeoprettelse og live budgivning | Tilmelding, bud, resultater og SignalR | Afprøvet i isoleret browsermiljø |
| Medarbejderlogin og roller | Gemte login, rettigheder og ugyldige sessioner | Afprøvet i isoleret browsermiljø |
| Lager og auktionsafslutning | Reservation, vinder og automatisk lukning | Testet mod isoleret PostgreSQL |
| Faktura og afhentning | Historiske beløb, betaling og kvittering | Testet lokalt; migration afventer normal deployment |
| Pålidelig beskedbehandling | Outbox, RabbitMQ og inbox | Afprøvet i lokal testopsætning |
| Bud fra et fysisk device | Session, fælles genstand og WebSocket | Backend og protokol testet; fysisk budforløb afventer |
| USB-opsætning af ESP32-S3 | Registrering, firmware og fejlsøgning | Konfiguration gemt; Wi-Fi/TLS og samlet accept afventer |

## Kundeoprettelse og live budgivning i Auktionshuset

Vi arbejdede på at gøre kundens vej fra oprettelse til et afsluttet budforløb sammenhængende. Det gav mig mulighed for at følge en funktion hele vejen fra brugerflade til database og tilbage til kundens skærm.

### Konkret erfaring

Kunder kan oprette en konto, tilmelde sig en auktion og byde på åbne genstande. De kan se deres eget bud, det højeste bud og et gemt resultat efter lukning. Vi tilføjede også framelding, men afviser den, hvis kunden allerede har afgivet et bud, selv når buddet er overbudt.

I en isoleret browserafprøvning bød to kunder på samme genstand. Den ene kundes visning skiftede automatisk fra **Du fører** til **Du er overbudt**, da den anden bød.

### Reflekterende observation

En opdateret pris i browseren er ikke nok, hvis siden kan vise en forkert eller forældet tilstand efter en afbrydelse. Vi så også, at en passeret sluttid ikke i sig selv er et endeligt resultat; linjen skal faktisk være lukket og gemt.

### Abstrakt begrebsliggørelse

Jeg ser liveopdatering som to adskilte opgaver: **at opdage en ændring** og **at hente den autoritative tilstand**. SignalR-hubben sender derfor kun en invalidering. Priser, budstatus og resultater hentes bagefter fra API'et, som kontrollerer den indloggede kundes adgang.

### Aktiv afprøvning

Vi afbrød SignalR-forbindelsen under en lokal test, lod en anden kunde byde og genoprettede forbindelsen. Siden viste afbrydelsen og hentede den nye pris automatisk efter genforbindelse. Den test gjorde det konkret, hvorfor genindlæsning skal være en del af realtidsfunktionen.

## Medarbejderlogin og adgangsstyring

Administrationsområdet fik gemte login til medarbejdere og tydeligere rollegrænser. Her blev det særligt vigtigt at undersøge, hvad der sker med en session, når en medarbejders adgang ændres.

### Konkret erfaring

En administrator kan oprette, ændre og deaktivere login til en medarbejder. Roller som **Employee**, **LotAdmin**, **AuctionAdmin** og **Admin** giver forskellige rettigheder. Databasen sikrer, at et login tilhører enten en kunde eller en medarbejder, og systemet beskytter den sidste aktive administrator.

### Reflekterende observation

Hvis en rolle kun ændres i databasen, kan et allerede udstedt token stadig bære gamle rettigheder. En skjult menu i brugerfladen løser heller ikke problemet, hvis API'et fortsat accepterer kaldet.

### Abstrakt begrebsliggørelse

Adgangskontrol skal håndhæves ved **API'et og den gemte identitet**. Når rolle, adgangskode eller aktiv status ændres, må den tidligere session ikke fortsætte med de gamle rettigheder. Projektet bruger derfor sessionsversioner til at afvise gamle adgangs- og refresh-tokens.

### Aktiv afprøvning

Vi prøvede rollerne i isolerede browsersessioner. En medarbejder med begrænset rolle blev afvist fra administrationssider, og en rolleændring krævede nyt login for at få de nye rettigheder. Tests dækkede også samtidige ændringer, der kunne have fjernet den sidste administrator.

## Lagerstyring og automatisk afslutning af auktioner

Vi skulle afklare, hvordan en auktionslinjes pris og antal hænger sammen med det fysiske lager. Det blev en øvelse i at få flere samtidige regler til at pege på samme resultat.

### Konkret erfaring

En linje kan indeholde flere enheder, men startprisen og buddet gælder for **hele linjen**. Åbne linjer reserverer det tilhørende antal på lageret. Ved lukning gemmes vinderen og lukketidspunktet samtidig med, at en solgt linjes antal trækkes fra lageret; en usolgt linje frigiver reservationen.

Vi tilføjede også en baggrundsservice, som lukker startede liveauktioner efter sluttidspunktet og forsøger igen efter nedetid. Tiderne behandles i UTC og vises lokalt i brugerfladen.

### Reflekterende observation

Hvis vinder og lager opdateres hver for sig, kan en afbrudt eller gentaget handling efterlade systemet med et salg uden korrekt lagertræk. Samtidige bud, manuelle lukninger og automatisk lukning gør risikoen mere konkret.

### Abstrakt begrebsliggørelse

Et auktionsresultat og dets lagerændring er **én forretningsmæssig transaktion**. PostgreSQL-låse og en gemt lukningsmarkør bruges til at forhindre, at samme linje trækkes fra lageret to gange.

### Aktiv afprøvning

Vi testede bud, lukning, gentagelse, genstart og samtidige handlinger mod isoleret PostgreSQL. Der blev også lavet et læsende SQL-audit til at finde konflikter i eksisterende lagerdata før udrulning. Audit af den tilsigtede projekt-/produktionsdatabase er endnu ikke dokumenteret som udført.

## Fakturaer, betaling og afhentning efter auktionen

Efter budforløbet arbejdede vi med, hvordan et vundet køb bliver til en faktura og senere til registreret betaling og afhentning.

### Konkret erfaring

Systemet danner én faktura pr. kunde for en helt afsluttet auktion. Hammerslaget gælder hele auktionslinjen, og et købersalær på **10 %** lægges til. Hammerslag og salær indeholder **25 % moms**, som udledes af beløbene. Fakturaen gemmer køber, sælger, genstande, antal og beløb som historiske værdier.

Medarbejdere kan registrere fuld betaling manuelt som kort eller kontant. Først efter betaling kan linjer markeres som afhentet, og kunden kan se kvittering.

### Reflekterende observation

En faktura må ikke ændre sig, blot fordi en adresse eller et genstandsnavn redigeres senere. Genforsøg efter en fejl må heller ikke skabe en ny faktura eller overskrive tidspunktet for en allerede registreret betaling.

### Abstrakt begrebsliggørelse

Jeg forstår fakturaen som et **gemt øjebliksbillede af aftalen**. Udstedelse, nummerering, betaling og afhentning kræver derfor vedvarende data, unikke nøgler og kontrollerede tilstandsskift.

### Aktiv afprøvning

Vi testede afrunding, samtidige handlinger, genforsøg, historiske ændringer og kundens adgang til egne dokumenter. I den lokale browseraccept blev en kontant **testbetaling** registreret, hvorefter kvitteringen kunne åbnes. Ingen ekstern betaling blev gennemført, og fakturamigrationen mangler normal deployment.

## Pålidelig beskedbehandling med outbox, RabbitMQ og inbox

Auktionshuset bruger hændelser til at føre ændringer videre til andre processer og til liveopdateringer. Jeg arbejdede med spørgsmålet om, hvad der sker, når beskedkøen ikke er tilgængelig i samme øjeblik som en databaseændring.

### Konkret erfaring

Projektet gemmer udgående hændelser i en **outbox**, før de publiceres til RabbitMQ. En **inbox** holder styr på modtagelse og behandling med genforsøg. Dermed kan API'et gemme en ændring, selv om RabbitMQ midlertidigt er nede, og publicere den senere. RabbitMQ-credentials hentes fra Vault frem for at ligge direkte i kildekoden.

### Reflekterende observation

En gemt ændring og en leveret besked er to forskellige hændelser. Hvis de behandles som én udelelig netværkshandling, kan et kort udfald give manglende opdateringer eller usikkerhed om, hvor langt behandlingen kom.

### Abstrakt begrebsliggørelse

Jeg ser outbox og inbox som en måde at håndtere **forsinket og gentaget levering** på. Modtagere må kunne genkende og håndtere genforsøg, mens browseren bør hente den aktuelle tilstand igen frem for at bygge sandheden op alene fra beskeder.

### Aktiv afprøvning

Vi afprøvede den samlede kunderejse lokalt med isoleret PostgreSQL, RabbitMQ, API og SignalR. Projektets tests dækker også beskedstatus og de to behandlingsspor. Testene understøtter den lokale adfærd; normal deployment og drift under længerevarende udfald er særskilte kontroller.

## Fra webbud til bud fra et fysisk device

Vi udvidede budsystemet, så en medarbejder kan knytte et device til en kunde og en auktion. Auktionarius vælger den genstand, alle tilknyttede devices aktuelt skal byde på.

### Konkret erfaring

Backend gemmer en aktiv device-session og kontrollerer kunde, auktion og valgt genstand ved et bud. Devicet kommunikerer med en **WebSocket-gateway**, mens backendændringer sendes gennem SignalR og omsættes til devicebeskeder. Bud får et request-id, så samme anmodning kan genkendes ved genforsøg.

### Reflekterende observation

Et tal fra en fysisk knap er ikke et sikkert bud uden kontekst. Hvis devicet har mistet forbindelsen eller er udleveret til en ny kunde, kan en gammel visning eller et forsinket svar knytte buddet til den forkerte situation.

### Abstrakt begrebsliggørelse

**Backend er autoritet for session, genstand og accepteret bud.** Devicet må først byde efter et aktuelt øjebliksbillede og skal stoppe, når forbindelsen eller konteksten bliver usikker. Beløb sendes som heltal i øre i protokollen, men serveren afgør stadig, om buddet opfylder reglerne.

### Aktiv afprøvning

Vi testede tildeling, sessionskontrol, snapshots, dubletter og bud mod isoleret PostgreSQL samt gatewayens genforbindelse. Den fysiske knapbetjening og et samlet deviceforløb gennem Wi-Fi, WebSocket, bud og lukning er endnu ikke godkendt.

## USB-opsætning af ESP32-S3 og den første fysiske accepttest

Den mest håndgribelige læringscirkel kom under opsætningen af et **ESP32-S3-device**. En medarbejder skal kunne starte registreringen fra websiden, mens en lokal station overfører konfigurationen via en fast USB-port.

### Konkret erfaring

Vi kompilerede og flashverificerede firmwaren på boardet på **COM4**. Ved den første registrering oprettede backend én afventende devicepost, men den fysiske USB-overførsel blev afvist. Efter rettelser i firmwaren kunne et kontrolleret genoptagelsesforsøg bruge samme device-id, og boardet kvitterede for gemt konfiguration.

### Reflekterende observation

Den første fejl viste, at en vellykket kompilering og hosttests ikke beviser, at en hel serialramme kan modtages på det fysiske board. Senere viste stationens **`configured`** heller ikke, at devicet havde forbindelse til backend: status var fortsat **`verification: wifi`**, og credential var inaktiv.

### Abstrakt begrebsliggørelse

Jeg skelner nu mellem **gemt USB-konfiguration**, **verificeret netværksforbindelse** og **godkendt budforløb**. Hver tilstand kræver sit eget bevis. En ukendt eller mislykket overførsel skal kunne genoptages uden at oprette et nyt device eller aktivere en credential for tidligt.

### Aktiv afprøvning

Fejlsøgningen førte til en rettelse af NVS-åbningen og en serialmodtagebuffer på 8 KB. Firmwaren blev flashverificeret igen, og den samme afventende registrering blev genoptaget fysisk. Næste afprøvning er at få det konfigurerede 2,4 GHz-net tilgængeligt og kontrollere Wi-Fi, TLS og backend fra boardet. Strømcyklus efter gemt konfiguration samt udlevering og bud på fysisk hardware mangler stadig; samlet accept er **ikke godkendt**.
