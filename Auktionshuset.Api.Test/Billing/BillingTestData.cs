using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;

namespace Auktionshuset.Api.Test.Billing;

internal static class BillingTestData
{
    public static async Task<Guid> SeedAsync(AHDBContext db, Guid customerId, bool closed = true)
    {
        var now = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var house = new AuctionHouse { AuctionHouseId = Guid.NewGuid(), AuctionHouseName = "Fakturatest",
            Address = "Sælgervej 1", CVRNumber = 12345678, PhoneNumber = "12345678", Email = "seller@example.test" };
        var auction = new Auction { AuctionId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId, AuctionHouse = house,
            Name = "Fakturaauktion", StartsAt = now.AddHours(-1), StartedAtTime = now.AddHours(-1),
            ClosedAtTime = closed ? now : null, AuctionStatus = closed ? AuctionStatuses.Ended : AuctionStatuses.Live };
        foreach (var (name, quantity, price) in new[] { ("Tre stole", 3, 1000m), ("Lille vase", 1, 0.05m), ("Usolgt", 1, 0m) })
        {
            var lot = new Lot { LotId = Guid.NewGuid(), AuctionHouseId = house.AuctionHouseId, AuctionHouse = house,
                Name = name, Description = "Historisk beskrivelse", Category = "Test", Quantity = 10, EstimatedValue = 1 };
            var line = new AuctionLot { AuctionLotId = Guid.NewGuid(), AuctionId = auction.AuctionId, Auction = auction,
                LotId = lot.LotId, Lot = lot, Quantity = quantity, StartingPrice = 0, OpenForBids = false, ClosedAtTime = now };
            db.AuctionLot.Add(line);
            if (price > 0)
            {
                var bid = new Bid { BidId = Guid.NewGuid(), AuctionLotId = line.AuctionLotId, Amount = price,
                    CustomerId = customerId, Customer = null!, DeviceSessionId = null, DeviceSession = null,
                    SequenceNumber = 1, PlacedAt = now.AddMinutes(-1) };
                db.Bids.Add(bid);
                await db.SaveChangesAsync();
                line.CurrentHighestBidId = bid.BidId;
            }
        }
        await db.SaveChangesAsync();
        return auction.AuctionId;
    }
}
