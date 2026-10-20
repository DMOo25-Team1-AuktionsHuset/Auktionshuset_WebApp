# Postman: login, refresh og logout

1. Start PostgreSQL og RabbitMQ med projektets sædvanlige opsætning.
2. Start API: `dotnet run --project Auktionshuset.Api --launch-profile https`.
3. Åbn collectionen **Auktionshuset – lokal API-test** fra denne mappe i Postman med understøttelse af collections i YAML-format. Behold undermapperne og deres `.resources/definition.yaml`-filer; de beskriver collectionens struktur, rækkefølge og fælles Bearer-auth.
4. Opret og vælg et lokalt miljø med navnet **Auktionshuset Local**. Tilføj `baseUrl` med værdien `https://localhost:7061`, og udfyld `email` og `password` med en eksisterende lokal testbrugers login. Opret `accessToken` og `refreshToken` med tomme værdier. Scripts gemmer automatisk tokens og opretter hjælpevariablerne `previousRefreshToken` og `loggedOutRefreshToken` under testene.
5. Kør requests i **1 Normalt flow** i rækkefølge. Login og refresh gemmer automatisk tokens. GET /api/auctions/ bruger collectionens Bearer-token.
6. Kør **2 Replay – ny session** i rækkefølge. Genbrug af det gamle refresh-token skal give 401 og revokere det nye token fra samme session.
7. Kør **3 Login rate limit** separat med seks hurtige requests. Vent mindst et minut uden loginforsøg først. Forvent fem 401 og derefter 429. Tidligere loginforsøg tæller med; ved 429 følg Retry-After.

POST-requests har Content-Type: application/json. Refresh og logout bruger No Auth.
Logout gemmer tokenet til negativ test og rydder klientens aktive tokens. Allerede udstedte JWT'er kan fortsat virke indtil udløb.
Udløbstest: Log ind, vent til expiresAtUtc er passeret (inklusive eventuel clock skew), og kald det beskyttede endpoint igen; forvent 401. Refresh derefter og gentag kaldet med det nye access token.
Refresh rate limit: Kør et separat refresh-request med et ugyldigt token 31 gange hurtigt efter et minut uden refresh-kald. Forvent 401 for de første 30 og 429 for nummer 31.
Hvis lokal HTTPS fejler på certifikatet, kør `dotnet dev-certs https --trust` og sørg for, at Postman stoler på udviklingscertifikatet.

Gem aldrig udfyldte credentials eller tokens i Git. Miljøfilen følger ikke med repoet. Lokale miljøfiler (`*.environment.yaml` og `*.postman_environment.json` i denne mappe) samt `.postman/` med lokal workspace-tilknytning er ignoreret via `.gitignore`. Gem dit lokale miljø uden for repoet eller med et af disse filnavnemønstre.
`postman/globals/workspace.globals.yaml` indeholder kun en tom liste og kan committes, så længe der ikke tilføjes private værdier.
Requests er bygget ud fra kildekoden; flowet er ikke kørt mod et levende API.

## Webappens session og roller

Webappen gemmer både access- og refresh-token på serveren. Browseren får kun en beskyttet sessionscookie. Access-tokenet fornyes, når det udløber inden for 30 sekunder, både ved HTTP-kald og under Blazors løbende sessionskontrol. Sessionens absolutte udløbstid følger refresh-tokenet og forlænges ikke ved rotation. Samtidige refresh-kald og logout bruger samme lås pr. session, så et refresh-token kun sendes én gang.

Logout kalder API'ets `/api/auth/logout` med sessionens seneste refresh-token og rydder altid den lokale session. Hvis API'et er utilgængeligt, logges fejlen uden tokens; den lokale session er stadig afsluttet, men tilbagekaldelsen hos API'et kan ikke bekræftes. Fejlet refresh afslutter sessionen og kræver nyt login. Sessionerne ligger i frontendens hukommelse, så genstart kræver nyt login; flere frontendinstanser kræver en fælles sessionsløsning eller sticky sessions.

| Rolle | Lager | Auktioner | Medarbejdere |
| --- | --- | --- | --- |
| `LotAdmin` | Læse, oprette, redigere, slette | Ingen administrationsadgang | Ingen adgang |
| `AuctionAdmin` | Læse og vælge genstande | Oprette, redigere, slette | Læse via API og auktionens medarbejdervalg |
| `SuperAdmin` | Alle handlinger | Alle handlinger | Alle handlinger |

Disse administrationsrettigheder styres af roller; de kræver ikke de tidligere `permission`-claims. Medarbejderadministrationens side og skrive-endpoints kræver fortsat `SuperAdmin`.

## IP-adresser og loginbegrænsning

Frontendens login-, refresh- og logout-kald sender den serverbestemte klient-IP som `X-Forwarded-For`. Browserens egen header kopieres aldrig direkte. API'et anvender kun headeren fra afsendere i `Authentication:KnownProxies`; ellers tæller den direkte afsender-IP. Grænsen er fem loginforsøg pr. klient-IP pr. minut, inklusive vellykkede loginforsøg, samt 30 refresh- og 30 logout-kald i hver sin grænse.

I lokal udvikling stoler API'et på `127.0.0.1` og `::1`, så frontend og API kan køre på samme computer. Ved deployment skal API'ets `Authentication:KnownProxies` indeholde frontendserverens faktiske afsender-IP (eller den nærmeste betroede proxy). Hvis selve frontenden står bag en reverse proxy, skal frontendens egen `Authentication:KnownProxies` indeholde denne proxys IP. Der accepteres ét forwarding-hop. En proxy skal overskrive indgående forwarding-headers med den verificerede klient-IP; ved flere proxy-hop skal kæden konfigureres og testes særskilt. Uden betroede proxy-adresser ignoreres forwarding-headers.

## Gennemgang af rettelserne

1. Log ind som `SuperAdmin`, og kontrollér rediger/slet på auktioner samt lagerets oprettelsesformular.
2. Kontrollér de samme rettigheder med `AuctionAdmin` og `LotAdmin`, også uden permission-claims. Den nuværende konfigurerede bootstrapbruger er `SuperAdmin`; de øvrige roller er dækket med testbrugere i integrationstestene.
3. Lad access-tokenet udløbe, og genindlæs en beskyttet side. Sessionen skal fortsætte via refresh. Logout skal tilbagekalde det seneste refresh-token.
4. Kør seks hurtige loginforsøg fra én klient-IP: nummer seks skal give 429 med en dansk fejlbesked i webappen. En anden klient-IP skal have sin egen grænse, når proxyopsætningen er korrekt.
5. Kald begge medarbejder-GET-endpoints med en `Customer` eller `LotAdmin`: forvent 403. `AuctionAdmin` og `SuperAdmin` må læse.

Automatiske tests køres med `dotnet test AuktionsHuset_WebApp.slnx`. Sessions- og forwarding-testene bruger testservere og simulerede API-svar; de erstatter ikke Postman-flowet mod en rigtig PostgreSQL-database.
