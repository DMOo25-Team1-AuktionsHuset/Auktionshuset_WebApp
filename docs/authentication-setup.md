# Opsæt login fra bunden

Følg guiden på din egen computer, efter du har hentet projektet.
Du opretter en lokal konto, som du kan bruge til at logge ind under udvikling.

## 1. Generér en signing key

Åbn **PowerShell**, og kør:

$keyBytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($keyBytes)
$rng.Dispose()
[Convert]::ToBase64String($keyBytes)

Kopiér resultatet. Du skal bruge det i næste trin.
Signing key bruges af API'et til login-tokens; det er ikke din adgangskode.

## 2. Åbn User Secrets i Visual Studio

1. Find projektet **Auktionshuset.Api** i Solution Explorer.
2. Højreklik på projektet.
3. Vælg **Manage User Secrets** (Administrer brugerhemmeligheder).

Visual Studio åbner filen `secrets.json`. Indstillingerne i denne fil gemmes
kun på din computer og følger ikke med Git.

## 3. Indsæt database- og login-indstillinger

Her bruges feltnavne med kolon, så eksemplet har samme format som teamets
secrets.json. Kopiér eksemplet, hvis din fil er tom:

    {
      "ConnectionStrings:DefaultConnection": "Host=DATABASESERVER;Port=5432;Database=auction_db;Username=DATABASEBRUGER;Password=DATABASEADGANGSKODE",
      "Authentication:Issuer": "Auktionshuset.Api",
      "Authentication:Audience": "Auktionshuset.Web",
      "Authentication:SigningKey": "INDSÆT NØGLEN FRA POWERSHELL HER",
      "Authentication:BootstrapAdmin:Email": "udvikler@example.com",
      "Authentication:BootstrapAdmin:Password": "VÆLG DIN EGEN ADGANGSKODE HER"
    }

- **ConnectionStrings:DefaultConnection:** Få den korrekte forbindelse til teamets database fra den ansvarlige via en privat kanal. Eksemplets server, databasebruger og adgangskode er pladsholdere. Computeren skal have netværksadgang til databaseserveren.
- **Authentication:SigningKey:** Indsæt din egen genererede nøgle fra trin 1.
- **Authentication:BootstrapAdmin:Email:** Skriv din egen login-email.
- **Authentication:BootstrapAdmin:Password:** Vælg din egen login-adgangskode. Det er ikke databaseadgangskoden.
- **Authentication:Issuer** og **Authentication:Audience:** Behold værdierne fra eksemplet.

Hvis filen allerede har indstillinger, fx RabbitMQ eller databaseforbindelsen,
skal du beholde dem og kun tilføje de manglende felter inden for de samme ydre
krølleparenteser. Behold en eksisterende gyldig signing key. Hvert feltnavn må
kun optræde én gang. Der skal være komma mellem felterne, men ikke efter det
sidste felt.

.NET understøtter også indlejrede sektioner, men brug én skrivemåde for hvert
felt. Tilføj fx ikke både en indlejret Authentication-sektion og de samme
Authentication-felter med kolon.

Del kun den nødvendige databaseforbindelse med de relevante teammedlemmer.
Hver udvikler vælger sin egen signing key og sit eget bootstrap-login; det er
ikke nødvendigt at kopiere en andens komplette secrets.json. Ingen rigtige
secrets skal lægges i repositoryet.

Gem filen med **Ctrl+S**.

## 4. Start begge projekter

1. Højreklik på solutionen i Solution Explorer, og vælg **Configure Startup Projects** / **Set Startup Projects**.
2. Vælg flere startup-projekter, og sæt både **Auktionshuset.Api** og **Auktionshuset** til **Start**.
3. Brug **https**-profilen til API'et. Den bruger adressen `https://localhost:7061`, som frontenden er konfigureret til at kontakte.
4. Start solutionen. Hvis projekterne allerede kørte, skal du stoppe og starte dem igen.

Guiden forudsætter, at projektets database og øvrige tjenester er sat
op som beskrevet i README. Trinene her opsætter selve login-funktionen.

## 5. Log ind

Åbn frontendens login-side, og indtast den **email** og **adgangskode**, du skrev
under `BootstrapAdmin` i trin 3.

Din lokale konto har rollen **SuperAdmin** og alle permissions.

## Næste gang

Du skal kun lave opsætningen én gang på din computer. Fremover starter du blot
projekterne og logger ind. Dine User Secrets bliver liggende, når du henter ny
kode. Del dem ikke, og læg dem ikke i Git.

Hver udvikler følger guiden på sin egen computer og vælger sit eget login.
Systemet understøtter i øjeblikket én konfigureret bootstrap-konto pr.
installation. Der er endnu ingen brugerregistrering eller invitationer.

## Hvis login ikke virker

- **Forkert email eller adgangskode:** Kontrollér værdierne under `BootstrapAdmin`, gem filen, og genstart API'et.
- **Fejl om manglende indstillinger:** Kontrollér, at du har lagt hele `Authentication`-sektionen i User Secrets for **Auktionshuset.Api**.
- **Fejl om signing key:** Kopiér hele resultatet fra trin 1 ind i `SigningKey`.
- **Frontend kan ikke kontakte API'et:** Kontrollér, at begge projekter kører, og at API'et bruger **https**-profilen på `https://localhost:7061`.
