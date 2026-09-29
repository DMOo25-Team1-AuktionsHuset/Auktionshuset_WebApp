using System.Data;
using System.Globalization;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Auktionshuset.Infrastructure.Service.Auctions
{
    public class SqlAuctionLotLock
    {
        public static async Task AcquireAsync(AHDBContext db, Guid auctionLotId, CancellationToken cancellationToken)
        {
            if (db.Database.CurrentTransaction == null)
            {
                throw new InvalidOperationException("Begin the database transaction before acquiring the item lock");
            }

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                SELECT "AuctionLotId"
                FROM "AuctionLot"
                WHERE "AuctionLotId" = {auctionLotId}
                FOR UPDATE
                """,
                cancellationToken);
        }
    }
}
