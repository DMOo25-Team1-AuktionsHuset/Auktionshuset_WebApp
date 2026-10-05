using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Abstraction
{
    public interface IUnitOfWork
    {
        Task CommitBatchAsync(CancellationToken cancellationToken = default);
    }
}
