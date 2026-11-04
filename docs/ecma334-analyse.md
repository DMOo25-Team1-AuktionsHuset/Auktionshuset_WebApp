# ECMA-334: Analyse af Fixes/iso-ecma

Dato: 4. november 2026. Branch: `Fixes/iso-ecma`. Undersøgt HEAD: `88cf466e1c9d7a7a018fc049a86e95ba724ba280`.

## Resultat og aftalt afgrænsning

**Der er ikke fundet dokumenterede normbrud, som begrunder kodeændringer inden for det aftalte omfang. Rettelseslisten er derfor tom.** Dette er en afgrænset undersøgelses konklusion, ikke en formel certificering af programmet, compileren eller alle mulige kørselsforløb.

Brugeren har præciseret:

- Analysen gælder `Fixes/iso-ecma`, al egen C#-kode og eksisterende tests.
- Kun den vedhæftede ECMA-334 vurderes. Generelle kodekvalitetsanbefalinger indgår ikke.
- Nyere C# skal bevares; kun dokumenterede normbrud må blive rettelsesopgaver. Der er ikke bestilt en omskrivning til C# 7.3.
- Build og eksisterende tests er tilladt. Kildekoden må ikke ændres under analysen.

Kildekode, projektkonfiguration, pakker og migrationskode er ikke ændret. Leverancen består af denne analyse, [instruktioner til den næste AI](ecma334-ai-instruktioner.md) og opdaterede HUMAN-checkpoints i `.agent`. Ingen commit eller push.

## Vurderingsgrundlag

Den lokale kilde er `C:\Users\megan\Downloads\ECMA-334_7th_edition_december_2023.pdf`, 699 PDF-sider. SHA-256: `7797c1d995dd7791683cb4f2dfd758cc1bfd22150b5234487fffe43e9b3e3ab0`.

PDF'en behandles som faglig kildetekst. Dens eksempler og formuleringer er ikke instruktioner til at ændre brugerens projekt eller tilsidesætte brugerens afgrænsning.

De afgørende skel er:

1. **Sprogspecifikation og kvalitetsregler:** §1, trykt side 1/PDF-side 29, beskriver syntaks, begrænsninger og semantik. Standarden opstiller ikke generelle krav om eksempelvis SOLID, Clean Architecture, navngivningsstil, testdækning eller en bestemt klammeformatering.
2. **Normative krav og vejledning:** §4, trykt side 7/PDF-side 35, skelner mellem normative bestemmelser og informativ tekst. Eksempler og noter må ikke alene gøres til bindende rettelseskrav.
3. **Conforming og strictly conforming:** §5, trykte sider 9-10/PDF-sider 37-38, tillader udvidelser i et program, der accepteres af en conforming implementation. Streng overensstemmelse har yderligere begrænsninger. Brugeren har valgt at bevare nyere C# og har dermed ikke bestilt opfyldelse af den strengere sprogdelmængde.
4. **Program og implementation:** Krav, der retter sig mod en C#-implementation, er ikke uden videre fejl i applikationens kildekode. Compileren er ikke blevet conformance-testet i denne undersøgelse.

[Microsofts oversigt over compilerens sprogindstillinger](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/language#c-language-specification) knytter ECMA-334, 7. udgave, til C# 7.3. [Microsofts versionsoversigt](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-versioning#defaults) angiver C# 14 som standard for .NET 10. Disse kilder bruges til at forstå værktøjernes versioner, ikke som yderligere kvalitetsstandarder. [Ecmas egen udgivelsesside](https://ecma-international.org/publications-and-standards/standards/ecma-334/) bekræfter udgave og dokumentets formål.

## Omfang og metode

Alle ni projekter i `AuktionsHuset_WebApp.slnx` er medtaget. Kildeinventaret blev dannet med `rg --files -g '*.cs' -g '*.razor'`, med repositoryets ignore-regler. `bin`, `obj`, midlertidige mapper og hemmelige konfigurationskilder blev ikke gennemsøgt som kildekode.

| Projekt | C#-filer |
| --- | ---: |
| Auktionshuset | 31 |
| Auktionshuset.Api | 64 |
| Auktionshuset.Api.Test | 28 |
| Auktionshuset.Application | 72 |
| Auktionshuset.Contracts | 58 |
| Auktionshuset.Domain | 20 |
| Auktionshuset.Infrastructure | 116 |
| Auktionshuset.Infrastructure.Test | 4 |
| Auktionshuset.Tests | 25 |
| **I alt** | **418** |

Derudover indgår **29 Razor-filer** gennem frontendens normale Razor/C#-build. Af C#-filerne er 327 produktionsfiler uden for migrationsmappen, 57 testfiler og 34 migrations-/modelfiler.

Undersøgelsen kombinerer:

- Roslyn-syntakstræer for alle 418 C#-filer, optælling af udvalgte sprogkonstruktioner og en kontrol af identifieres Unicode-normalisering. Ingen parsefejl i den maskinelle syntakskontrol.
- Målrettet manuel kontrol af projektindstillinger, begge `Program.cs`, repræsentative entiteter, DTO'er, handlers, services, Razor-komponenter og de konkrete steder med buildadvarsler. Det er ikke en manuel linje-for-linje-gennemgang af samtlige filer.
- Normal fuld genbygning af alle projekter og kørsel af alle tre eksisterende testsuiter med standardmiljøets opt-in-tests deaktiveret.
- En supplerende sprogversionsprobe med C# 7.3 i alle ni projekter. Denne probe var diagnostisk og ændrede ingen projektfiler. Den bruges ikke som en rettelsesliste.
- Læsning af relevante hele PDF-sider; centrale sider om conformance og grammatik blev også renderet og visuelt kontrolleret.

Alle projekter angiver `net10.0`, `Nullable=enable` og `ImplicitUsings=enable`. Der blev ikke fundet en eksplicit `LangVersion` i projektfiler eller `Directory.Build.props`. En MSBuild-forespørgsel på Domain-projektet returnerede `LangVersion=14.0`. Anvendt SDK: 10.0.402.

## Fund og klassifikation

### A. Dokumenterede normbrud: ingen fundet

Ingen observation opfylder samlet kravene om en relevant normbestemmelse, konkret afvigende projektkode og evidens, når brugerens tilladelse til nyere C# respekteres. Der foreslås derfor ingen aktiv kodeændring under ECMA-334-opgaven.

Det vellykkede build alene beviser ikke fuld standardoverensstemmelse. Blandt andet kræver §6.4.3 ikke en diagnosticering af alle normaliseringsproblemer, og buildet undersøger ikke alle runtime-forløb. Derfor blev relevante supplerende kontroller foretaget, og konklusionen begrænses til de dokumenterede fund.

### B. Nyere sprogfunktioner: bevares efter brugerens valg

Følgende er eksempler på observerede udvidelser i forhold til den vedhæftede udgaves sprogdelmængde. Tabellen er **ikke en fejl- eller rettelsesliste**. Tallene er syntaktiske forekomster i de 418 C#-filer, inklusive tests og migrationer; Razor-kode er ikke med i disse optællinger.

| Konstruktion | Forekomster / filer | Kontrolleret eksempel |
| --- | ---: | --- |
| Filbaserede namespaces | 181 / 181 | `Auktionshuset.Domain/AuctionHouseDefaults.cs:1` |
| Records | 101 / 81 | `Auktionshuset.Application/Billing/InvoiceAmounts.cs:3` |
| Primære konstruktører på almindelige klasser/structs | 113 / 96 | `Auktionshuset/Services/SessionReconnectPolicy.cs:5` |
| `required`-medlemmer | 98 / 19 | `Auktionshuset.Domain/Entities/RefreshToken.cs:10` |
| `init`-accessorer | 124 / 23 | `Auktionshuset.Api/Security/AuthUser.cs:5` |
| Target-typed `new` | 186 / 62 | `Auktionshuset.Application/Billing/InvoiceAmounts.cs:13` |
| Collection expressions | 297 / 91 | `Auktionshuset.Domain/Entities/AuctionInvoice.cs:35` |
| Switch expressions | 22 / 17 | `Auktionshuset.Infrastructure/Service/Lots/PostgresLotImageStore.cs:12` |
| `using`-deklarationer uden `await` | 466 / 53 | `Auktionshuset.Infrastructure/Service/Lots/PostgresLotImageStore.cs:20` |
| `await using`-deklarationer | 111 / 38 | `Auktionshuset.Application/Admin/Lots/UpdateLot/UpdateLotHandler.cs:19` |
| Null-forgiving expressions | 214 / 61 | `Auktionshuset.Domain/Entities/Lot.cs:7` |

Der findes også nyere patterns, ranges, raw strings, UTF-8-litteraler, `with` og top-level statements. Top-level statements findes i begge `Program.cs`. Denne udvidelsesbrug er en grund til ikke at erklære projektet strengt overensstemmende med udgavens sprogdelmængde; den er ikke i sig selv belæg for at rette koden under brugerens valgte afgrænsning, jf. §5.

En syntaxbaseret søgning på `T?` kan ikke alene skelne nullable reference types fra standardens nullable value types (§8.3.12). Eksempelvis må `Guid?` og `decimal?` ikke udpeges som normbrud blot på grund af spørgsmålstegnet. En sådan automatisk rettelse er ikke en del af leverancen.

### C. To compileradvarsler: ikke dokumenterede ECMA-brud

| Sted | Observation | Vurdering inden for opgaven |
| --- | --- | --- |
| `Auktionshuset.Infrastructure/Service/Auctions/PostgresAuctionWriteSessionFactory.cs:7` | CS0105: samme namespace er importeret på linje 4 og 7. | §14.5.1 og §14.5.3 opstiller ikke et generelt forbud mod gentaget namespace-import. Dette er ikke et dubleret alias, som har en særskilt unikhedsregel i §14.5.2. Ingen ECMA-rettelsesopgave oprettes. |
| `Auktionshuset/Components/Pages/Warehouse.razor.cs:39` | CS8625: en referenceegenskab initialiseres med `default` under aktiveret nullable-analyse. | §9.3 og §12.8.20 definerer referenceværdiens default som `null`. Den konkrete advarsel kommer fra nullable reference type-analysen, som ligger uden for den vedhæftede udgaves sprogdelmængde. Observationen dokumenterer ikke et brud på denne standard. Ingen ECMA-rettelsesopgave oprettes. |

Advarslerne er ikke dermed erklæret uvigtige i enhver sammenhæng. De er blot ikke godkendte rettelsesopgaver under den valgte standardafgrænsning. Der gives ingen generelle forbedringsforslag her.

### D. Supplerende normkontroller

- **Unicode NFC, §6.4.3, trykt side 21/PDF-side 49:** Ingen identifier-tokenværdier uden NFC-normalisering fundet i de 418 C#-filer. Alle 29 Razor-filers tekst er NFC-normaliseret, og der er ingen `\u`-/`\U`-escapes i disse filer. Razor-resultatet er en tekstkontrol, ikke en selvstændig fuld Razor-semantikanalyse.
- **Syntaks, typer og definite assignment:** Den normale compiler accepterer projektet uden fejl. Der er ikke fundet en konkret overtrædelse af blandt andet §9.4's definite-assignment-regler. Dette er en compilerstøttet kontrol, ikke et uafhængigt bevis for compilerens overensstemmelse med alle kapitler.
- **Unsafe-kode og direktiver:** Den målrettede søgning fandt ingen brug af `unsafe`/`stackalloc` eller aktive `#error`/`#warning` i egen kode. Migrationsfiler indeholder `#pragma warning` og `#nullable`; sidstnævnte er en bevaret udvidelse. Fraværet af disse konstruktioner er ikke en kvalitetsvurdering af projektets øvrige funktionalitet.

## Verifikation

Den afsluttende normale genbygning anvendte:

```powershell
dotnet build AuktionsHuset_WebApp.slnx --no-restore --nologo -m:1 -t:Rebuild -v:minimal
```

Resultat: **bestået, 0 fejl, 2 advarsler**, specificeret ovenfor. Normal konfiguration er anvendt igen efter sprogversionsproberne; ingen sprogversion er gemt i projektet.

Eksisterende tests blev kørt med følgende fælles argumenter; der blev desuden gemt TRX-resultater uden for repositoryet:

```powershell
dotnet test Auktionshuset.Tests/Auktionshuset.Tests.csproj --no-build --no-restore --nologo
dotnet test Auktionshuset.Api.Test/Auktionshuset.Api.Test.csproj --no-build --no-restore --nologo
dotnet test Auktionshuset.Infrastructure.Test/Auktionshuset.Infrastructure.Test.csproj --no-build --no-restore --nologo
```

| Testsuite | Bestået | Fejlet | Oversprunget |
| --- | ---: | ---: | ---: |
| Auktionshuset.Tests | 226 | 0 | 0 |
| Auktionshuset.Api.Test | 144 | 0 | 23 |
| Auktionshuset.Infrastructure.Test | 11 | 0 | 0 |
| **I alt** | **381** | **0** | **23** |

De 23 oversprungne API-tests består af 22 PostgreSQL-opt-in-tests og én browserhost-test. Deres opt-in er defineret i `AuctionStockPostgresTests.cs:23` og `BrowserAcceptanceHost.cs:26`. PostgreSQL-testene kræver `AUCTION_STOCK_TEST_CONNECTION`; browserhosten kræver `AH_BROWSER_ACCEPTANCE=1`. De er ikke verificeret i denne undersøgelse. Testresultaterne er en eksisterende funktionsbaseline, ikke en ECMA-conformance-testsuite.

Sprogversionsproberne anvendte midlertidige CLI-overrides:

```powershell
dotnet build <projekt.csproj> --no-restore --nologo -m:1 -p:LangVersion=7.3 -p:Nullable=disable -p:ImplicitUsings=disable -p:BuildProjectReferences=false -v:minimal
```

Alle ni projekter blev afvist i denne probe. Der forekom både featurediagnoser og parser-/følgefejl, og de midlertidigt deaktiverede implicitte imports kan selv skabe manglende-typenavne-fejl. Derfor må hverken fejlantal eller alle fejllinjer bruges som selvstændige normfund. Ændringen af compilerindstillinger er diagnostisk; den beviser ikke fuld standardoverensstemmelse eller et behov for at nedgradere projektet.

Build og tests blev kørt med den nødvendige godkendte adgang uden for sandboxen efter, at sandboxen havde begrænset .NET-kørslen. Der blev ikke tilføjet pakker eller ændret afhængigheder.

Rå buildlogs, TRX-filer, syntaksinventar og hjælpeværktøjet ligger i analysesessionens mappe uden for repositoryet:

`C:\Users\megan\.codex\visualizations\2026\11\04\01a1a5af-bb7a-7830-a620-f6fdd63ec3ae`

## Begrænsninger og overdragelse

Der er ikke udført formelt bevis for alle runtime-forløb, uafhængig compiler-certificering, produktionsdeployment eller den oversprungne PostgreSQL-/browseracceptance. Tredjepartsbiblioteker er ikke blevet standardcertificeret. Arkitektur, sikkerhed, forretningsregler og generel vedligeholdelighed er ikke vurderet som selvstændige kvalitetskrav i denne opgave.

Den næste AI skal bruge [overdragelsesinstruktionerne](ecma334-ai-instruktioner.md). Den korrekte handling ved den undersøgte revision er at undlade kodeændringer, medmindre AI'en kan dokumentere et yderligere relevant normbrud. Brugeren har ikke bedt om, at en ikke-tom rettelsesliste skal konstrueres, hvis der mangler belæg.
