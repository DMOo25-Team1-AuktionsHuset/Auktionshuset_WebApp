# Kom i gang med Auktionshuset på din Windows-pc

Denne guide er til dig, der har hentet projektet, men aldrig har brugt Vault.
Følg trinene i rækkefølge. Kommandoerne i denne guide køres på **din Windows-pc**.

Du har allerede fået certifikatet og adgang til teamets VPN. Vault kører på vores
fælles Linux-server. API'en bruger Vault til at hente databasebrugernavn og
databaseadgangskode. Du behøver hverken installere Vault eller logge ind på dens
webside for at køre projektet.

**Status for denne version:** Kun databaseoplysningerne hentes fra Vault.
RabbitMQ og appens login kræver stadig lokal opsætning i trin 5. Denne guide
ændrer ikke, hvor applikationen henter de øvrige hemmeligheder.

## 1. Hav dette klar

- Projektet fra Git og .NET 10 SDK. Kommandoen `dotnet --list-sdks` skal vise en
  linje med `10.0.xxx`. Brug en Visual Studio-version med .NET 10-support.
- Teamets VPN skal være forbundet.
- Teamets CA-certifikat skal være installeret som betroet på din pc. En kopi af
  certifikatfilen på skrivebordet er ikke nok.
- Du skal kende projektets mappe på din pc og kunne finde solutionen
  `AuktionsHuset_WebApp.slnx`.

Bed den projektansvarlige om disse oplysninger:

| Oplysning | Hvad bruger du den til? |
| --- | --- |
| Vaults HTTPS-adresse | Adressen på vores fælles Vault, inklusive port. Brug den nøjagtige adresse, der passer til certifikatet. |
| Backendrollens RoleID | Fortæller Vault, hvilken backendrolle API'en logger ind som. |
| Dit personlige engangstoken (`wrapping_token`) | Bruges én gang til at hente dit SecretID. Bed først om det, når du er klar til trin 4. |
| RabbitMQ-host, port, brugernavn og adgangskode | Skal bruges i trin 5 i den nuværende version. Brugernavn og adgangskode leveres fortroligt. |

Projektansvarliges del af opsætningen står i
[vejledningen til udlevering af adgang](vault-admin-onboarding.md).

### De tre slags login

| Begreb | Betydning |
| --- | --- |
| Engangstoken / `wrapping_token` | En kortlivet afhentningskode, som du får af den projektansvarlige. Den bliver brugt af opsætningsscriptet. |
| SecretID / `VAULT_SECRET_ID` | API'ens adgang til Vault. Scriptet henter og gemmer denne værdi på din pc. |
| Appens email og adgangskode | Dit login til Auktionshusets hjemmeside. Det opretter du i trin 5. |

Du skal ikke bruge administratorens Vault-token, root-token eller unseal-nøgler.
Engangstokenet er fortroligt, mens det er gyldigt. Gem det ikke i en fil i projektet,
en Git-commit eller et screenshot til gruppechatten.

## 2. Åbn PowerShell i den rigtige mappe

1. Åbn projektets rodmappe i Stifinder. Det er mappen med
   `AuktionsHuset_WebApp.slnx`, `docs` og `scripts`.
2. Klik i Stifinders adresselinje.
3. Skriv `powershell`, og tryk **Enter**.
4. Kør denne kommando i det nye vindue:

   ```powershell
   Test-Path .\scripts\Initialize-VaultEnvironment.ps1
   ```

Resultatet skal være `True`. Hvis det er `False`, står du i en forkert mappe,
eller du mangler at hente den seneste kode fra Git.

Lad PowerShell-vinduet være åbent til de næste trin.

## 3. Kontrollér forbindelsen til Vault

Åbn den HTTPS-adresse, du har fået, i din browser med `/ui/` til sidst.
Eksempel, som du skal erstatte med teamets faktiske adresse:

```text
https://VAULT-HOST:8200/ui/
```

Du skal kunne se Vaults login-side, og browseren skal acceptere certifikatet.
**Du skal ikke logge ind her.** Siden bruges kun til at kontrollere forbindelsen.

Hvis siden ikke åbner, eller browseren viser en certifikatadvarsel, skal du
kontakte den projektansvarlige. Kontrollér VPN, hostnavn og certifikat, før du
fortsætter. Certifikatkontrollen skal forblive slået til.

## 4. Hent og gem din personlige Vault-adgang

Når PowerShell er åbent, og forbindelsen er kontrolleret, beder du den
projektansvarlige om **dit personlige engangstoken**. Aftal levering gennem
teamets fortrolige kanal. Tokenet gælder normalt kun i 10 minutter fra udstedelsen.

Kør følgende i PowerShell-vinduet fra trin 2:

```powershell
.\scripts\Initialize-VaultEnvironment.ps1
```

Scriptet spørger om tre ting, én ad gangen:

1. **Vaults HTTPS-adresse:** Indsæt adressen fra den projektansvarlige. Her skal
   `/ui/` ikke med. Eksempel: `https://VAULT-HOST:8200`.
2. **RoleID:** Indsæt backendrollens RoleID, og tryk **Enter**.
3. **Engangstoken:** Indsæt kun værdien fra `wrapping_token`, og tryk **Enter**.
   Indtastningen er skjult; du ser ikke selve tokenet. Indsæt ikke hele
   terminaludskriften, feltnavnet eller anførselstegn.

Scriptet kontrollerer, at tokenet kommer fra backendrollens SecretID-udstedelse,
pakker det ud én gang og gemmer tre miljøvariabler for din Windows-bruger:

```text
VAULT_ADDR
VAULT_ROLE_ID
VAULT_SECRET_ID
```

SecretID'et bliver ikke vist. Det gemmes som en brugermiljøvariabel, som den
nuværende API allerede understøtter. Miljøvariabler er ikke krypteret lagring;
opsætningen er til vores udviklingsmiljø.

Når du ser **"Vault-opsætningen er gemt for din Windows-bruger"**, er dette trin
færdigt. Det bekræfter, at adgangen er gemt; selve API-login testes ved opstart.

**Luk alle Visual Studio-vinduer. Åbn derefter Visual Studio igen fra
Start-menuen**, og åbn solutionen. Et allerede åbent Visual Studio får ikke
automatisk de nye miljøvariabler.

Hvis Windows blokerer scriptet med en besked om execution policy eller signering,
så send selve fejlbeskeden til den projektansvarlige. Guiden kræver ikke, at du
slår maskinens scriptbeskyttelse fra.

## 5. Opsæt RabbitMQ og dit login til appen

Dette trin er stadig nødvendigt, fordi koden endnu kun henter databaseoplysninger
fra Vault. Der skal ikke tilføjes en databaseadgangskode eller en
`ConnectionStrings:DefaultConnection` på din pc.

1. Find **Auktionshuset.Api** i Visual Studios **Solution Explorer**.
2. Højreklik på projektet, og vælg **Manage User Secrets** /
   **Administrer brugerhemmeligheder**.
3. Visual Studio åbner en lokal `secrets.json`. Denne fil ligger uden for
   projektmappen og følger ikke med Git.
4. Hvis filen er tom, kan du kopiere skabelonen nedenfor. Hvis den allerede har
   indstillinger, skal du tilføje de manglende felter inden for samme ydre `{ }`.
   Hvert feltnavn skal kun stå én gang. Der skal være komma mellem felterne.

```json
{
  "RabbitMQ:HostName": "RABBITMQ-HOST-FRA-PROJEKTANSVARLIG",
  "RabbitMQ:Port": 5672,
  "RabbitMQ:UserName": "DIT-UDLEVEREDE-RABBITMQ-BRUGERNAVN",
  "RabbitMQ:Password": "DIN-UDLEVEREDE-RABBITMQ-ADGANGSKODE",
  "Authentication:SigningKey": "DIN-GENEREREDE-NØGLE-FRA-NÆSTE-AFSNIT",
  "Authentication:BootstrapAdmin:Email": "DIN-VALGTE-LOGIN-EMAIL",
  "Authentication:BootstrapAdmin:Password": "DIN-VALGTE-LOGIN-ADGANGSKODE"
}
```

Erstat alle pladsholdere. Brug RabbitMQ-porten fra den projektansvarlige; `5672`
er kun et eksempel. Vælg din egen email og adgangskode til appens lokale
bootstrap-konto. Den nuværende kode giver denne konto rollen **SuperAdmin**.
Kopier ikke en andens fulde `secrets.json`.

### Opret din lokale signing key

En signing key er en tilfældig nøgle, API'en bruger til login-tokens.
Behold en eksisterende gyldig nøgle. Hvis du mangler en, kører du i PowerShell:

```powershell
$signingBytes = New-Object byte[] 32
$signingGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$signingGenerator.GetBytes($signingBytes)
$signingGenerator.Dispose()
[Convert]::ToBase64String($signingBytes)
```

Kopiér resultatet til `Authentication:SigningKey` i din lokale User Secrets-fil.
Gem ikke resultatet i projektet eller gruppechatten. Gem filen med **Ctrl+S**.
Issuer og audience kan beholde værdierne fra projektets almindelige konfiguration.

Når RabbitMQ og login senere hentes fra Vault, skal dette trin opdateres og de
overflødige lokale værdier fjernes.

## 6. Byg og start de to projekter

1. Vælg **Build → Build Solution** i Visual Studio. Build skal gennemføres uden fejl.
2. Højreklik på solutionen, og vælg **Configure Startup Projects** /
   **Set Startup Projects**.
3. Vælg flere startup-projekter. Sæt **Auktionshuset.Api** og **Auktionshuset** til
   **Start**. Navnene ligner hinanden: det første er API'en, det andet er hjemmesiden.
4. Vælg **https**-profilen til begge projekter.
5. Start solutionen.

API'en bruger `https://localhost:7061`, og hjemmesiden bruger
`https://localhost:7063`. De adresser kommer fra projektets launch-profiler.
Hvis Visual Studio spørger om at stole på ASP.NET Cores lokale udviklingscertifikat,
er det certifikatet til din lokale app. Det er separat fra teamets Vault-certifikat.

Ved API-opstart hentes databaseoplysningerne fra Vault. API'en migrerer og seeder
også databasen. Start mod den aftalte udviklingsdatabase. PostgreSQL skal tillade
din pc's VPN-adresse, og den konfigurerede RabbitMQ-server skal være tilgængelig.
Den projektansvarlige håndterer servernes adgangsregler.

## 7. Kontrollér, at det virker

1. Åbn `https://localhost:7063`, hvis hjemmesiden ikke åbner automatisk.
2. Gå til login-siden.
3. Log ind med den email og adgangskode, du skrev under `BootstrapAdmin` i trin 5.
4. Åbn en side, som viser data fra API'en, eksempelvis lageret.
5. Kontrollér, at API'en fortsat kører uden forbindelsesfejl.

Dit website-login er ikke RoleID, SecretID eller engangstokenet.

## Næste gang du arbejder på projektet

Forbind VPN, og start projekterne. Du skal ikke hente et nyt engangstoken ved
hver opstart. Dit SecretID bliver liggende på din pc og gælder efter rollens
levetid, som teamet har valgt til 30 dage. Den faktiske levetid afhænger af
rolleindstillingerne på udstedelsestidspunktet.

Ved udløb eller tilbagekaldelse beder du om et nyt personligt engangstoken og
gentager trin 4. Dine eksisterende miljøvariabler bliver erstattet.

## Hvis noget fejler

| Fejl eller symptom | Hvad gør du? |
| --- | --- |
| `Test-Path` viser `False` | Find mappen med solutionen, eller hent den seneste kode. |
| Vaults login-side kan ikke åbnes | Kontrollér VPN og den udleverede adresse. Kontakt den projektansvarlige. |
| Certifikatfejl i browser eller script | Kontrollér det installerede CA-certifikat og det præcise hostnavn. Få hjælp; behold certifikatkontrollen. |
| Engangstoken er udløbet eller allerede brugt | Bed om et nyt. Prøv ikke at pakke samme token ud i både browser og script. |
| Tokenet er oprettet til en anden sti | Kontakt den projektansvarlige. Scriptet pakker det ikke ud. |
| `VAULT_ADDR mangler` | Luk alle Visual Studio-vinduer, og åbn fra Start-menuen. Kontrollér, at trin 4 blev gennemført under din Windows-bruger. |
| AppRole-login giver `invalid role or secret ID` | Bed den projektansvarlige kontrollere RoleID, SecretID, udløb og eventuelle IP-begrænsninger. |
| Vault giver `permission denied` ved læsning | Backendrollens policy skal kontrolleres af den projektansvarlige. |
| PostgreSQL giver `no pg_hba.conf entry` | Send fejlbeskeden til den projektansvarlige. Din VPN-adresse, databasebruger og eventuelle TLS-krav skal passe til serverreglerne. |
| RabbitMQ-forbindelsen fejler | Kontrollér de udleverede host-, port- og loginværdier samt VPN. `localhost` betyder din egen pc. |
| Fejl om `Authentication:SigningKey` | Kontrollér User Secrets på **Auktionshuset.Api**. Brug hele nøglen fra trin 5. |
| Appens login afvises | Brug email og adgangskode fra `BootstrapAdmin`, gem filen, og genstart API'en. |
| Hjemmesiden kan ikke kontakte API'en | Start begge projekter med **https**, og kontrollér API-adressen `https://localhost:7061`. |

Når du beder om hjælp, send fejlbeskeden og det trin, du er nået til.
Fjern tokens, adgangskoder og komplette connection strings fra tekst og screenshots.

## Kilder

- [HashiCorp: Response wrapping](https://developer.hashicorp.com/vault/docs/concepts/response-wrapping)
- [HashiCorp: Wrapping lookup](https://developer.hashicorp.com/vault/api-docs/system/wrapping-lookup)
- [HashiCorp: Wrapping unwrap](https://developer.hashicorp.com/vault/api-docs/system/wrapping-unwrap)
- [Microsoft: User Secrets under udvikling](https://learn.microsoft.com/aspnet/core/security/app-secrets?view=aspnetcore-10.0)
