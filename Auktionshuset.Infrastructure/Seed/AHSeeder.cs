using Auktionshuset.Domain.Entities;
using Auktionshuset.Domain;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Seed
{
    internal static class AHSeeder
    {
        internal static readonly Guid DefaultAuctionHouseId = AuctionHouseDefaults.DefaultAuctionHouseId;

        public static async Task SeedAsync(
            AHDBContext context,
            CancellationToken cancellationToken = default)
        {
            bool exists = await context.AuctionHouse
                .AnyAsync(auctionHouse => auctionHouse.AuctionHouseId == DefaultAuctionHouseId,
                    cancellationToken);

            if (exists)
            {
                return;
            }

            context.AuctionHouse.Add(new AuctionHouse
            {
                AuctionHouseId = DefaultAuctionHouseId,
                AuctionHouseName = "Haderslev Auktionshus",
                Address = "Auktionsvej 1, 6100 Haderslev",
                CVRNumber = 31415926,
                PhoneNumber = "+45 74 52 10 00",
                Email = "kontakt@haderslev-auktionshus.dk"
            });

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
