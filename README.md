# UCL Team 1, 4 Semester - Auktionshuset
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

```bash

git clone [repository-url]

Åben projekt i Visual Studio

Tilføj user secret i package manager console med dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=my-server;Database=my-db;User Id=user;Password=password;"

Launch-projektet skal være AuktionsHuset.API

Default-projektet i package manager console skal være AuktionsHuset.Infrastructure

I package manager console kør `Update-Database`

Kør multilaunch på AuktionsHuset og AuktionsHuset.API

```

Brugere i systemet:
- Brugernavn: DevUser   - Password: DevUser123!
- Brugernavn: Employee  - Password: Medarbejder123!
- Brugernavn: Test      - Password: Test123!

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
Connection string ligger i secrets eller Docker environment variables.
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
