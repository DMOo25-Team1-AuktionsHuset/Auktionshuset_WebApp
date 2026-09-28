using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Seed
{
    internal static class LotSeeder
    {
        private static readonly Lot[] Lots =
        [
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000001"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Royal Copenhagen Musselmalet stel", Category = "Porcelæn", Quantity = 24, EstimatedValue = 2400m, Description = "Et klassisk sæt musselmalet porcelæn med middagstallerkener, sidetallerkener og skåle.", Tags = ["Royal Copenhagen", "porcelæn", "stel"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000002"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Dansk teak skrivebord", Category = "Møbler", Quantity = 1, EstimatedValue = 3200m, Description = "Skrivebord i teak med skuffer og afrundede kanter, dansk design fra midten af århundredet.", Tags = ["teak", "dansk design", "skrivebord"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000003"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Olie på lærred, fjordlandskab", Category = "Kunst", Quantity = 1, EstimatedValue = 1800m, Description = "Indrammet fjordlandskab i afdæmpede farver, signeret nederst til højre.", Tags = ["maleri", "landskab", "indrammet"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000004"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Omega herreur", Category = "Ure", Quantity = 1, EstimatedValue = 5600m, Description = "Klassisk mekanisk herreur med stållænke og lys urskive. Fremstår med almindelige brugsspor.", Tags = ["Omega", "ur", "mekanisk"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000005"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Sølvbestik, dansk mønster", Category = "Sølv", Quantity = 18, EstimatedValue = 4100m, Description = "Samling af danske sølvbestikdele bestående af gafler, skeer og serveringsdele.", Tags = ["sølv", "bestik", "dansk"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000006"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "PH 5 pendel", Category = "Belysning", Quantity = 1, EstimatedValue = 2900m, Description = "Pendel i klassisk flerskærmsdesign med hvidlakerede skærme.", Tags = ["PH", "lampe", "pendel"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000007"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Bing & Grøndahl figurgruppe", Category = "Porcelæn", Quantity = 1, EstimatedValue = 950m, Description = "Detaljeret porcelænsfigurgruppe med to personer. Ingen synlige skår.", Tags = ["Bing & Grøndahl", "figur", "porcelæn"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000008"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Persisk uldtæppe", Category = "Tæpper", Quantity = 1, EstimatedValue = 3600m, Description = "Håndknyttet uldtæppe med geometrisk mønster i røde og blå nuancer.", Tags = ["persisk", "uld", "håndknyttet"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000009"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Samling af danske frimærker", Category = "Samlerobjekter", Quantity = 1, EstimatedValue = 1250m, Description = "Mappe med danske frimærker fra forskellige årgange, enkelte monteret i album.", Tags = ["frimærker", "Danmark", "samling"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000010"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Georg Jensen lysestager", Category = "Sølv", Quantity = 2, EstimatedValue = 2200m, Description = "Et par moderne lysestager i rustfrit stål fra Georg Jensen.", Tags = ["Georg Jensen", "lysestager", "stål"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000011"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Vintage lænestol i læder", Category = "Møbler", Quantity = 1, EstimatedValue = 2750m, Description = "Lænestol med mørkebrunt læderbetræk og træstel. Patina efter brug.", Tags = ["læder", "lænestol", "vintage"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000012"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Kamera med objektiver", Category = "Foto", Quantity = 1, EstimatedValue = 1950m, Description = "Analogt spejlreflekskamera med to objektiver og bæretaske.", Tags = ["kamera", "analog", "objektiver"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000013"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Blåmalet vitrineskab", Category = "Møbler", Quantity = 1, EstimatedValue = 3400m, Description = "Vitrineskab med glaslåger, hylder og blåmalet træramme.", Tags = ["vitrineskab", "opbevaring", "malet træ"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000014"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Murano glasskål", Category = "Glas", Quantity = 1, EstimatedValue = 1450m, Description = "Dekorativ håndlavet glasskål i grønne og klare nuancer.", Tags = ["Murano", "glas", "skål"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000015"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Kobbergryder, sæt", Category = "Køkkenudstyr", Quantity = 3, EstimatedValue = 1100m, Description = "Tre kobbergryder i forskellige størrelser med håndtag og låg.", Tags = ["kobber", "gryder", "køkken"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000016"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Litografi af dansk kunstner", Category = "Kunst", Quantity = 1, EstimatedValue = 2100m, Description = "Farvelitografi i passepartout og enkel træramme, signeret og nummereret.", Tags = ["litografi", "grafik", "signeret"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000017"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Håndlavet guitar", Category = "Musikinstrumenter", Quantity = 1, EstimatedValue = 4300m, Description = "Akustisk stålstrengsguitar med massivt trædæk og medfølgende kasse.", Tags = ["guitar", "akustisk", "instrument"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000018"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Vægur i egetræ", Category = "Ure", Quantity = 1, EstimatedValue = 850m, Description = "Mekanisk vægur i egetræ med pendul og urskive med romertal.", Tags = ["vægur", "egetræ", "mekanisk"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000019"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Havebænk i støbejern", Category = "Have", Quantity = 1, EstimatedValue = 1600m, Description = "Klassisk havebænk med støbejernsben og lameller i træ.", Tags = ["havebænk", "støbejern", "udendørs"] },
            new Lot { LotId = Guid.Parse("10000000-0000-4000-8000-000000000020"), AuctionHouseId = AHSeeder.DefaultAuctionHouseId, Name = "Bogsamling om dansk historie", Category = "Bøger", Quantity = 12, EstimatedValue = 700m, Description = "Tolv indbundne bøger om dansk historie og kulturarv i god stand.", Tags = ["bøger", "historie", "Danmark"] }
        ];

        public static async Task SeedAsync(
            AHDBContext context,
            CancellationToken cancellationToken = default)
        {
            var lotIds = Lots.Select(lot => lot.LotId).ToArray();
            var existingIds = await context.Lot
                .Where(lot => lotIds.Contains(lot.LotId))
                .Select(lot => lot.LotId)
                .ToListAsync(cancellationToken);

            var existingIdSet = existingIds.ToHashSet();
            var missingLots = Lots
                .Where(lot => !existingIdSet.Contains(lot.LotId))
                .ToArray();

            if (missingLots.Length == 0)
            {
                return;
            }

            context.Lot.AddRange(missingLots);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
