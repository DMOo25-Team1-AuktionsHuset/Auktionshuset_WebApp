# UCL Team 1 ~ 4 Semester - Auktionshuset
## Formål
Projektet er udarbejdet som en prototype på et auktionssoftware. Formålet er at udarbejde et system med online budgivning og embedded devices til at styre budgivning. 

---
## Features
- [ ] 
- [ ] 
- [ ] 
- [ ] 
- [ ] Authentication og authorization
- [ ] Tests
- [ ] Docker containerisation

---
## Teknologier
- **C# / .NET 10** - Programmeringssprog og framework.
- **ASP.NET Core Web API** - Backend API til at håndtere requests fra client.
- **Blazor WebAssembly** - Frontend client til user interface.
- **Entity Framework Core** - Object-relational mapper brugt til database adgang.
- **Microsoft SQL Server** - Relational database brugt til at opbevare data.
- **ASP.NET Core Identity** - User og  role management.
- **Role-based Authorization** - Access control ved brug af roller som Admin, Employee og Storskaerm.
- **Swagger / OpenAPI** - API dokumentation og test.
- **Dtos** - Brugt til at transporterer data imellem frontend og backend.
- **EF Core Migrations** - Brugt til at håndtere databaseændringer.
- **Docker / Visual Studio Container Tools** - Container support for applikationen.
- **SignalR** - Teknologi til realtidskommunikation. 

---
## Projektstruktur og arkitektur
Projektet er struktureret efter Clean Architecture. Se evt. mere her: [Clean Architecture with ASP.NET Core 10](https://www.youtube.com/live/rjefnUC9Z90) 
Projektets overordnede arkitektur er Event-Driven. Se evt. mere her: [
Building Next-Generation Applications with EDA • Eric Johnson](https://youtu.be/u2lh7pbgcMM?si=8xekc0nTDh5bsRlk)

```txt

AuktionsHuset

│

├── AuktionsHuset                      # Server-side rendering

├── AuktionsHuset.API                  # Controllers & Middleware

├── AuktionsHuset.Application          # Interfaces # Dto

├── AuktionsHuset.Client               # Client-side rendering & UI

├── AuktionsHuset.Client.Test          # Test class

├── AuktionsHuset.Domain               # Entities 

├── AuktionsHuset.Infrastructure       # EF Core, DtoServices & DBcontext

├── AuktionsHuset.Infrastructure.Test  # Test class             

│

├── Dockerfile

├── docker-compose.yml

└── README.md

```

---
## Installation
### Krav
- .NET SDK 10.0
- Docker Desktop
- Visual Studio 2022 og nyere
- Git
---
## Kør projektet lokalt med Visual Studio

Følg [trin-for-trin-vejledningen til teamets Windows-pc'er](docs/team-setup.md).
Den dækker VPN, certifikat, personlig Vault-adgang, de resterende lokale
indstillinger og opstart af **Auktionshuset.Api** og **Auktionshuset**.

Den projektansvarlige bruger [vejledningen til sikker udlevering af Vault-adgang](docs/vault-admin-onboarding.md).

---
## Kør med Docker

```bash

docker compose up --build

```

Applikationen kører på:

```txt

https://localhost:[5001]

https://localhost:[7201]

```

---
## Database
Ved lokal API-opstart hentes databasebrugernavn og adgangskode fra Vault.
Host, port og databasenavn læses fra `Database`-sektionen i API'ens konfiguration.
API'en kører migrationer og seeding ved opstart mod den konfigurerede database.
### Migrations

```bash

dotnet ef migrations add InitialCreate

dotnet ef database update

```

--- 
## Test
Udvalgte klasser er testet på baggrund af, hvor meget funktionalitet, der minder om hinanden. Vi har altså valgt eksemplariske klasser, der ligner andre klasser. Der er foretaget integrationstest og unittest. 

```bash

***dotnet test .\Slottet\Slottet.Client.Test\Slottet.Client.Test.csproj***
***dotnet test .\Slottet\Slottet.API.Test\Slottet.API.Test.csproj***
***dotnet test .\Slottet\Slottet.Infrastructure.Test\Slottet.Infrastructure.Test.csproj***

```

---
## Security
Der er i projektet implementeret login med ASP.Net Identity, hvor der er rollebaseret authentication. Der er brugt SHA512 til password hashing med 210.000 iterationer for sikkerhed. Projektet er derudover sammenlignet med OWASP top 10 sikkerhedsbrister og der er lavet Sonarqube analyser med jævne mellemrum. 

---
## Roadmap

- [ ] Deployment
- [ ] CI/CD pipeline

---
## Læringsmål
Projektet er lavet med følgende læringsmål for øje: 

- ASP.NET Core MVC
- Clean Architecture
- SQL Server
- Entity Framework Core
- Docker
- Lagdeling
- SOLID
- Testbarhed

---
