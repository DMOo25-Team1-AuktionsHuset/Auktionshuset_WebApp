using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Abstraction.Auction
{
    public interface IAuctionWriteSessionFactory
    {
        Task<IAuctionWriteSession> BeginAsync(
            Guid auctionId,
            CancellationToken cancellationToken);
    }

    public interface IAuctionWriteSession : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken cancellationToken);
    }
}
