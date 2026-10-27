using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Repositories;

public sealed class EFLotRepo(AHDBContext dbContext)
    : ILotRepository
{
    public Task AddAsync(
        Lot lot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        dbContext.Lot.Add(lot);

        return Task.CompletedTask;
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

    public Task UpdateAsync(
        Lot lot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        dbContext.Lot.Update(lot);

        return Task.CompletedTask;
    }

    public async Task<bool> DeleteAsync(
        Guid lotId,
        CancellationToken cancellationToken)
    {
        Lot? lot = await dbContext.Lot.FindAsync(
            [lotId],
            cancellationToken);

        if (lot is null)
        {
            return false;
        }

        if (lot.ImageFileName is { } fileName)
        {
            StoredLotImage? image = await dbContext.StoredLotImages.FindAsync([fileName], cancellationToken);
            if (image is not null) dbContext.StoredLotImages.Remove(image);
        }

        dbContext.Lot.Remove(lot);

        return true;
    }
}
