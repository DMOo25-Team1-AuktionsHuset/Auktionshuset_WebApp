using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Service.Auctions
{
    internal static class SqlAuctionWriteLock
    {
        public static Task AcquireAsync(
            AHDBContext context,
            Guid auctionId,
            CancellationToken cancellationToken)
        {
            RequireTransaction(context);

            return context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                SELECT "AuctionId"
                FROM "Auction"
                WHERE "AuctionId" = {auctionId}
                FOR UPDATE  
                """,
                cancellationToken);
        }

        public static async Task<bool> AcquireForLotAsync(
            AHDBContext context,
            Guid auctionLotId,
            CancellationToken cancellationToken)
        {
            RequireTransaction(context);

            Guid? auctionId = await context.AuctionLot
                .AsNoTracking()
                .Where(item => item.AuctionLotId == auctionLotId)
                .Select(item=> (Guid?)item.AuctionId)
                .SingleOrDefaultAsync(cancellationToken);

            if (auctionId is null) return false;

            await AcquireAsync(
                context,
                auctionId.Value,
                cancellationToken);

            return true;
        }

        private static void RequireTransaction(AHDBContext context)
        {
            if (context.Database.CurrentTransaction is null)
            {
                throw new InvalidOperationException(
                    "Start transaktionen før auktionslåsen.");
            }
        }
    }
}
