using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Database
{
    public static class DBSeeder
    {
        private static readonly Guid DefaultAuctionHouseId =
            Guid.Parse("8cc2c7dc-6244-41e7-805f-a90f9279c540");

        public static async Task SeedAsync(
            AHDBContext dbContext,
            CancellationToken cancellationToken = default)
        {
            var exists = await dbContext.AuctionHouse
                .AnyAsync(auctionHouse => auctionHouse.AuctionHouseId == DefaultAuctionHouseId,
                    cancellationToken);

            if (exists)
            {
                return;
            }

            dbContext.AuctionHouse.Add(new AuctionHouse
            {
                AuctionHouseId = DefaultAuctionHouseId,
                AuctionHouseName = "Haderslev Auktionshus",
                Address = "Auktionsvej 1, 6100 Haderslev",
                CVRNumber = 31415926,
                PhoneNumber = "+45 74 52 10 00",
                Email = "kontakt@haderslev-auktionshus.dk"
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
