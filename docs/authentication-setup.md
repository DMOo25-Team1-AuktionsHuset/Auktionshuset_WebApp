# Opsæt login fra bunden

Følg guiden på din egen computer, efter du har hentet projektet.
Du opretter en lokal konto, som du kan bruge til at logge ind under udvikling.

## 1. Generér en signing key

Åbn **PowerShell**, og kør:

[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))

Kopiér resultatet. Du skal bruge det i næste trin.
Signing key bruges af API'et til login-tokens; det er ikke din adgangskode.

## 2. Åbn User Secrets i Visual Studio

1. Find projektet **Auktionshuset.Api** i Solution Explorer.
2. Højreklik på projektet.
3. Vælg **Manage User Secrets** (Administrer brugerhemmeligheder).

Visual Studio åbner filen `secrets.json`. Indstillingerne i denne fil gemmes
kun på din computer og følger ikke med Git.

## 3. Indsæt login-indstillingerne

Hvis filen er tom eller kun indeholder `{}`, indsætter du hele eksemplet:

{
  "Authentication": {
    "Issuer": "Auktionshuset.Api",
    "Audience": "Auktionshuset.Web",
    "SigningKey": "INDSÆT NØGLEN FRA POWERSHELL HER",
    "BootstrapAdmin": {
      "Email": "udvikler@example.com",
      "Password": "VÆLG DIN EGEN ADGANGSKODE HER"
    }
  }
}

Ret disse tre værdier, og behold anførselstegnene omkring dem:

- **SigningKey:** Indsæt resultatet fra trin 1.
- **Email:** Skriv den email, du vil logge ind med.
- **Password:** Vælg din egen adgangskode.

`Issuer` og `Audience` kan stå præcis som i eksemplet.

Hvis filen allerede indeholder fx en databaseforbindelse, skal du beholde den
og tilføje `Authentication` ved siden af med komma mellem felterne:

{
  "ConnectionStrings": {
    "DefaultConnection": "DIN EKSISTERENDE DATABASEFORBINDELSE"
  },
  "Authentication": {
    "Issuer": "Auktionshuset.Api",
    "Audience": "Auktionshuset.Web",
    "SigningKey": "INDSÆT NØGLEN FRA POWERSHELL HER",
    "BootstrapAdmin": {
      "Email": "udvikler@example.com",
      "Password": "VÆLG DIN EGEN ADGANGSKODE HER"
    }
  }
}

Behold din rigtige databaseforbindelse i stedet for teksten i eksemplet.
Hvis du allerede har en `Authentication`-sektion, skal du rette den eksisterende,
så der kun er én. Gem filen med **Ctrl+S**.

## 4. Start begge projekter

1. Højreklik på solutionen i Solution Explorer, og vælg **Configure Startup Projects** / **Set Startup Projects**.
2. Vælg flere startup-projekter, og sæt både **Auktionshuset.Api** og **Auktionshuset** til **Start**.
3. Brug **https**-profilen til API'et. Den bruger adressen `https://localhost:7061`, som frontenden er konfigureret til at kontakte.
4. Start solutionen. Hvis projekterne allerede kørte, skal du stoppe og starte dem igen.

Guiden forudsætter, at projektets database og øvrige tjenester allerede er sat
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
