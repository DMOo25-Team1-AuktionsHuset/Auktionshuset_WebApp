using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Auktionshuset.Infrastructure.Service.Auctions
{
    public class SqlAuctionLotLock
    {
        public static async Task AcquireAsync(DbContext db, Guid auctionLotId, CancellationToken cancellationToken)
        {
            var transaction = db.Database.CurrentTransaction
                ?? throw new InvalidOperationException("Begin the database transaction before acquiring the item lock");

            await using var command = db.Database.GetDbConnection().CreateCommand();

            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = """
                DECLARE @lockResult int;
                EXEC @lockResult = sys.sp_getapplock
                    @Resource = @resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = '5000';
                SELECT @lockResult;
                """;

            var resource = command.CreateParameter();
            resource.ParameterName = "@resource";
            resource.DbType = DbType.String;
            resource.Size = 255;
            resource.Value = $"AuctionLot:{auctionLotId:N}";
            command.Parameters.Add(resource);

            var resultValue = await command.ExecuteScalarAsync(cancellationToken);
            var result = Convert.ToInt32(resultValue, CultureInfo.InvariantCulture);

            // 0 and 1 indicate that the lock was acuired
            if (result < 0)
            {
                throw new TimeoutException($"Could not acquire the biddomg lock for the auction lot: {auctionLotId}");
            }
        }
    }
}
