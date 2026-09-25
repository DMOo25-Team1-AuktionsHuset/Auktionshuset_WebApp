# Lokal opsætning og test af API-login

Denne guide beskriver, hvordan hvert gruppemedlem konfigurerer og tester det lokale login-flow.
User secrets deles ikke gennem Git og skal derfor oprettes separat på hver maskine.

## 1. Forudsætninger

- Hent branchen `Fixes/SecurityRefactor3.0`.
- Åbn Developer PowerShell i solution-mappen.
- Start Docker og den lokale RabbitMQ-container.

## 2. Konfigurer RabbitMQ

Hvis den lokale RabbitMQ bruger standard-login, kør:

```powershell
dotnet user-secrets set "RabbitMQ:UserName" "guest" --project .\Auktionshuset.Api
dotnet user-secrets set "RabbitMQ:Password" "guest" --project .\Auktionshuset.Api
```

Host og port ligger allerede i `Auktionshuset.Api/appsettings.json`.

## 3. Generer en signing key

Kør linjerne en ad gangen i Developer PowerShell:

```powershell
$authSigningKeyBytes = New-Object byte[] 32
$randomGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$randomGenerator.GetBytes($authSigningKeyBytes)
$authSigningKey = [Convert]::ToBase64String($authSigningKeyBytes)
$randomGenerator.Dispose()

dotnet user-secrets set "Authentication:SigningKey" $authSigningKey --project .\Auktionshuset.Api

Remove-Variable authSigningKey, authSigningKeyBytes, randomGenerator
```

Nøglen gemmes lokalt uden for repositoryet. Hvis en token bliver delt ved en fejl, genereres og gemmes en ny signing key, hvorefter API'et genstartes.

## 4. Opret en lokal bootstrap-admin

Erstat password-placeholderen med et lokalt password:

```powershell
dotnet user-secrets set "Authentication:BootstrapAdmin:Email" "admin@auktionshuset.dk" --project .\Auktionshuset.Api
dotnet user-secrets set "Authentication:BootstrapAdmin:Password" "INDSÆT-EGET-PASSWORD" --project .\Auktionshuset.Api
dotnet user-secrets set "Security:EnforceAuthorization" "true" --project .\Auktionshuset.Api
```

Bootstrap-adminen er foreløbig en konfigurationsbaseret testbruger. Den bliver senere erstattet af databasebaserede Identity-brugere.

## 5. Start API'et

1. Vælg `Auktionshuset.Api` med HTTPS-profilen som startup project.
2. Start projektet.
3. Kontrollér, at Output viser:

```text
Now listening on: https://localhost:7061
Application started.
```

Hvis API'et fejler på RabbitMQ, kontrollér at Docker-containeren kører, og at `RabbitMQ:UserName` og `RabbitMQ:Password` er oprettet som user secrets.

## 6. Test forkert login

Åbn `Auktionshuset.Api/Auktionshuset.Api.http`. Filen har allerede denne variabel øverst:

```http
@Auktionshuset.Api_HostAddress = https://localhost:7061
```

Tilføj følgende request:

```http
###

# Forkerte credentials
POST {{Auktionshuset.Api_HostAddress}}/api/auth/login
Content-Type: application/json

{
  "email": "forkert@example.dk",
  "password": "forkert-password"
}
```

Tryk `Send Request`. Det forventede resultat er:

```text
401 Unauthorized
```

## 7. Test korrekt login

Tilføj denne request, og indsæt det lokale bootstrap-password midlertidigt:

```http
###

# Korrekte credentials
POST {{Auktionshuset.Api_HostAddress}}/api/auth/login
Content-Type: application/json

{
  "email": "admin@auktionshuset.dk",
  "password": "INDSÆT-EGET-PASSWORD"
}
```

Det forventede resultat er `200 OK`. Responsen skal indeholde:

- `accessToken`
- `tokenType` med værdien `Bearer`
- `expiresAtUtc`
- Rollen `Admin`
- Brugerens permissions

Passwordet skal stå på en enkelt linje, og JSON-strengen skal have både start- og slutcitationstegn.

## 8. Test et beskyttet endpoint

Kopiér midlertidigt `accessToken` fra login-responsen:

```http
@token = INDSÆT-ACCESS-TOKEN

GET {{Auktionshuset.Api_HostAddress}}/api/lots
Accept: application/json
Authorization: Bearer {{token}}
```

Det forventede resultat er `200 OK`. Uden `Authorization`-headeren forventes `401 Unauthorized`, når `Security:EnforceAuthorization` er `true`.

## 9. Oprydning før commit

- Fjern det rigtige password fra `.http`-filen.
- Fjern en eventuelt indsat access token fra `.http`-filen.
- Behold kun placeholders som `INDSÆT-EGET-PASSWORD` og `INDSÆT-ACCESS-TOKEN`.
- Kontrollér Git Changes eller `git diff`, før der committes.

En token, der kun vises i Visual Studios response-vindue, bliver ikke skrevet ind i `.http`-filen og kræver ingen oprydning i repositoryet.

## Typiske fejl

### `HTTP0012: Unable to evaluate expression 'host'`

Requesten bruger `{{host}}`, men projektets variabel hedder `{{Auktionshuset.Api_HostAddress}}`. Brug det fulde eksisterende variabelnavn.

### `400 Failed to read LoginRequest as JSON`

Kontrollér manglende citationstegn, kommaer og utilsigtede linjeskift i email eller password.

### Visual Studio viser kun `An error occurred while sending the request`

Kontrollér, at API'et faktisk viser `Now listening on: https://localhost:7061`. Stop en gammel debug-proces med `Shift+F5`, og start API'et igen.

### Signing key skal være mindst 32 bytes

Kør kommandoerne i afsnittet om signing key igen, og genstart API'et. En ny signing key gør tidligere tokens ugyldige.
