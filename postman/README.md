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
