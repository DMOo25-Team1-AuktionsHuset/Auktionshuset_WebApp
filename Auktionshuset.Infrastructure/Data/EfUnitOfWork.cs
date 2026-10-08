using Auktionshuset.Application.Abstraction;
using Auktionshuset.Infrastructure.Database;

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
