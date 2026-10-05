using Auktionshuset.Application.Abstraction;
using Auktionshuset.Infrastructure.Database;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Data
{
    public class EfUnitOfWork(AHDBContext context) : IUnitOfWork
    {
        public async Task CommitBatchAsync(CancellationToken cancellationToken)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
