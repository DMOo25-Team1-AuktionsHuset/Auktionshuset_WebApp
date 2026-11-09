# Komplet ER-diagram – Auktionshuset

Kontrolleret mod repository-koden den 9. november 2026. Diagrammet er udledt af den aktuelle PostgreSQL-model i `AHDBContext`, inkl. Fluent API og EF-konventioner. Modellen matcher migrationssnapshotten. Ingen databaseforbindelse er åbnet; dette dokument bekræfter ikke, at migrationerne er kørt i en konkret database.

**Omfang:** 28 EF-mappede entities/tabeller, 224 kolonner og 33 foreign keys samt den ikke-mappede domæneklasse `Outbox` med fem properties. Alle navigationer er repræsenteret af relationer, ikke af ekstra kolonner.

- [Komplet Mermaid-kode](er-diagram.mmd)
- [Komplet grafik – SVG, kan forstørres uden kvalitetstab](er-diagram.svg)
- [Komplet grafik – PNG](er-diagram.png)
- [Auktion og bud – SVG](auktion-og-bud.svg)
- [Login og fakturering – SVG](login-og-fakturering.svg)
- [Identity – SVG](identity.svg)

## Tegnforklaring og annotationer

| Notation | Betydning |
|---|---|
| `PK` | Primærnøgle. Flere PK-felter i samme tabel udgør én sammensat nøgle. |
| `FK` | Faktisk foreign key i EF-modellen. |
| `UK` | Unikt enkeltkolonneindeks. NULL behandles efter PostgreSQL-regler. |
| `U1-del`, `U2-del` | Del af et sammensat unikt indeks i den pågældende tabel. Feltet er ikke unikt alene. Se indekslisten. |
| `NOT NULL` / `NULL` | Kolonnen er obligatorisk / nullable i den aktuelle EF-model. |
| `max=n`, `numeric(p,s)` | Maksimal tekstlængde og decimalpræcision/skala. |
| `EF:OnAdd` | EF angiver værdigenerering ved indsættelse; det er ikke i sig selv en SQL-default. |
| `concurrency` | Feltet bruges til optimistisk samtidighedskontrol. |
| `List_string` | Mermaid-kompatibel skrivemåde for CLR-typen `List<string>`. |
| `||` / `o|` / `o{` / `|{` | Præcis én / nul eller én / nul eller flere / én eller flere. |
| Fuld streg / stiplet streg | Identificerende / ikke-identificerende relation: FK er / er ikke del af den afhængige tabels PK. Begge er rigtige foreign keys. |
| `Cascade`, `Restrict`, `SetNull` på relationen | Sletteadfærd, når den refererede række slettes. |

Felttyperne i Mermaid er CLR-typer med nullability i annotationen. De præcise PostgreSQL-typer, inklusive `jsonb`, `bytea`, `date` og tidszonetyper, fremgår af kolonnelisten nedenfor. Entities er primært konfigureret med Fluent API og konventioner; C#-modifieren `required` betyder ikke automatisk SQL `NOT NULL` for en nullable property.

## Forhold, som diagrammet gør eksplicit

- `Auction` ↔ `Lot` er mange-til-mange via `AuctionLot`. Kombinationen `(AuctionId, LotId)` er unik, og join-entity har sin egen PK samt mængde, startpris og budstatus.
- `Auction` ↔ `Customers` er mange-til-mange via `AuctionRegistrations`, hvis PK er `(AuctionId, CustomerId)`.
- `LoginUsers` tilhører enten en kunde eller en medarbejder, aldrig begge. Hver ejer kan have højst ét login; ejerskabet og rollen håndhæves af `CK_LoginUser_Owner`. De to valgfrie relationer skal derfor læses sammen med denne XOR-regel.
- `AuctionInvoiceLines.AuctionLotId` og `WinningBidId` er hver især unikke. Databasen tillader derfor højst én fakturalinje pr. auktionspost og højst én pr. vinderbud. EF-navigationerne er konfigureret med `WithMany`; diagrammets kardinalitet følger også de unikke indeks.
- `AuctionLot.CurrentHighestBidId` er valgfri og ikke unik. Databasen tillader, at flere auktionsposter peger på samme bud. Der er ingen FK-constraint, der også beviser, at dette bud tilhører netop den samme auktionspost.
- `Invoice` er den eksisterende legacy-model og vises sammen med `AuctionInvoices` og `AuctionInvoiceLines`. Den har ikke de nye fakturalinjers unikke bud-/auktionspostbegrænsninger.
- `AspNet*`-tabellerne følger med, fordi context arver fra `IdentityDbContext`. De er et separat skemaområde uden foreign keys til `LoginUsers`.
- `Outbox` findes i `Auktionshuset.Domain/Entities/Outbox.cs`, men er ikke med i EF-modellen. Den er vist særskilt og uden PK/FK-markeringer; den persisterede entity hedder `OutboxMessage` og mappes til `OutboxMessages`.

## Referencefelter uden foreign key

Følgende felter er ikke tegnet som relationer. Der er ikke en tilsvarende FK eller kardinalitetsgaranti i modellen:

| Entity/tabel | Felter | Bemærkning |
|---|---|---|
| `Lot` | `ImageFileName` | Filnavn til billedlager; ingen FK til `StoredLotImages.FileName`. |
| `RefreshTokens` | `UserId`, `FamilyId`, `ReplacedByTokenId` | Login- og tokenreferencer uden bruger-FK eller selvrefererende FK. Brugere kan også være konfigurerede bootstrap-brugere. |
| `BidCommandKeys` | `AuctionLotId`, `DeviceSessionId`, `BidId`, `RequestId` | Idempotens-/resultatdata. Kun `CustomerId` har FK. |
| `AuctionInvoices` | `PaidByUserId` | Auditværdi, ingen FK til `LoginUsers` eller Identity. |
| `AuctionInvoiceLines` | `CollectedByUserId` | Auditværdi, ingen FK til `LoginUsers` eller Identity. |
| `MessageReplayAudits` | `MessageId`, `EventId`, `ActorId` | Auditdata; ingen FK til inbox/outbox eller brugertabeller. |
| `InboxMessages` | `EventId`, `TransportMessageId` | Beskedidentitet, ingen relation til en separat eventtabel. |
| `OutboxMessages`, `InboxMessages` | `LeaseToken` | Teknisk leasingtoken, ikke en entityreference. |

`BillingSequences`, `StoredLotImages`, `RefreshTokens`, `OutboxMessages`, `InboxMessages` og `MessageReplayAudits` har ingen foreign keys og står derfor uden forbindelseslinjer.

## Alle relationer

| Refereret tabel | Afhængig tabel.FK | Refereret pr. afhængig | Afhængige pr. refereret | Sletning | EF-navigation |
|---|---|---|---|---|---|
| AspNetRoles | AspNetRoleClaims.RoleId | 1 | 0..* | Cascade | uden navigation / uden invers navigation |
| AspNetUsers | AspNetUserClaims.UserId | 1 | 0..* | Cascade | uden navigation / uden invers navigation |
| AspNetUsers | AspNetUserLogins.UserId | 1 | 0..* | Cascade | uden navigation / uden invers navigation |
| AspNetRoles | AspNetUserRoles.RoleId | 1 | 0..* | Cascade | uden navigation / uden invers navigation |
| AspNetUsers | AspNetUserRoles.UserId | 1 | 0..* | Cascade | uden navigation / uden invers navigation |
| AspNetUsers | AspNetUserTokens.UserId | 1 | 0..* | Cascade | uden navigation / uden invers navigation |
| AuctionHouse | Auction.AuctionHouseId | 1 | 0..* | Restrict | AuctionHouse / Auctions |
| Employee | Auction.EmployeeId | 0..1 | 0..* | Restrict | Employee / Auctions |
| AuctionLot | AuctionInvoiceLines.AuctionLotId | 1 | 0..1 | Restrict | AuctionLot / uden invers navigation |
| AuctionInvoices | AuctionInvoiceLines.InvoiceId | 1 | 0..* | Restrict | Invoice / Lines |
| Bids | AuctionInvoiceLines.WinningBidId | 1 | 0..1 | Restrict | WinningBid / uden invers navigation |
| Auction | AuctionInvoices.AuctionId | 1 | 0..* | Restrict | Auction / uden invers navigation |
| Customers | AuctionInvoices.CustomerId | 1 | 0..* | Restrict | Customer / uden invers navigation |
| Auction | AuctionLot.AuctionId | 1 | 0..* | Cascade | Auction / AuctionLots |
| Bids | AuctionLot.CurrentHighestBidId | 0..1 | 0..* | SetNull | CurrentHighestBid / CurrentHighestForAuctionLots |
| Lot | AuctionLot.LotId | 1 | 0..* | Restrict | Lot / AuctionLots |
| Auction | AuctionRegistrations.AuctionId | 1 | 0..* | Cascade | Auction / uden invers navigation |
| Customers | AuctionRegistrations.CustomerId | 1 | 0..* | Cascade | Customer / uden invers navigation |
| Customers | BidCommandKeys.CustomerId | 1 | 0..* | Restrict | uden navigation / uden invers navigation |
| AuctionLot | Bids.AuctionLotId | 1 | 0..* | Restrict | AuctionLot / Bids |
| Customers | Bids.CustomerId | 1 | 0..* | Restrict | Customer / uden invers navigation |
| DeviceSessions | Bids.DeviceSessionId | 0..1 | 0..* | Restrict | DeviceSession / Bids |
| AuctionHouse | Device.AuctionHouseId | 1 | 0..* | Restrict | AuctionHouse / Devices |
| Auction | DeviceSessions.AuctionId | 1 | 0..* | Restrict | Auction / DeviceSessions |
| Customers | DeviceSessions.CustomerId | 1 | 0..* | Restrict | Customer / DeviceSessions |
| Device | DeviceSessions.DeviceId | 1 | 0..* | Restrict | Device / DeviceSessions |
| AuctionHouse | Employee.AuctionHouseId | 1 | 0..* | Restrict | AuctionHouse / Employees |
| AuctionLot | Invoice.AuctionLotId | 1 | 0..* | Restrict | AuctionLot / Invoices |
| Customers | Invoice.CustomerId | 1 | 0..* | Restrict | Customer / Invoices |
| Bids | Invoice.WinningBidId | 1 | 0..* | Restrict | WinningBid / WinningInvoices |
| Customers | LoginUsers.CustomerId | 0..1 | 0..1 | Cascade | Customer / uden invers navigation |
| Employee | LoginUsers.EmployeeId | 0..1 | 0..1 | Restrict | Employee / uden invers navigation |
| AuctionHouse | Lot.AuctionHouseId | 1 | 0..* | Restrict | AuctionHouse / Lots |

## Komplet kolonne- og constraintkatalog

### AuctionHouse

Entity: `AuctionHouse`. Primærnøgle: `(AuctionHouseId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| AuctionHouseId | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| Address | string | text | — | NOT NULL |
| AuctionHouseName | string | text | — | NOT NULL |
| CVRNumber | int | integer | — | NOT NULL |
| Email | string | text | — | NOT NULL |
| PhoneNumber | string | text | — | NOT NULL |

### Employee

Entity: `Employee`. Primærnøgle: `(EmployeeId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| EmployeeId | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| Address | string | character varying(200) | — | NOT NULL; max=200 |
| AuctionHouseId | Guid | uuid | FK | NOT NULL |
| BirthDate | DateOnly | date | — | NOT NULL |
| FirstName | string | character varying(80) | — | NOT NULL; max=80 |
| LastName | string | character varying(80) | — | NOT NULL; max=80 |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_Employee_AuctionHouseId | AuctionHouseId | Nej | — | ASC |

### Auction

Entity: `Auction`. Primærnøgle: `(AuctionId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| AuctionId | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| AuctionHouseId | Guid | uuid | FK | NOT NULL |
| AuctionNumber | long? | bigint | UK | NULL |
| AuctionStatus | string | character varying(32) | — | NOT NULL; max=32 |
| ClosedAtTime | DateTime? | timestamp with time zone | — | NULL |
| EmployeeId | Guid? | uuid | FK | NULL |
| EndedAt | DateTime? | timestamp with time zone | — | NULL |
| Name | string | character varying(120) | — | NOT NULL; max=120 |
| StartedAtTime | DateTime? | timestamp with time zone | — | NULL |
| StartsAt | DateTime | timestamp with time zone | — | NOT NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_Auction_AuctionHouseId | AuctionHouseId | Nej | — | ASC |
| IX_Auction_AuctionNumber | AuctionNumber | Ja | — | ASC |
| IX_Auction_EmployeeId | EmployeeId | Nej | — | ASC |

### Lot

Entity: `Lot`. Primærnøgle: `(LotId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| LotId | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| AuctionHouseId | Guid | uuid | FK | NOT NULL |
| Category | string | text | — | NOT NULL |
| Description | string | text | — | NOT NULL |
| EstimatedValue | decimal | numeric | — | NOT NULL |
| ImageFileName | string? | character varying(128) | — | NULL; max=128 |
| Name | string | text | — | NOT NULL |
| Quantity | int | integer | — | NOT NULL |
| Tags | List_string | jsonb | — | NOT NULL; jsonb |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_Lot_AuctionHouseId | AuctionHouseId | Nej | — | ASC |

### AuctionLot

Entity: `AuctionLot`. Primærnøgle: `(AuctionLotId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| AuctionLotId | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| AuctionId | Guid | uuid | FK | NOT NULL; U1-del |
| ClosedAtTime | DateTime? | timestamp with time zone | — | NULL |
| CurrentHighestBidId | Guid? | uuid | FK | NULL |
| LotId | Guid | uuid | FK | NOT NULL; U1-del |
| OpenForBids | bool | boolean | — | NOT NULL |
| Quantity | int | integer | — | NOT NULL |
| StartingPrice | decimal | numeric(18,2) | — | NOT NULL; numeric(18,2) |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_AuctionLot_CurrentHighestBidId | CurrentHighestBidId | Nej | — | ASC |
| IX_AuctionLot_LotId | LotId | Nej | — | ASC |
| IX_AuctionLot_AuctionId_LotId | AuctionId, LotId | Ja | — | ASC |

U1 = `(AuctionId, LotId)`.

### Customers

Entity: `Customer`. Primærnøgle: `(CustomerId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| CustomerId | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| Address | string | text | — | NOT NULL |
| BirthDate | DateOnly | date | — | NOT NULL |
| Email | string | text | — | NOT NULL |
| FirstName | string | text | — | NOT NULL |
| LastName | string | text | — | NOT NULL |
| PhoneNumber | string | text | — | NOT NULL |

### AuctionRegistrations

Entity: `AuctionRegistration`. Primærnøgle: `(AuctionId, CustomerId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| AuctionId | Guid | uuid | PK, FK | NOT NULL; sammensat PK |
| CustomerId | Guid | uuid | PK, FK | NOT NULL; sammensat PK |
| RegisteredAt | DateTime | timestamp with time zone | — | NOT NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_AuctionRegistrations_CustomerId | CustomerId | Nej | — | ASC |

### Device

Entity: `Device`. Primærnøgle: `(DeviceId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| DeviceId | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| AuctionHouseId | Guid | uuid | FK | NOT NULL |
| DeviceNumber | int | integer | — | NOT NULL |
| Status | string | text | — | NOT NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_Device_AuctionHouseId | AuctionHouseId | Nej | — | ASC |

### DeviceSessions

Entity: `DeviceSession`. Primærnøgle: `(DeviceSessionId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| DeviceSessionId | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| AuctionId | Guid | uuid | FK | NOT NULL |
| CustomerId | Guid | uuid | FK | NOT NULL |
| DeviceId | Guid | uuid | FK | NOT NULL |
| EndedAt | DateTime? | timestamp with time zone | — | NULL |
| StartedAt | DateTime | timestamp with time zone | — | NOT NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_DeviceSessions_AuctionId | AuctionId | Nej | — | ASC |
| IX_DeviceSessions_CustomerId | CustomerId | Nej | — | ASC |
| IX_DeviceSessions_DeviceId | DeviceId | Nej | — | ASC |

### Bids

Entity: `Bid`. Primærnøgle: `(BidId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| BidId | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| Amount | decimal | numeric | — | NOT NULL |
| AuctionLotId | Guid | uuid | FK | NOT NULL; U1-del |
| CustomerId | Guid | uuid | FK | NOT NULL |
| DeviceSessionId | Guid? | uuid | FK | NULL |
| PlacedAt | DateTime | timestamp with time zone | — | NOT NULL |
| SequenceNumber | int | integer | — | NOT NULL; U1-del |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_Bids_CustomerId | CustomerId | Nej | — | ASC |
| IX_Bids_DeviceSessionId | DeviceSessionId | Nej | — | ASC |
| IX_Bids_AuctionLotId_SequenceNumber | AuctionLotId, SequenceNumber | Ja | — | ASC |
| IX_Bids_AuctionLotId_Amount_SequenceNumber | AuctionLotId, Amount, SequenceNumber | Nej | — | ASC, DESC, ASC |

U1 = `(AuctionLotId, SequenceNumber)`.

### BidCommandKeys

Entity: `BidCommandKey`. Primærnøgle: `(BidCommandKeyId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| BidCommandKeyId | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| Accepted | bool | boolean | — | NOT NULL |
| AuctionLotId | Guid | uuid | — | NOT NULL |
| BidId | Guid? | uuid | — | NULL |
| CurrentPrice | decimal | numeric(18,2) | — | NOT NULL; numeric(18,2) |
| CustomerId | Guid | uuid | FK | NOT NULL; U1-del |
| DeviceSessionId | Guid? | uuid | — | NULL |
| ErrorCode | string? | character varying(64) | — | NULL; max=64 |
| ProcessedAt | DateTime | timestamp with time zone | — | NOT NULL |
| RequestId | Guid | uuid | — | NOT NULL; U1-del |
| RequestedAmount | decimal | numeric(18,2) | — | NOT NULL; numeric(18,2) |
| SequenceNumber | int? | integer | — | NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_BidCommandKeys_CustomerId_RequestId | CustomerId, RequestId | Ja | — | ASC |

U1 = `(CustomerId, RequestId)`.

### LoginUsers

Entity: `LoginUser`. Primærnøgle: `(UserId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| UserId | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| CustomerId | Guid? | uuid | FK, UK | NULL |
| Email | string | text | UK | NOT NULL |
| EmployeeId | Guid? | uuid | FK, UK | NULL |
| IsActive | bool | boolean | — | NOT NULL; default=true; EF:OnAdd |
| PasswordHash | string | text | — | NOT NULL |
| Role | string | text | — | NOT NULL |
| SessionVersion | int | integer | — | NOT NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_LoginUsers_CustomerId | CustomerId | Ja | — | ASC |
| IX_LoginUsers_Email | Email | Ja | — | ASC |
| IX_LoginUsers_EmployeeId | EmployeeId | Ja | — | ASC |

**Check constraint `CK_LoginUser_Owner`:**

```sql
("CustomerId" IS NOT NULL AND "EmployeeId" IS NULL AND "Role" = 'Customer') OR ("CustomerId" IS NULL AND "EmployeeId" IS NOT NULL AND "Role" IN ('Admin', 'AuctionAdmin', 'LotAdmin', 'Employee'))
```

### AuctionInvoices

Entity: `AuctionInvoice`. Primærnøgle: `(Id)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| Id | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| AuctionId | Guid | uuid | FK | NOT NULL; U1-del |
| AuctionName | string | text | — | NOT NULL |
| Currency | string | text | — | NOT NULL |
| CustomerAddress | string | text | — | NOT NULL |
| CustomerEmail | string | text | — | NOT NULL |
| CustomerId | Guid | uuid | FK | NOT NULL; U1-del |
| CustomerName | string | text | — | NOT NULL |
| HammerTotal | decimal | numeric(20,2) | — | NOT NULL; numeric(20,2) |
| IssuedAt | DateTime | timestamp with time zone | — | NOT NULL |
| Number | string | text | UK | NOT NULL |
| PaidAmount | decimal? | numeric(20,2) | — | NULL; numeric(20,2) |
| PaidAt | DateTime? | timestamp with time zone | — | NULL |
| PaidByName | string? | text | — | NULL |
| PaidByUserId | Guid? | uuid | — | NULL |
| PaymentMethod | string? | text | — | NULL |
| PaymentTerms | string | text | — | NOT NULL |
| PremiumPercent | decimal | numeric(7,4) | — | NOT NULL; numeric(7,4) |
| PremiumTotal | decimal | numeric(20,2) | — | NOT NULL; numeric(20,2) |
| SellerAddress | string | text | — | NOT NULL |
| SellerCvr | int | integer | — | NOT NULL |
| SellerEmail | string | text | — | NOT NULL |
| SellerName | string | text | — | NOT NULL |
| Sequence | long | bigint | UK | NOT NULL |
| Total | decimal | numeric(20,2) | — | NOT NULL; numeric(20,2) |
| VatPercent | decimal | numeric(7,4) | — | NOT NULL; numeric(7,4) |
| VatTotal | decimal | numeric(20,2) | — | NOT NULL; numeric(20,2) |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_AuctionInvoices_CustomerId | CustomerId | Nej | — | ASC |
| IX_AuctionInvoices_Number | Number | Ja | — | ASC |
| IX_AuctionInvoices_Sequence | Sequence | Ja | — | ASC |
| IX_AuctionInvoices_AuctionId_CustomerId | AuctionId, CustomerId | Ja | — | ASC |

U1 = `(AuctionId, CustomerId)`.

### AuctionInvoiceLines

Entity: `AuctionInvoiceLine`. Primærnøgle: `(Id)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| Id | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| AuctionLotId | Guid | uuid | FK, UK | NOT NULL |
| CollectedAt | DateTime? | timestamp with time zone | — | NULL |
| CollectedByName | string? | text | — | NULL |
| CollectedByUserId | Guid? | uuid | — | NULL |
| Description | string | text | — | NOT NULL |
| HammerPrice | decimal | numeric(20,2) | — | NOT NULL; numeric(20,2) |
| HammerVat | decimal | numeric(20,2) | — | NOT NULL; numeric(20,2) |
| InvoiceId | Guid | uuid | FK | NOT NULL |
| Name | string | text | — | NOT NULL |
| Premium | decimal | numeric(20,2) | — | NOT NULL; numeric(20,2) |
| PremiumVat | decimal | numeric(20,2) | — | NOT NULL; numeric(20,2) |
| Quantity | int | integer | — | NOT NULL |
| Total | decimal | numeric(20,2) | — | NOT NULL; numeric(20,2) |
| WinningBidId | Guid | uuid | FK, UK | NOT NULL |
| WonAt | DateTime | timestamp with time zone | — | NOT NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_AuctionInvoiceLines_AuctionLotId | AuctionLotId | Ja | — | ASC |
| IX_AuctionInvoiceLines_InvoiceId | InvoiceId | Nej | — | ASC |
| IX_AuctionInvoiceLines_WinningBidId | WinningBidId | Ja | — | ASC |

### Invoice

Entity: `Invoice`. Primærnøgle: `(InvoiceId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| InvoiceId | Guid | uuid | PK | NOT NULL; EF:OnAdd; legacy |
| AuctionLotId | Guid | uuid | FK | NOT NULL |
| CustomerId | Guid | uuid | FK | NOT NULL |
| InvoiceDateTime | DateTime | timestamp with time zone | — | NOT NULL |
| InvoiceNumber | int | integer | — | NOT NULL |
| Status | string | text | — | NOT NULL |
| WinningBidId | Guid | uuid | FK | NOT NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_Invoice_AuctionLotId | AuctionLotId | Nej | — | ASC |
| IX_Invoice_CustomerId | CustomerId | Nej | — | ASC |
| IX_Invoice_WinningBidId | WinningBidId | Nej | — | ASC |

### AspNetRoleClaims

Entity: `IdentityRoleClaim`. Primærnøgle: `(Id)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| Id | int | integer | PK | NOT NULL; EF:OnAdd |
| ClaimType | string? | text | — | NULL |
| ClaimValue | string? | text | — | NULL |
| RoleId | string | text | FK | NOT NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_AspNetRoleClaims_RoleId | RoleId | Nej | — | ASC |

### AspNetRoles

Entity: `IdentityRole`. Primærnøgle: `(Id)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| Id | string | text | PK | NOT NULL |
| ConcurrencyStamp | string? | text | — | NULL; concurrency |
| Name | string? | character varying(256) | — | NULL; max=256 |
| NormalizedName | string? | character varying(256) | UK | NULL; max=256 |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| RoleNameIndex | NormalizedName | Ja | — | ASC |

### AspNetUserClaims

Entity: `IdentityUserClaim`. Primærnøgle: `(Id)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| Id | int | integer | PK | NOT NULL; EF:OnAdd |
| ClaimType | string? | text | — | NULL |
| ClaimValue | string? | text | — | NULL |
| UserId | string | text | FK | NOT NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_AspNetUserClaims_UserId | UserId | Nej | — | ASC |

### AspNetUserLogins

Entity: `IdentityUserLogin`. Primærnøgle: `(LoginProvider, ProviderKey)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| LoginProvider | string | text | PK | NOT NULL; sammensat PK |
| ProviderKey | string | text | PK | NOT NULL; sammensat PK |
| ProviderDisplayName | string? | text | — | NULL |
| UserId | string | text | FK | NOT NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_AspNetUserLogins_UserId | UserId | Nej | — | ASC |

### AspNetUserRoles

Entity: `IdentityUserRole`. Primærnøgle: `(UserId, RoleId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| UserId | string | text | PK, FK | NOT NULL; sammensat PK |
| RoleId | string | text | PK, FK | NOT NULL; sammensat PK |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_AspNetUserRoles_RoleId | RoleId | Nej | — | ASC |

### AspNetUsers

Entity: `IdentityUser`. Primærnøgle: `(Id)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| Id | string | text | PK | NOT NULL |
| AccessFailedCount | int | integer | — | NOT NULL |
| ConcurrencyStamp | string? | text | — | NULL; concurrency |
| Email | string? | character varying(256) | — | NULL; max=256 |
| EmailConfirmed | bool | boolean | — | NOT NULL |
| LockoutEnabled | bool | boolean | — | NOT NULL |
| LockoutEnd | DateTimeOffset? | timestamp with time zone | — | NULL |
| NormalizedEmail | string? | character varying(256) | — | NULL; max=256 |
| NormalizedUserName | string? | character varying(256) | UK | NULL; max=256 |
| PasswordHash | string? | text | — | NULL |
| PhoneNumber | string? | text | — | NULL |
| PhoneNumberConfirmed | bool | boolean | — | NOT NULL |
| SecurityStamp | string? | text | — | NULL |
| TwoFactorEnabled | bool | boolean | — | NOT NULL |
| UserName | string? | character varying(256) | — | NULL; max=256 |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| EmailIndex | NormalizedEmail | Nej | — | ASC |
| UserNameIndex | NormalizedUserName | Ja | — | ASC |

### AspNetUserTokens

Entity: `IdentityUserToken`. Primærnøgle: `(UserId, LoginProvider, Name)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| UserId | string | text | PK, FK | NOT NULL; sammensat PK |
| LoginProvider | string | text | PK | NOT NULL; sammensat PK |
| Name | string | text | PK | NOT NULL; sammensat PK |
| Value | string? | text | — | NULL |

### BillingSequences

Entity: `BillingSequence`. Primærnøgle: `(Id)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| Id | int | integer | PK | NOT NULL |
| LastAuction | long | bigint | — | NOT NULL |
| LastInvoice | long | bigint | — | NOT NULL |

### RefreshTokens

Entity: `RefreshToken`. Primærnøgle: `(Id)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| Id | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| CreatedAt | DateTimeOffset | timestamp with time zone | — | NOT NULL |
| ExpiresAt | DateTimeOffset | timestamp with time zone | — | NOT NULL |
| FamilyId | Guid | uuid | — | NOT NULL |
| FamilyRevokedAt | DateTimeOffset? | timestamp with time zone | — | NULL |
| ReplacedByTokenId | Guid? | uuid | — | NULL |
| RevokedAt | DateTimeOffset? | timestamp with time zone | — | NULL |
| SessionVersion | int | integer | — | NOT NULL |
| TokenHash | string | character varying(64) | UK | NOT NULL; max=64 |
| UserId | Guid | uuid | — | NOT NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_RefreshTokens_ExpiresAt | ExpiresAt | Nej | — | ASC |
| IX_RefreshTokens_FamilyId | FamilyId | Nej | — | ASC |
| IX_RefreshTokens_TokenHash | TokenHash | Ja | — | ASC |

### StoredLotImages

Entity: `StoredLotImage`. Primærnøgle: `(FileName)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| FileName | string | character varying(128) | PK | NOT NULL; max=128 |
| Content | byte[] | bytea | — | NOT NULL |
| ContentType | string | character varying(32) | — | NOT NULL; max=32 |

### OutboxMessages

Entity: `OutboxMessage`. Primærnøgle: `(OutboxId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| OutboxId | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| Attempts | int | integer | — | NOT NULL |
| Error | string? | text | — | NULL |
| EventType | string | text | — | NOT NULL |
| LeaseExpiresAtTime | DateTime? | timestamp with time zone | — | NULL |
| LeaseToken | Guid? | uuid | — | NULL |
| NextAttemptAtTime | DateTime | timestamp with time zone | — | NOT NULL; default=CURRENT_TIMESTAMP; EF:OnAdd |
| OccurredAtTime | DateTime | timestamp with time zone | — | NOT NULL |
| Payload | string | text | — | NOT NULL |
| ProcessedAtTime | DateTime? | timestamp with time zone | — | NULL |
| QuarantinedAtTime | DateTime? | timestamp with time zone | — | NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| OutboxMessages_Retention | ProcessedAtTime, OutboxId | Nej | "ProcessedAtTime" IS NOT NULL AND "QuarantinedAtTime" IS NULL | ASC |
| OutboxMessages_Quarantine | QuarantinedAtTime, OutboxId | Nej | "ProcessedAtTime" IS NULL AND "QuarantinedAtTime" IS NOT NULL | ASC |
| OutboxMessages_Ready | NextAttemptAtTime, OccurredAtTime, OutboxId | Nej | "ProcessedAtTime" IS NULL AND "QuarantinedAtTime" IS NULL | ASC |

### InboxMessages

Entity: `InboxMessage`. Primærnøgle: `(InboxId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| InboxId | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| Attempts | int | integer | — | NOT NULL; default=0; EF:OnAdd |
| ConsumerName | string | text | — | NOT NULL; U1-del |
| Error | string? | text | — | NULL |
| EventId | Guid? | uuid | — | NULL |
| EventType | string | text | — | NOT NULL |
| InboxQuarantinedAtTime | DateTime? | timestamp with time zone | — | NULL |
| LeaseExpiresAtTime | DateTime? | timestamp with time zone | — | NULL |
| LeaseToken | Guid? | uuid | — | NULL |
| MessageKey | string | text | — | NOT NULL; U1-del |
| NextAttemptAtTime | DateTime | timestamp with time zone | — | NOT NULL; default=CURRENT_TIMESTAMP; EF:OnAdd |
| Payload | byte[] | bytea | — | NOT NULL |
| ProcessedAtTime | DateTime? | timestamp with time zone | — | NULL |
| ReceivedAtTime | DateTime | timestamp with time zone | — | NOT NULL; default=CURRENT_TIMESTAMP; EF:OnAdd |
| TransportMessageId | string? | text | — | NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| InboxMessages_Identity | ConsumerName, MessageKey | Ja | — | ASC |
| InboxMessages_Quarantine | InboxQuarantinedAtTime, InboxId | Nej | "ProcessedAtTime" IS NULL AND "InboxQuarantinedAtTime" IS NOT NULL | ASC |
| InboxMessages_Ready | ConsumerName, NextAttemptAtTime, ReceivedAtTime, InboxId | Nej | "ProcessedAtTime" IS NULL AND "InboxQuarantinedAtTime" IS NULL | ASC |

U1 = `(ConsumerName, MessageKey)`.

### MessageReplayAudits

Entity: `MessageReplayAudit`. Primærnøgle: `(ReplayId)`.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| ReplayId | Guid | uuid | PK | NOT NULL; EF:OnAdd |
| ActorId | string | character varying(200) | — | NOT NULL; max=200 |
| EventId | Guid? | uuid | — | NULL |
| MessageId | Guid | uuid | — | NOT NULL |
| Pipeline | string | character varying(10) | — | NOT NULL; max=10 |
| PreviousAttempts | int | integer | — | NOT NULL |
| PreviousError | string? | text | — | NULL |
| Reason | string | character varying(500) | — | NOT NULL; max=500 |
| RequestedAtTime | DateTime | timestamp with time zone | — | NOT NULL |

**Indeks** (inklusive EF-genererede FK-indeks):

| Navn | Felter | Unikt | Filter | Sortering |
|---|---|---|---|---|
| IX_MessageReplayAudits_Pipeline_MessageId_RequestedAtTime | Pipeline, MessageId, RequestedAtTime | Nej | — | ASC |

### Outbox_ikke_EF_mappet

Domæneklasse `Outbox`; ikke en tabel i EF-modellen.

| Felt | CLR-type | PostgreSQL-type | Nøgle | Annotation |
|---|---|---|---|---|
| OutboxId | Guid | ikke mappet | — | C# required; IKKE EF-MAPPET |
| EventType | string | ikke mappet | — | C# required; IKKE EF-MAPPET |
| Content | string | ikke mappet | — | C# required; IKKE EF-MAPPET |
| OccurredAtTime | DateTime | ikke mappet | — | C# required; IKKE EF-MAPPET |
| ProcessedAtTime | DateTime? | ikke mappet | — | C# required; nullable; IKKE EF-MAPPET |

## Kilder og validering

- `Auktionshuset.Domain/Entities/*.cs` og de fem ekstra persisterede infrastrukturelle entities.
- `Auktionshuset.Infrastructure/Database/AHDBContext.cs` og `Data/Configuration/*.cs`.
- EF Core-designmodellen er eksporteret ved at instantiere context med Npgsql, uden at åbne en databaseforbindelse. Alle 28 tabeller, 224 kolonner og 33 FK er sammenholdt med genereret Mermaid.
- `Database.HasPendingModelChanges()` returnerer `false`: den aktuelle model matcher `AHDBContextModelSnapshot`.
- Mermaid er parset og renderet med Mermaid 11.12.1; grafikken bruger de samme entityblokke og relationer som den komplette kode. Billedet opdeles i auktion/bud, login/fakturering, Identity og enkeltstående entities for læsbarhed. Fem entities gentages kun med PK som reference i login-/faktureringspanelet; deres fulde felter står i auktions-/budpanelet.
- Build af model-eksporten og de refererede projekter lykkedes med 0 advarsler og 0 fejl. De fokuserede `AHDBContextModelTests` blev forsøgt, men testkørslen blev afbrudt, fordi testhost ikke kunne etablere forbindelse inden for 90 sekunder. Testene er derfor ikke registreret som bestået; model-eksport og Mermaid-dækningskontrol blev kørt selvstændigt og lykkedes.
