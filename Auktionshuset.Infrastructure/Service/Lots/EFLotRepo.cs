using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Repositories;

public sealed class EFLotRepo(AHDBContext dbContext)
    : ILotRepository
{
    public async Task AddAsync(
        Lot lot,
        CancellationToken cancellationToken)
    {
        dbContext.Lot.Add(lot);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Lot>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Lot
            .AsNoTracking()
            .OrderBy(lot => lot.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Lot?> GetByIdAsync(
        Guid lotId,
        CancellationToken cancellationToken)
    {
        return dbContext.Lot.SingleOrDefaultAsync(
            lot => lot.LotId == lotId,
            cancellationToken);
    }

    public async Task UpdateAsync(
        Lot lot,
        CancellationToken cancellationToken)
    {
        dbContext.Lot.Update(lot);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid lotId,
        CancellationToken cancellationToken)
    {
        var lot = await dbContext.Lot.FindAsync(
            [lotId],
            cancellationToken);

        if (lot is null)
        {
            return false;
        }

        dbContext.Lot.Remove(lot);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}