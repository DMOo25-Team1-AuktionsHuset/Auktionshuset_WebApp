using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.Abstraction.Auction;
using System.Data;
using Auktionshuset.Application.Abstraction;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Auktionshuset.Infrastructure.Service.Auctions
{
    public sealed class PostgresAuctionWriteSessionFactory(
        AHDBContext context,
        IUnitOfWork unitOfWork) : IAuctionWriteSessionFactory
    {
        public async Task<IAuctionWriteSession> BeginAsync(
            Guid auctionId,
            CancellationToken cancellationToken)
        {
            IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

            try
            {
                await SqlAuctionWriteLock.AcquireAsync(
                    context,
                    auctionId,
                    cancellationToken);

                return new Session(transaction, unitOfWork);
            }
            catch
            {
                await transaction.DisposeAsync();
                throw;
            }
        }

        private sealed class Session(
            IDbContextTransaction transaction,
            IUnitOfWork unitOfWork) : IAuctionWriteSession
        {
            public async Task CommitAsync(CancellationToken cancellationToken)
            {
                await unitOfWork.CommitBatchAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            public ValueTask DisposeAsync() => transaction.DisposeAsync();
        }
    }
}
