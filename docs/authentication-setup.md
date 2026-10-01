# Lokal opsætning af login

Frontendens login bruger backendens `POST /api/auth/login`. Frontenden udsteder
ikke JWT'er og gemmer ikke credentials i repositoryet.

## API-hemmeligheder

Konfigurér API-projektets secrets lokalt med .NET User Secrets (eller tilsvarende
secret manager). Brug en tilfældig signing key på mindst 32 UTF-8 bytes.

```text
dotnet user-secrets init --project Auktionshuset.Api
dotnet user-secrets set "Authentication:Issuer" "<lokal issuer>" --project Auktionshuset.Api
dotnet user-secrets set "Authentication:Audience" "<lokal audience>" --project Auktionshuset.Api
dotnet user-secrets set "Authentication:SigningKey" "<tilfældig nøgle på mindst 32 bytes>" --project Auktionshuset.Api
dotnet user-secrets set "Authentication:BootstrapAdmin:Email" "<lokal admin-email>" --project Auktionshuset.Api
dotnet user-secrets set "Authentication:BootstrapAdmin:Password" "<lokal admin-adgangskode>" --project Auktionshuset.Api
```

Alternativt kan de samme nøgler sættes som miljøvariabler med `__` i stedet
for `:` (for eksempel `Authentication__SigningKey`). Del ikke værdierne eller
commit dem. `Authentication:AccessTokenLifetimeMinutes` er valgfri; standarden
er 60 minutter. `Api:BaseUrl` i frontendens konfiguration skal pege på API'et.

`ConfiguredAuthUserStore` opretter den konfigurerede bootstrap-bruger med
rollen `Admin` og de permissions, som brugerstoret eksplicit tildeler. Det er
ikke en registreringsfunktion eller en ny brugerdatabase.

## Session

Frontendcookie indeholder et uigennemsigtigt ticket-id. Serverens
`ServerTicketStore` opbevarer ticket, JWT og udløbstid; JWT'en returneres ikke
til browseren. Ticketstoret er proceslokalt, så en genstart ugyldiggør aktive
sessioner. Ved flere frontendinstanser skal storet erstattes med et delt,
sikkert ticket-store (og fælles Data Protection-nøgler konfigureres).
